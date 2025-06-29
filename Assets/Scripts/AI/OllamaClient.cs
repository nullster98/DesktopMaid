// Assets/Scripts/AI/OllamaClient.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AI
{
    public static class OllamaClient
    {
        const string ENDPOINT = "http://localhost:11434/api/chat";

        [Serializable] class Msg { public string role; public string content;
            public List<string> images;
        }
        [Serializable] class Req { public string model; public bool stream; public List<Msg> messages; }
        [Serializable] class Res { public Msg message; }

        public static async UniTask<string> AskAsync(
            string prompt,
            string model = "gemma3:4b",
            string base64Image = null,
            CancellationToken ct = default)
        {
            // --- 요청 데이터 구성 ---
            var message = new Msg { role = "user", content = prompt };

            // 이미지 데이터가 있으면 메시지에 추가
            if (!string.IsNullOrEmpty(base64Image))
            {
                base64Image = base64Image.Replace("\n", "").Replace("\r", "");
                message.images = new List<string> { base64Image };
            }
            
            var reqData = new Req
            {
                model = model,
                stream = false,
                messages = new List<Msg> { message } // 메시지를 리스트에 담아 전달
            };
            
            // Null인 images 필드가 JSON에 포함되지 않도록 설정
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(reqData, settings);

            using var req = new UnityWebRequest(ENDPOINT, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reqJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"[Ollama] HTTP {req.responseCode}: {req.error}");

            var res = JsonUtility.FromJson<Res>(req.downloadHandler.text);
            return res.message.content;
        }
    }
}