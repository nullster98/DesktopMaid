// --- START OF FILE OllamaClient.cs (최종 수정본) ---

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace AI
{
    [Serializable]
    public class OllamaMessage
    {
        public string role;
        public string content;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> images;
    }

    public static class OllamaClient
    {
        const string ENDPOINT = "http://localhost:11434/api/chat";
        const string BASE_ENDPOINT = "http://localhost:11434/";

        [Serializable]
        class Req { public string model; public bool stream; public List<OllamaMessage> messages; }
        [Serializable]
        class Res { public OllamaMessage message; }
        
        /// <summary>
        /// Ollama 서버가 실행 중인지 확인합니다.
        /// </summary>
        /// <returns>연결 성공 시 true, 실패 시 false</returns>
        public static async UniTask<bool> CheckConnectionAsync(CancellationToken ct = default)
        {
            // 타임아웃을 설정하기 위해 CancellationTokenSource를 사용합니다.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            using var req = new UnityWebRequest(BASE_ENDPOINT, "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            
            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: linkedCts.Token);

                // 성공적으로 응답을 받으면 (에러가 아니면) 서버가 켜져 있는 것으로 간주
                return req.result == UnityWebRequest.Result.Success;
            }
            catch (Exception ex)
            {
                // 타임아웃 또는 연결 거부 등 예외 발생 시
                Debug.LogWarning($"Ollama Connection Error: {ex.Message}");
                return false;
            }
        }

        public static async UniTask<string> AskAsync(
            string model,
            List<OllamaMessage> messages,
            CancellationToken ct = default)
        {
            var reqData = new Req
            {
                model = model,
                stream = false,
                messages = messages
            };
            
            if (messages == null || messages.Count == 0)
            {
                reqData.messages = new List<OllamaMessage> { new OllamaMessage { role = "user", content = "ping" } };
            }

            var reqJson = JsonConvert.SerializeObject(reqData);

            using var req = new UnityWebRequest(ENDPOINT, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reqJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // Ollama 서버가 모델을 못찾았을 때 반환하는 특정 에러 메시지를 확인합니다.
                    if (req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text) &&
                        req.downloadHandler.text.ToLower().Contains("model") && req.downloadHandler.text.ToLower().Contains("not found"))
                    {
                        return "Model Not Found"; // 호출 측에서 확인할 수 있는 특정 에러 문자열
                    }
                    throw new Exception($"[Ollama] HTTP {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                }

                var res = JsonConvert.DeserializeObject<Res>(req.downloadHandler.text);
                return res?.message?.content ?? "[Ollama Response Parsing Error]";
            }
            catch (Exception ex)
            {
                //Debug.LogError($"Ollama 요청 실패: {ex.Message}");
                // 사용자에게 보여줄 에러 메시지를 반환할 수도 있습니다.
                return "Ollama Connection Error";
            }
        }
    }
}