using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FlyingChick
{
    // Thin UnityWebRequest wrapper for the FlyingChick-Server backend
    // (~/src/FlyingChick-Server, a separate Python/FastAPI codebase --
    // see its README for the full API surface). Coroutine-based with
    // Action<T> callbacks rather than async/await, matching this
    // codebase's existing event/callback style -- nothing else here uses
    // C# async, so this doesn't introduce a second async idiom.
    //
    // Every call reports failure through ApiResult.Success == false rather
    // than throwing. This matters more than usual here: login is optional
    // (see CLAUDE.md/AuthService), so the game must keep working fully
    // offline if the server is unreachable -- callers must never assume a
    // request succeeds.
    public class ApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = "http://localhost:8000";
        [SerializeField] private float timeoutSeconds = 10f;

        // Since this whole project assembles itself at runtime with no
        // prefabs (see GameBootstrapper), a runtime-added component's
        // [SerializeField] default is all it would ever get without this --
        // GameBootstrapper exposes its own apiBaseUrl field (Inspector-
        // editable, since IT is the one thing placed in the scene by hand)
        // and forwards it here.
        public void Configure(string serverBaseUrl)
        {
            if (!string.IsNullOrEmpty(serverBaseUrl)) baseUrl = serverBaseUrl;
        }

        // Set by AuthService once logged in; sent as a Bearer header on
        // every subsequent request. Left null/empty for anonymous calls
        // (signup, login, public rankings).
        public string AuthToken { get; set; }

        public struct ApiResult<T>
        {
            public bool Success;
            public T Data;
            public long StatusCode;
            public string Error; // server's {"detail": "..."} message when available, else a network-level error
        }

        // FastAPI's error body shape depends on WHERE the error came from:
        // our own `raise HTTPException(status_code=..., detail="...")` calls
        // (401/409) send a plain string, but Pydantic's automatic request
        // validation (422 -- e.g. password too short) sends a LIST of
        // {"msg": "...", ...} objects instead. JsonUtility can't deserialize
        // one field as "sometimes a string, sometimes an array", so these
        // are two separate DTOs -- TryExtractServerErrorDetail picks which
        // one to try based on the status code.
        [Serializable]
        private class ErrorDetail
        {
            public string detail;
        }

        [Serializable]
        private class ValidationErrorItem
        {
            public string msg;
        }

        [Serializable]
        private class ValidationErrorDetail
        {
            public ValidationErrorItem[] detail;
        }

        public void Get<T>(string path, Action<ApiResult<T>> callback)
        {
            StartCoroutine(SendRequest(UnityWebRequest.Get(baseUrl + path), callback));
        }

        public void Post<T>(string path, Action<ApiResult<T>> callback) => Post(path, null, callback);

        public void Post<T>(string path, object body, Action<ApiResult<T>> callback)
        {
            StartCoroutine(SendRequest(BuildJsonRequest(path, "POST", body), callback));
        }

        public void Put<T>(string path, object body, Action<ApiResult<T>> callback)
        {
            StartCoroutine(SendRequest(BuildJsonRequest(path, "PUT", body), callback));
        }

        private UnityWebRequest BuildJsonRequest(string path, string method, object body)
        {
            string json = body != null ? JsonUtility.ToJson(body) : "{}";
            var request = new UnityWebRequest(baseUrl + path, method);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private IEnumerator SendRequest<T>(UnityWebRequest request, Action<ApiResult<T>> callback)
        {
            request.timeout = Mathf.CeilToInt(timeoutSeconds);
            if (!string.IsNullOrEmpty(AuthToken))
                request.SetRequestHeader("Authorization", "Bearer " + AuthToken);

            yield return request.SendWebRequest();

            var result = new ApiResult<T> { StatusCode = request.responseCode };

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.Success = false;
                result.Error = TryExtractServerErrorDetail(request) ?? request.error;
                callback?.Invoke(result);
                request.Dispose();
                yield break;
            }

            try
            {
                result.Data = JsonUtility.FromJson<T>(request.downloadHandler.text);
                result.Success = true;
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Error = "Failed to parse server response: " + e.Message;
            }

            callback?.Invoke(result);
            request.Dispose();
        }

        // FastAPI error responses look like {"detail": "..."} -- surface
        // that human-readable message when present (e.g. "login_id already
        // taken") instead of just UnityWebRequest's generic "HTTP/1.1 409
        // Conflict". 422 is special-cased to the validation-error array shape
        // (see ValidationErrorDetail comment) -- without this, a plain
        // "password too short" signup attempt fell through to a useless
        // generic error instead of telling the player what was actually
        // wrong, which read as "the server is broken" rather than "fix your
        // input" (실제 원인: JsonUtility가 배열을 string 필드로 못 읽어서
        // 조용히 실패 -> request.error로 폴백).
        private static string TryExtractServerErrorDetail(UnityWebRequest request)
        {
            string body = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(body)) return null;

            if (request.responseCode == 422)
            {
                try
                {
                    var parsed = JsonUtility.FromJson<ValidationErrorDetail>(body);
                    if (parsed?.detail == null || parsed.detail.Length == 0) return null;
                    var messages = new string[parsed.detail.Length];
                    for (int i = 0; i < parsed.detail.Length; i++) messages[i] = parsed.detail[i].msg;
                    return string.Join(" / ", messages);
                }
                catch
                {
                    return null;
                }
            }

            try
            {
                var parsed = JsonUtility.FromJson<ErrorDetail>(body);
                return parsed?.detail;
            }
            catch
            {
                return null;
            }
        }
    }
}
