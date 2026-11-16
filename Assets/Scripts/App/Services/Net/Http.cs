using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Margin.App.Services.Net
{
    /// <summary>What came back from one HTTP call. Never throws: a failed call is a value too.</summary>
    public class HttpResponse
    {
        /// <summary>HTTP status (0 when the request never reached the server).</summary>
        public long Status;
        public string Text;
        /// <summary>True when there was no answer at all (offline, timeout, DNS…).</summary>
        public bool NetworkError;
        public string Error;
        /// <summary>The Content-Range header (PostgREST puts row counts there).</summary>
        public string ContentRange;

        public bool Ok => !NetworkError && Status >= 200 && Status < 300;
    }

    /// <summary>
    /// The only place that talks to UnityWebRequest. Wraps it in a Task so the rest of the
    /// code can use async/await; continuations run on Unity's main thread (UnitySynchronizationContext).
    /// </summary>
    public static class Http
    {
        public static Task<HttpResponse> Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds)
        {
            var tcs = new TaskCompletionSource<HttpResponse>();
            UnityWebRequest req;
            try
            {
                req = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = timeoutSeconds };
                if (jsonBody != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (headers != null)
                    foreach (var h in headers) req.SetRequestHeader(h.Key, h.Value);
            }
            catch (System.Exception e)
            {
                tcs.SetResult(new HttpResponse { NetworkError = true, Error = e.Message });
                return tcs.Task;
            }

            // "completed" is raised on the main thread; if the operation is already done when we
            // subscribe, Unity calls the handler right away, so no completion is ever missed.
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                var res = new HttpResponse
                {
                    Status = req.responseCode,
                    Text = req.downloadHandler != null ? req.downloadHandler.text : null,
                    NetworkError = req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError,
                    Error = req.error,
                    ContentRange = req.GetResponseHeader("Content-Range"),
                };
                req.Dispose();
                tcs.TrySetResult(res);
            };
            return tcs.Task;
        }
    }
}
