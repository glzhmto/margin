using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Margin.App.Services.Net;
using Margin.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Margin.App.Services
{
    /// <summary>What the account needs from the app: the diary, a way to swap it, and today's date.</summary>
    public interface IStateHost
    {
        AppState State { get; }
        /// <summary>Puts another diary in place (and saves it locally) without counting it as a change.</summary>
        void ReplaceState(AppState state);
        DateTime Today { get; }
    }

    /// <summary>Result of signing up, logging in or entering as a guest (the web's { ok, error, confirm, disabled }).</summary>
    public class AuthOutcome
    {
        public string Error;
        /// <summary>The account was created but the email must be confirmed before logging in.</summary>
        public bool Confirm;
        /// <summary>Guest mode is switched off on the server.</summary>
        public bool Disabled;
        public bool Ok => Error == null && !Confirm;
    }

    /// <summary>
    /// The signed-in account (same Supabase backend as the web) and keeping the diary in sync.
    /// <para>
    /// Offline first: every change is saved on the phone right away (StateStore). This service then
    /// copies it to <c>reading_states</c> (state and settings), updates the public summary in
    /// <c>profiles</c> and sends the activity rows. If there is no connection, it remembers it has
    /// unsent changes and tries again.
    /// </para>
    /// <para>
    /// Three copies of the diary JSON: <c>_remote</c> is what the server has; <c>_local</c> is the
    /// working copy with the web-only fields (<see cref="Extras"/>); AppState is the diary itself.
    /// What is sent is AppState merged over <c>_local</c>, so fields only the web knows are kept.
    /// </para>
    /// <para>
    /// Conflicts: last writer wins. The phone pulls the server copy on launch and when it comes back
    /// to the foreground, unless it has unsent changes of its own (then those are sent first).
    /// </para>
    /// </summary>
    public class AccountService
    {
        const string ProfileKey = "margin.account.profile";
        const string OwnerKey = "margin.account.stateOwner";
        const string DirtyKey = "margin.account.dirty";
        const string OutboxKey = "margin.account.outbox";
        const string SettingsKey = "margin.account.settings";
        const string SettingsDirtyKey = "margin.account.settingsDirty";
        const string RemoteFile = "margin-remote-state.json";
        const string LocalFile = "margin-local-state.json";

        public const string ProfileColumns = "id,username,name,bio,university,color,is_creator,is_guest,current_book_id,current_page,momentum_level,pages_this_week,books_read,favorites,created_at";

        readonly SupabaseClient _sb;
        readonly IStateHost _host;
        readonly string _remotePath;
        readonly string _localPath;

        JObject _remote;          // last diary JSON we know the server has
        JObject _local;           // working copy: _remote plus local changes to the web-only fields
        ActivitySnapshot _seen;   // what activity has already been reported
        List<Activity> _outbox;   // activity rows waiting for a connection
        bool _syncing;

        /// <summary>My public profile (cached, so Settings works offline).</summary>
        public Profile Me { get; private set; }
        public bool SignedIn => _sb.SignedIn;
        public string UserId => _sb.Session?.UserId;
        public string Email => _sb.Session?.Email ?? "";
        public bool IsGuest => SignedIn && ((Me != null && Me.is_guest) || (_sb.Session.IsAnonymous && string.IsNullOrEmpty(Email)));
        public bool IsCreator => Me != null && Me.is_creator;
        /// <summary>A guest saved with an email that still needs the confirmation link (the web's acc.pendingEmail).</summary>
        public string PendingEmail { get; private set; }

        /// <summary>The web-only fields of the diary (lists, want, dnf, yearGoal...). Change them, then save.</summary>
        public WebExtras Extras { get; private set; } = new WebExtras();
        /// <summary>reading_states.settings (privacy, theme, focus length...).</summary>
        public WebSettings Settings { get; private set; }

        public bool HasUnsentChanges => PlayerPrefs.GetInt(DirtyKey, 0) == 1 || PlayerPrefs.GetInt(SettingsDirtyKey, 0) == 1 || _outbox.Count > 0;

        /// <summary>"Show what I'm reading" (on by default).</summary>
        public bool ShowsCurrentBook => Settings == null || Settings.ShowCurrent;
        /// <summary>"Activity visible to: Only me" turns off activity rows.</summary>
        public bool SharesActivity => Settings == null || Settings.ActivityVisibility != "Only me";

        /// <summary>Raised when the profile or the diary changed because of the server.</summary>
        public event Action Changed;
        /// <summary>Raised when a setting changes (the app applies theme, text size and motion).</summary>
        public event Action SettingsChanged;

        public AccountService(SupabaseClient sb, IStateHost host)
        {
            _sb = sb;
            _host = host;
            _remotePath = Path.Combine(Application.persistentDataPath, RemoteFile);
            _localPath = Path.Combine(Application.persistentDataPath, LocalFile);
            Me = NetJson.Read<Profile>(PlayerPrefs.GetString(ProfileKey, ""));
            UseSettings(NetJson.ReadRaw(PlayerPrefs.GetString(SettingsKey, "")) as JObject);
            _outbox = NetJson.Read<List<Activity>>(PlayerPrefs.GetString(OutboxKey, "")) ?? new List<Activity>();
            _remote = ReadJson(_remotePath);
            _local = ReadJson(_localPath) ?? (JObject)_remote?.DeepClone();
            Extras = WebExtras.From(_local);
        }

        // ---------- Signing in ----------

        public async Task<AuthOutcome> SignUp(string name, string email, string username, string password, string university)
        {
            var free = await UsernameAvailable(username);
            if (free == false) return new AuthOutcome { Error = Copy.UsernameTaken(username) };
            if (free == null) return new AuthOutcome { Error = Copy.UsernameCheckFailed };
            var meta = new JObject
            {
                ["username"] = username,
                ["name"] = name.Trim(),
                ["university"] = (university ?? "").Trim(),
                ["color"] = AccountRules.ColorFor(username),
            };
            var r = await _sb.SignUp(email.Trim(), password, meta);
            // No session in the answer: the project asks to confirm the email first.
            if (!r.Ok && r.Code == "email_not_confirmed" && r.Status < 300) return new AuthOutcome { Confirm = true };
            if (!r.Ok) return new AuthOutcome { Error = Explain(r) };
            return new AuthOutcome { Error = await AdoptAccount() };
        }

        public async Task<AuthOutcome> LogIn(string email, string password)
        {
            var r = await _sb.SignIn(email.Trim(), password);
            if (!r.Ok) return new AuthOutcome { Error = Explain(r) };
            return new AuthOutcome { Error = await AdoptAccount() };
        }

        /// <summary>Guest mode: a real (anonymous) account with a generated @username, saved later if they like it.</summary>
        public async Task<AuthOutcome> ContinueAsGuest()
        {
            var r = await _sb.SignUpAnonymously(new JObject { ["name"] = "Guest" });
            if (!r.Ok)
            {
                if ((r.Error ?? "").IndexOf("anonymous", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new AuthOutcome { Error = "Guest mode is switched off right now. Sign up with email instead, it takes a minute.", Disabled = true };
                return new AuthOutcome { Error = Explain(r) };
            }
            var flag = await _sb.Update(MyRow(), new JObject { ["is_guest"] = true });
            if (!flag.Ok) Debug.LogWarning("[Margin] Could not mark the guest profile: " + flag.Error);
            return new AuthOutcome { Error = await AdoptAccount() };
        }

        /// <summary>
        /// Turns the guest into a normal account: email + password on the same user, then the public profile.
        /// Returns (error, pendingEmail): pendingEmail is set when the email still needs its confirmation link.
        /// </summary>
        public async Task<(string error, string pending)> SaveGuest(string name, string email, string username, string password)
        {
            var free = await UsernameAvailable(username);
            if (free == false) return (Copy.UsernameTaken(username), null);
            if (free == null) return (Copy.UsernameCheckFailed, null);

            bool pending = false;
            var r = await _sb.UpdateUser(new JObject { ["email"] = email.Trim(), ["password"] = password });
            if (!r.Ok && !r.Offline && !IsAlreadyRegistered(r) && r.Status != 429)
            {
                // Some projects only accept the password once the email is on the account: two steps.
                r = await _sb.UpdateUser(new JObject { ["email"] = email.Trim() });
                if (r.Ok)
                {
                    var r2 = await _sb.UpdateUser(new JObject { ["password"] = password });
                    if (!r2.Ok) pending = true;
                }
            }
            if (!r.Ok) return (Explain(r), null);
            var user = r.Value;
            if (user != null && string.IsNullOrEmpty((string)user["email"]) && !string.IsNullOrEmpty((string)user["new_email"])) pending = true;

            var fields = new JObject { ["username"] = username, ["name"] = name.Trim(), ["is_guest"] = pending, ["updated_at"] = Now() };
            var p = await _sb.Update(MyRow(), fields);
            if (!p.Ok) return (IsDuplicate(p) ? Copy.UsernameTaken(username) : Copy.GuestProfileNotUpdated, null);

            if (!await LoadProfile() && Me != null)
            {
                Me.username = username;
                Me.name = name.Trim();
                Me.is_guest = pending;
                CacheMe();
            }
            PendingEmail = pending ? email.Trim() : null;
            _host.State.profile.name = FirstWord(name);
            OnStateSaved();
            Changed?.Invoke();
            return (null, pending ? email.Trim() : null);
        }

        /// <summary>Sends what's pending, ends the session and forgets this account's diary on the phone.</summary>
        public async Task SignOut()
        {
            await Sync();
            await _sb.SignOut();
            ForgetLocalAccount();
        }

        /// <summary>Called when the server rejected the session: same as signing out, without the server call.</summary>
        public void ForgetLocalAccount()
        {
            _sb.Forget();
            Me = null;
            _remote = null;
            _local = null;
            Extras = new WebExtras();
            UseSettings(null);
            _seen = null;
            PendingEmail = null;
            _outbox.Clear();
            foreach (var k in new[] { ProfileKey, OwnerKey, DirtyKey, OutboxKey, SettingsKey, SettingsDirtyKey }) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
            TryDelete(_remotePath);
            TryDelete(_localPath);
            _host.ReplaceState(new AppState());
        }

        /// <summary>
        /// True if nobody has the username, false if it's taken, null if it couldn't be checked.
        /// Signed in, my own profile doesn't count (so I can keep my name).
        /// </summary>
        public async Task<bool?> UsernameAvailable(string username)
        {
            if (SignedIn)
            {
                var r = await _sb.Select<Profile>(RestQuery.From("profiles").Select("id").Eq("username", username).Neq("id", UserId).Limit(1));
                return r.Ok ? r.Value.Count == 0 : (bool?)null;
            }
            var rpc = await _sb.Rpc<bool?>("username_available", new JObject { ["u"] = username });
            return rpc.Ok ? rpc.Value : null;
        }

        // ---------- Password and email (net.js) ----------

        /// <summary>Sends the "set a new password" email. Null if it went out.</summary>
        public async Task<string> ResetPassword(string email)
        {
            var r = await _sb.Recover(email.Trim());
            return r.Ok ? null : Explain(r);
        }

        /// <summary>True if the password is the account's current one (checked without touching the session).</summary>
        public async Task<bool> CheckPassword(string password)
        {
            var r = await _sb.CheckPassword(Email, password);
            return r.Ok;
        }

        public async Task<string> SetPassword(string password)
        {
            var r = await _sb.UpdateUser(new JObject { ["password"] = password });
            return r.Ok ? null : Explain(r);
        }

        /// <summary>Asks to change the email (Supabase sends a link to the new address).</summary>
        public async Task<string> SetEmail(string email)
        {
            var r = await _sb.UpdateUser(new JObject { ["email"] = email.Trim() });
            return r.Ok ? null : Explain(r);
        }

        public async Task<string> ChangeUsername(string username)
        {
            var free = await UsernameAvailable(username);
            if (free == false) return Copy.UsernameTaken(username);
            if (free == null) return Copy.UsernameCheckFailed;
            return await UpdateProfile(new JObject { ["username"] = username });
        }

        /// <summary>
        /// Deletes the account for good: sends what's pending, then the server function
        /// <c>public.delete_my_account()</c> removes the login and everything cascades (profile, diary,
        /// friends, follows, activity, posts, reviews). Then the phone forgets the session and the diary.
        /// Returns null on success, or a message.
        /// </summary>
        public async Task<string> DeleteAccount()
        {
            if (!SignedIn) return null;
            await Sync();
            var r = await _sb.Rpc<JToken>("delete_my_account", new JObject());
            if (!r.Ok) return r.Offline ? Copy.NetFail : "Couldn't delete the account: " + r.Error;
            ForgetLocalAccount();
            return null;
        }

        // ---------- Profile ----------

        public async Task<string> UpdateProfile(JObject fields)
        {
            fields["updated_at"] = Now();
            var r = await _sb.Update(MyRow(), fields);
            if (!r.Ok) return r.Offline ? Copy.NetFail : IsDuplicate(r) ? Copy.UsernameTaken((string)fields["username"] ?? "") : r.Error;
            await LoadProfile();
            Changed?.Invoke();
            return null;
        }

        public Task<string> SetCreator(bool on) => UpdateProfile(new JObject { ["is_creator"] = on });
        public Task<string> SetUniversity(string university) => UpdateProfile(new JObject { ["university"] = (university ?? "").Trim() });

        async Task<bool> LoadProfile()
        {
            var r = await _sb.SelectOne<Profile>(RestQuery.From("profiles").Select(ProfileColumns).Eq("id", UserId));
            if (!r.Ok || r.Value == null) return false;
            Me = r.Value;
            CacheMe();
            return true;
        }

        // ---------- Settings ----------

        void UseSettings(JObject saved)
        {
            Settings = new WebSettings(WebSettings.Normalize(saved), OnSettingsChanged);
        }

        void OnSettingsChanged()
        {
            PlayerPrefs.SetString(SettingsKey, Settings.ToString());
            PlayerPrefs.SetInt(SettingsDirtyKey, 1);
            PlayerPrefs.Save();
            SettingsChanged?.Invoke();
        }

        /// <summary>Takes the server's settings unless this phone has unsent changes to them.</summary>
        void AdoptSettings(JObject server)
        {
            if (PlayerPrefs.GetInt(SettingsDirtyKey, 0) == 1) return;
            UseSettings(server);
            PlayerPrefs.SetString(SettingsKey, Settings.ToString());
            SettingsChanged?.Invoke();
        }

        // ---------- Diary sync ----------

        /// <summary>
        /// Right after signing in (or on launch with a saved session): loads the profile and decides
        /// which diary wins. The server's, unless this phone has unsent changes for the same account.
        /// A brand-new account keeps what is on the phone (so a reader's progress moves into it).
        /// </summary>
        public async Task<string> AdoptAccount()
        {
            var uid = UserId;
            if (!await LoadProfile()) return Me != null && Me.id == uid ? null : Copy.NetFail; // offline launch: keep the cached profile
            var rs = await _sb.SelectRaw(RestQuery.From("reading_states").Select("state,settings").Eq("user_id", uid));
            if (!rs.Ok) return null; // the diary on the phone keeps working; we'll sync later

            var row = rs.Value.Count > 0 ? rs.Value[0] as JObject : null;
            var owner = PlayerPrefs.GetString(OwnerKey, "");
            bool mineLocally = owner == uid;
            if (!mineLocally) PlayerPrefs.SetInt(SettingsDirtyKey, 0); // another account's unsent settings never move over
            AdoptSettings(row?["settings"] as JObject);
            var remote = row?["state"] as JObject;
            var remoteDiary = RemoteStateCodec.FromRemote(remote);

            if (remoteDiary != null && !(mineLocally && HasUnsentChanges))
            {
                remoteDiary.remindersScheduled = mineLocally ? _host.State.remindersScheduled : new List<DateTime>();
                var canonical = RemoteStateCodec.Canonical(remote);
                SetRemote(canonical);
                SetLocal((JObject)canonical.DeepClone());
                _host.ReplaceState(remoteDiary);
            }
            else
            {
                if (remoteDiary == null && owner != "" && !mineLocally) _host.ReplaceState(new AppState()); // someone else's diary: never move it
                _remote = remote ?? _remote;
                if (!mineLocally || _local == null) SetLocal(remote != null ? (JObject)remote.DeepClone() : null);
                MarkDirty(true);
            }
            PlayerPrefs.SetString(OwnerKey, uid);
            PlayerPrefs.Save();
            _seen = ActivityTracker.Snapshot(_host.State);
            Changed?.Invoke();
            await Sync();
            return null;
        }

        /// <summary>
        /// Gets the server's diary if it changed elsewhere (the web) and the phone has nothing unsent.
        /// True if the diary on the phone was replaced.
        /// </summary>
        public async Task<bool> PullIfIdle()
        {
            if (!SignedIn || _syncing || HasUnsentChanges) return false;
            var rs = await _sb.SelectRaw(RestQuery.From("reading_states").Select("state,settings").Eq("user_id", UserId));
            if (!rs.Ok || rs.Value.Count == 0 || HasUnsentChanges) return false;
            var row = rs.Value[0] as JObject;
            var serverSettings = WebSettings.Normalize(row?["settings"] as JObject);
            if (!JToken.DeepEquals(serverSettings, Settings.Json)) AdoptSettings(serverSettings);
            var remote = row?["state"] as JObject;
            var diary = RemoteStateCodec.FromRemote(remote);
            var canonical = RemoteStateCodec.Canonical(remote);
            if (diary == null || RemoteStateCodec.SameDiary(canonical, _remote)) return false;
            diary.remindersScheduled = _host.State.remindersScheduled;
            SetRemote(canonical);
            SetLocal((JObject)canonical.DeepClone());
            _host.ReplaceState(diary);
            _seen = ActivityTracker.Snapshot(diary);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Writes <see cref="Extras"/> into the working JSON. Call it before saving (Data.Save does).
        /// The diary is merged in first so every progress item exists for its finishedOn and rating.
        /// </summary>
        public void CommitExtras()
        {
            var merged = RemoteStateCodec.ToRemote(_host.State, _local);
            Extras.WriteTo(merged);
            SetLocal(merged, keepExtras: true); // the same Extras object stays valid for screens that hold it
        }

        /// <summary>Called after every local save: queues activity and marks the diary as unsent if it changed.</summary>
        public void OnStateSaved()
        {
            if (!SignedIn) return;
            var s = _host.State;
            if (_seen == null) _seen = ActivityTracker.Snapshot(s);
            var rows = ActivityTracker.Diff(_seen, s);
            _seen = ActivityTracker.Snapshot(s);
            if (rows.Count > 0) Queue(rows);
            if (!RemoteStateCodec.SameDiary(RemoteStateCodec.ToRemote(s, _local), _remote)) MarkDirty(true);
        }

        /// <summary>Adds activity rows (reviews, posts…) to the outbox and tries to send them.</summary>
        public void Queue(List<Activity> rows)
        {
            if (!SignedIn || !SharesActivity) return;
            foreach (var r in rows) r.user_id = UserId;
            _outbox.AddRange(rows);
            SaveOutbox();
        }

        /// <summary>Sends the diary and settings, the public summary and the activity outbox. Safe to call often.</summary>
        public async Task Sync()
        {
            if (!SignedIn || _syncing) return;
            _syncing = true;
            try
            {
                if (PlayerPrefs.GetInt(DirtyKey, 0) == 1 || PlayerPrefs.GetInt(SettingsDirtyKey, 0) == 1) await PushDiary();
                if (_outbox.Count > 0) await PushOutbox();
            }
            finally { _syncing = false; }
        }

        async Task PushDiary()
        {
            var s = _host.State;
            var remote = RemoteStateCodec.ToRemote(s, _local);
            var sentSettings = (JObject)Settings.Json.DeepClone();
            var row = new JObject { ["user_id"] = UserId, ["state"] = remote, ["settings"] = sentSettings, ["updated_at"] = Now() };
            var saved = await _sb.Upsert("reading_states", row, "user_id");
            if (!saved.Ok) return;
            SetRemote(remote);
            // Only mark it sent if nothing changed while we were uploading.
            if (RemoteStateCodec.SameDiary(RemoteStateCodec.ToRemote(_host.State, _local), _remote)) MarkDirty(false);
            if (JToken.DeepEquals(sentSettings, Settings.Json)) { PlayerPrefs.SetInt(SettingsDirtyKey, 0); PlayerPrefs.Save(); }

            var summary = NetJson.ToObjectWithNulls(ProfileSummary.From(s, _host.Today, ShowsCurrentBook, Extras.favorites, Copy.LevelName));
            summary["updated_at"] = Now();
            var p = await _sb.Update(MyRow(), summary);
            if (p.Ok && Me != null)
            {
                Me.current_book_id = (string)summary["current_book_id"];
                Me.current_page = (int?)summary["current_page"];
                Me.momentum_level = (string)summary["momentum_level"];
                CacheMe();
            }
        }

        async Task PushOutbox()
        {
            var batch = _outbox.ToList();
            // Every row needs the same keys in a bulk insert (PostgREST), so book_id is always sent.
            var rows = new JArray(batch.Select(a => (JToken)new JObject
            {
                ["user_id"] = a.user_id,
                ["kind"] = a.kind,
                ["book_id"] = a.book_id,
                ["data"] = NetJson.ToObject(a.data ?? new ActivityData()),
            }));
            var r = await _sb.Insert("activity", rows);
            if (r.Ok || (!r.Offline && r.Status >= 400 && r.Status < 500))
            {
                // Sent, or rejected for good (a bad row must not block the queue forever).
                if (!r.Ok) Debug.LogWarning("[Margin] Activity rows dropped: " + r.Error);
                _outbox.RemoveAll(batch.Contains);
                SaveOutbox();
            }
        }

        // ---------- Helpers ----------

        RestQuery MyRow() => RestQuery.From("profiles").Eq("id", UserId);

        void SetRemote(JObject remote)
        {
            _remote = remote;
            WriteJson(_remotePath, remote);
        }

        void SetLocal(JObject local, bool keepExtras = false)
        {
            _local = local;
            if (!keepExtras) Extras = WebExtras.From(local);
            WriteJson(_localPath, local);
        }

        static void WriteJson(string path, JObject o)
        {
            try { File.WriteAllText(path, o != null ? o.ToString(Newtonsoft.Json.Formatting.None) : ""); }
            catch (Exception e) { Debug.LogWarning("[Margin] Could not cache the diary JSON: " + e.Message); }
        }

        static JObject ReadJson(string path)
        {
            try { return File.Exists(path) ? NetJson.ReadRaw(File.ReadAllText(path)) as JObject : null; }
            catch (Exception) { return null; }
        }

        void MarkDirty(bool dirty)
        {
            PlayerPrefs.SetInt(DirtyKey, dirty ? 1 : 0);
            PlayerPrefs.Save();
        }

        void SaveOutbox()
        {
            PlayerPrefs.SetString(OutboxKey, NetJson.Write(_outbox));
            PlayerPrefs.Save();
        }

        void CacheMe()
        {
            PlayerPrefs.SetString(ProfileKey, NetJson.Write(Me));
            PlayerPrefs.Save();
        }

        static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        static string FirstWord(string name)
        {
            var n = (name ?? "").Trim();
            int i = n.IndexOf(' ');
            return i > 0 ? n.Substring(0, i) : n;
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* nothing to clean */ }
        }

        static bool IsAlreadyRegistered<T>(Result<T> r) =>
            r.Code == "email_exists" || r.Code == "user_already_exists" || (r.Error ?? "").IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsDuplicate<T>(Result<T> r) =>
            r.Code == "23505" || (r.Error ?? "").IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0;

        static string Explain<T>(Result<T> r) => r.Offline ? Copy.NetFail : AccountRules.FriendlyAuthError(r.Code, r.Error);
    }
}
