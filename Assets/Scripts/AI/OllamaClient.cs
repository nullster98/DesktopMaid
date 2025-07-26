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
    /// <summary>
    /// 로컬 Ollama 서버와의 통신을 담당하는 정적 클라이언트 클래스.
    /// </summary>
    public static class OllamaClient
    {
        private const string CHAT_ENDPOINT = "http://localhost:11434/api/chat";
        private const string BASE_ENDPOINT = "http://localhost:11434/";

        #region Internal Data Structures
        
        [Serializable]
        private class OllamaRequest
        {
            public string model;
            public bool stream;
            public List<OllamaMessage> messages;
        }

        [Serializable]
        private class OllamaResponse
        {
            public OllamaMessage message;
            // 스트리밍 미사용 시 다른 필드들은 무시
        }
        
        #endregion

        /// <summary>
        /// Ollama 서버가 현재 실행 중이고 응답하는지 비동기적으로 확인합니다.
        /// </summary>
        /// <param name="timeoutSeconds">연결 시도 제한 시간 (초)</param>
        /// <returns>서버에 연결 가능하면 true, 아니면 false를 반환합니다.</returns>
        public static async UniTask<bool> CheckConnectionAsync(int timeoutSeconds = 3)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var req = UnityWebRequest.Get(BASE_ENDPOINT);
            
            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: timeoutCts.Token);
                // 성공적인 HTTP 상태 코드를 받으면 연결된 것으로 간주 (e.g., 200 OK)
                return req.result == UnityWebRequest.Result.Success;
            }
            catch (Exception ex)
            {
                // OperationCanceledException (타임아웃) 또는 연결 거부 등
                Debug.LogWarning($"[OllamaClient] 서버 연결 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 구조화된 대화 기록을 Ollama 서버에 보내고 AI의 응답을 비동기적으로 받습니다.
        /// </summary>
        /// <param name="model">사용할 Ollama 모델 이름 (예: "llama3")</param>
        /// <param name="messages">전송할 대화 메시지 리스트</param>
        /// <param name="ct">비동기 작업을 취소하기 위한 CancellationToken</param>
        /// <returns>AI의 응답 메시지 문자열. 실패 시 에러 메시지를 반환합니다.</returns>
        public static async UniTask<string> AskAsync(
            string model,
            List<OllamaMessage> messages,
            CancellationToken ct = default)
        {
            var requestData = new OllamaRequest
            {
                model = model,
                stream = false, // 스트리밍 응답은 사용하지 않음
                messages = messages
            };

            var requestJson = JsonConvert.SerializeObject(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

            using var req = new UnityWebRequest(CHAT_ENDPOINT, "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // HTTP 프로토콜 수준의 에러 (e.g., 404 Not Found, 500 Server Error)
                    throw new Exception($"HTTP Error {req.responseCode}: {req.error}\nResponse: {req.downloadHandler.text}");
                }

                var response = JsonConvert.DeserializeObject<OllamaResponse>(req.downloadHandler.text);
                return response?.message?.content ?? "[Ollama 응답 파싱 오류]";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OllamaClient] 요청 실패: {ex.Message}");
                // 사용자에게 보여줄 수 있는 안전한 대체 메시지 반환
                return "죄송해요, 지금은 답변을 생성할 수 없어요. (Ollama 연결 오류)";
            }
        }
    }

    /// <summary>
    /// Ollama API와 통신하기 위한 메시지 데이터 구조.
    /// </summary>
    [Serializable]
    public class OllamaMessage
    {
        public string role; // "system", "user", "assistant"
        public string content;
        
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> images; // 이미지 base64 문자열 리스트
    }
}