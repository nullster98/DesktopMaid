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
    // 다른 스크립트에서 참조할 수 있도록 public으로 변경
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

        [Serializable]
        class Req { public string model; public bool stream; public List<OllamaMessage> messages; }
        [Serializable]
        class Res { public OllamaMessage message; }

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

            var reqJson = JsonConvert.SerializeObject(reqData);

            using var req = new UnityWebRequest(ENDPOINT, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reqJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"[Ollama] HTTP {req.responseCode}: {req.error}\n{req.downloadHandler.text}");

                var res = JsonConvert.DeserializeObject<Res>(req.downloadHandler.text);
                return res?.message?.content ?? "[Ollama 응답 파싱 오류]";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ollama 요청 실패: {ex.Message}");
                // 사용자에게 보여줄 에러 메시지를 반환할 수도 있습니다.
                return "죄송해요, 지금은 답변을 생성할 수 없어요. (Ollama 연결 오류)";
            }
        }
    }
}