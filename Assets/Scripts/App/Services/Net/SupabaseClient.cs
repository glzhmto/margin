using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Margin.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Margin.App.Services.Net
{
    /// <summary>Result of a backend call: a value, or a reason it failed. Network failures don't throw.</summary>
    public class Result<T>
    {
        public bool Ok;
        public T Value;
        /// <summary>Message from the server (or a generic one when offline).</summary>
        public string Error;
        /// <summary>Error code from the server (GoTrue "error_code" or PostgREST "code").</summary>
        public string Code;
        /// <summary>True when the server was not reached (no connection, timeout).</summary>
        public bool Offline;
        public long Status;

        public static Result<T> Success(T value) => new Result<T> { Ok = true, Value = value };

        /// <summary>The same failure, typed for another value.</summary>
        public Result<TOther> As<TOther>() => new Result<TOther> { Ok = false, Error = Error, Code = Code, Offline = Offline, Status = Status };
    }

    /// <summary>
    /// A small Supabase client over REST (Auth, PostgREST, Edge Functions): only what Margin uses.
    /// It adds the publishable key and the user's token to every call, refreshes the token before it
    /// expires and retries once if the server says it expired.
    /// </summary>
    public class SupabaseClient
    {
        readonly BackendConfig _cfg;
        Task<bool> _refreshing;

        public Session Session { get; private set; }
        public bool SignedIn => Session != null;

        /// <summary>Raised when the server rejects the saved session (e.g. the refresh token was revoked).</summary>
        public event Action SessionLost;

        public SupabaseClient(BackendConfig cfg)
        {
            _cfg = cfg;
            Session = Session.Load();
        }

        // ---------- Auth ----------

        public Task<Result<Session>> SignUp(string email, string password, JObject metadata) =>
            AuthCall("signup", new JObject { ["email"] = email, ["password"] = password, ["data"] = metadata });

        /// <summary>Guest mode: an anonymous user (only works if the project allows anonymous sign-ins).</summary>
        public Task<Result<Session>> SignUpAnonymously(JObject metadata) =>
            AuthCall("signup", new JObject { ["data"] = metadata });

        public Task<Result<Session>> SignIn(string email, string password) =>
            AuthCall("token?grant_type=password", new JObject { ["email"] = email, ["password"] = password });

        /// <summary>Sends the password recovery email (the web's resetPasswordForEmail).</summary>
        public async Task<Result<bool>> Recover(string email)
        {
            var body = new JObject { ["email"] = email };
            var res = await Http.Send("POST", _cfg.AuthUrl("recover"), body.ToString(Newtonsoft.Json.Formatting.None), AnonHeaders(), _cfg.requestTimeoutSeconds);
            return res.Ok ? Result<bool>.Success(true) : Fail<bool>(res);
        }

        /// <summary>True if the email and password are right. The session in use is not replaced.</summary>
        public async Task<Result<bool>> CheckPassword(string email, string password)
        {
            var body = new JObject { ["email"] = email, ["password"] = password };
            var res = await Http.Send("POST", _cfg.AuthUrl("token?grant_type=password"), body.ToString(Newtonsoft.Json.Formatting.None), AnonHeaders(), _cfg.requestTimeoutSeconds);
            return res.Ok ? Result<bool>.Success(true) : Fail<bool>(res);
        }

        /// <summary>Changes the signed-in user's email and/or password (used to save a guest account).</summary>
        public async Task<Result<JObject>> UpdateUser(JObject fields)
        {
            if (!await EnsureFreshToken()) return Offline<JObject>();
            var res = await Http.Send("PUT", _cfg.AuthUrl("user"), fields.ToString(Newtonsoft.Json.Formatting.None), Headers(), _cfg.requestTimeoutSeconds);
            if (!res.Ok) return Fail<JObject>(res);
            var user = NetJson.ReadRaw(res.Text) as JObject;
            if (user != null && Session != null)
            {
                Session.Email = (string)user["email"] ?? Session.Email;
                Session.IsAnonymous = (bool?)user["is_anonymous"] ?? false;
                Session.Save();
            }
            return Result<JObject>.Success(user);
        }

        /// <summary>Ends the session on the server (best effort) and forgets it here.</summary>
        public async Task SignOut()
        {
            if (Session != null)
                await Http.Send("POST", _cfg.AuthUrl("logout"), null, Headers(), _cfg.requestTimeoutSeconds);
            Forget();
        }

        public void Forget()
        {
            Session = null;
            Session.Clear();
        }

        async Task<Result<Session>> AuthCall(string path, JObject body)
        {
            var res = await Http.Send("POST", _cfg.AuthUrl(path), body.ToString(Newtonsoft.Json.Formatting.None), AnonHeaders(), _cfg.requestTimeoutSeconds);
            if (!res.Ok) return Fail<Session>(res);
            var session = Session.FromAuthResponse(NetJson.ReadRaw(res.Text) as JObject);
            if (session == null)
                return new Result<Session> { Ok = false, Code = "email_not_confirmed", Error = "Email not confirmed", Status = res.Status };
            Session = session;
            Session.Save();
            return Result<Session>.Success(session);
        }

        /// <summary>
        /// Makes sure the access token is valid for at least another minute. False only when we're
        /// offline; if the server rejects the refresh token, the session is dropped and <see cref="SessionLost"/> fires.
        /// </summary>
        public Task<bool> EnsureFreshToken(bool force = false)
        {
            if (Session == null) return Task.FromResult(true);
            if (!force && !Session.ExpiresWithin(60)) return Task.FromResult(true);
            // One refresh at a time: calls that arrive meanwhile wait for the same one.
            if (_refreshing == null || _refreshing.IsCompleted) _refreshing = RefreshNow();
            return _refreshing;
        }

        async Task<bool> RefreshNow()
        {
            var body = new JObject { ["refresh_token"] = Session.RefreshToken };
            var res = await Http.Send("POST", _cfg.AuthUrl("token?grant_type=refresh_token"), body.ToString(Newtonsoft.Json.Formatting.None), AnonHeaders(), _cfg.requestTimeoutSeconds);
            if (res.NetworkError || res.Status >= 500 || res.Status == 429) return false; // offline: keep the session and try later
            var s = res.Ok ? Session.FromAuthResponse(NetJson.ReadRaw(res.Text) as JObject) : null;
            if (s == null)
            {
                Debug.LogWarning($"[Margin] Session expired ({res.Status}). Signing out.");
                Forget();
                SessionLost?.Invoke();
                return false;
            }
            Session = s;
            Session.Save();
            return true;
        }

        // ---------- PostgREST ----------

        public async Task<Result<List<T>>> Select<T>(RestQuery q)
        {
            var res = await Rest("GET", q.ToString(), null, null);
            if (!res.Ok) return Fail<List<T>>(res);
            return Result<List<T>>.Success(NetJson.Read<List<T>>(res.Text) ?? new List<T>());
        }

        /// <summary>The first matching row, or a successful result with a null value if there is none.</summary>
        public async Task<Result<T>> SelectOne<T>(RestQuery q) where T : class
        {
            var r = await Select<T>(q.Limit(1));
            if (!r.Ok) return r.As<T>();
            return Result<T>.Success(r.Value.Count > 0 ? r.Value[0] : null);
        }

        /// <summary>Raw JSON rows (used for the private diary, which is stored as-is).</summary>
        public async Task<Result<JArray>> SelectRaw(RestQuery q)
        {
            var res = await Rest("GET", q.ToString(), null, null);
            if (!res.Ok) return Fail<JArray>(res);
            return Result<JArray>.Success(NetJson.ReadRaw(res.Text) as JArray ?? new JArray());
        }

        /// <summary>Number of matching rows (exact count, no rows downloaded).</summary>
        public async Task<Result<int>> Count(RestQuery q)
        {
            var res = await Rest("GET", q.Limit(1).ToString(), null, "count=exact");
            if (!res.Ok) return Fail<int>(res);
            // Content-Range: "0-0/12" or "*/0"
            var range = res.ContentRange ?? "";
            int slash = range.LastIndexOf('/');
            int n;
            return Result<int>.Success(slash >= 0 && int.TryParse(range.Substring(slash + 1), out n) ? n : 0);
        }

        public async Task<Result<List<T>>> Insert<T>(string table, JToken rows)
        {
            var res = await Rest("POST", table, rows.ToString(Newtonsoft.Json.Formatting.None), "return=representation");
            if (!res.Ok) return Fail<List<T>>(res);
            return Result<List<T>>.Success(NetJson.Read<List<T>>(res.Text) ?? new List<T>());
        }

        public async Task<Result<bool>> Insert(string table, JToken rows) =>
            ToBool(await Rest("POST", table, rows.ToString(Newtonsoft.Json.Formatting.None), "return=minimal"));

        /// <summary>Insert, or update the row that has the same <paramref name="onConflict"/> columns.</summary>
        public async Task<Result<bool>> Upsert(string table, JToken rows, string onConflict = null)
        {
            var path = onConflict == null ? table : RestQuery.From(table).OnConflict(onConflict).ToString();
            return ToBool(await Rest("POST", path, rows.ToString(Newtonsoft.Json.Formatting.None), "resolution=merge-duplicates,return=minimal"));
        }

        public async Task<Result<bool>> Update(RestQuery filter, JObject fields) =>
            ToBool(await Rest("PATCH", filter.ToString(), fields.ToString(Newtonsoft.Json.Formatting.None), "return=minimal"));

        public async Task<Result<bool>> Delete(RestQuery filter) =>
            ToBool(await Rest("DELETE", filter.ToString(), null, "return=minimal"));

        public async Task<Result<T>> Rpc<T>(string function, JObject args)
        {
            var res = await Rest("POST", "rpc/" + function, args.ToString(Newtonsoft.Json.Formatting.None), null);
            if (!res.Ok) return Fail<T>(res);
            return Result<T>.Success(NetJson.Read<T>(res.Text));
        }

        // ---------- Edge Functions ----------

        public async Task<Result<T>> Function<T>(string name, string query)
        {
            var url = _cfg.FunctionUrl(name) + (string.IsNullOrEmpty(query) ? "" : "?" + query);
            var res = await Http.Send("GET", url, null, AnonHeaders(), _cfg.requestTimeoutSeconds);
            if (!res.Ok) return Fail<T>(res);
            return Result<T>.Success(NetJson.Read<T>(res.Text));
        }

        // ---------- Plumbing ----------

        async Task<HttpResponse> Rest(string method, string pathAndQuery, string body, string prefer)
        {
            await EnsureFreshToken();
            var url = _cfg.RestUrl(pathAndQuery);
            var res = await Http.Send(method, url, body, Headers(prefer), _cfg.requestTimeoutSeconds);
            // Expired or rejected token: refresh once and try again.
            if (res.Status == 401 && Session != null && await EnsureFreshToken(force: true) && Session != null)
                res = await Http.Send(method, url, body, Headers(prefer), _cfg.requestTimeoutSeconds);
            return res;
        }

        Dictionary<string, string> Headers(string prefer = null)
        {
            var h = new Dictionary<string, string>
            {
                ["apikey"] = _cfg.publishableKey,
                ["Authorization"] = "Bearer " + (Session != null ? Session.AccessToken : _cfg.publishableKey),
            };
            if (prefer != null) h["Prefer"] = prefer;
            return h;
        }

        Dictionary<string, string> AnonHeaders() => new Dictionary<string, string>
        {
            ["apikey"] = _cfg.publishableKey,
            ["Authorization"] = "Bearer " + _cfg.publishableKey,
        };

        static Result<bool> ToBool(HttpResponse res) => res.Ok ? Result<bool>.Success(true) : Fail<bool>(res);

        static Result<T> Offline<T>() => new Result<T> { Ok = false, Offline = true, Error = Copy.NetFail };

        static Result<T> Fail<T>(HttpResponse res)
        {
            if (res.NetworkError) return new Result<T> { Ok = false, Offline = true, Error = Copy.NetFail, Status = res.Status };
            string code = null, msg = null;
            if (NetJson.ReadRaw(res.Text) is JObject o)
            {
                code = (string)(o["error_code"] ?? o["code"]);
                msg = (string)(o["msg"] ?? o["message"] ?? o["error_description"] ?? o["error"]);
            }
            Debug.LogWarning($"[Margin] Backend {res.Status} {code}: {msg ?? res.Error}");
            return new Result<T> { Ok = false, Status = res.Status, Code = code, Error = msg ?? res.Error ?? "Request failed" };
        }
    }
}
