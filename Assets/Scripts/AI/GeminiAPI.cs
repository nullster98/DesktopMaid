using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

/// <summary>
/// Google Gemini API와의 직접적인 통신을 담당하는 static 클라이언트 클래스.
/// HTTP 요청 생성, JSON 직렬화/역직렬화, 응답 파싱 등 저수준 API 호출을 처리합니다.
/// </summary>
public static partial class GeminiAPI
{
    private const string visionEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    #region API Data Serialization Classes

    [System.Serializable]
    public class SafetySetting
    {
        [JsonProperty("category")] public string category;
        [JsonProperty("threshold")] public string threshold;
    }

    [System.Serializable]
    public class Part
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string text;

        [JsonProperty("inlineData", NullValueHandling = NullValueHandling.Ignore)]
        public InlineData inlineData;
    }

    [System.Serializable]
    public class InlineData
    {
        [JsonProperty("mimeType")] public string mimeType = "image/png";
        [JsonProperty("data")] public string data;
    }

    [System.Serializable]
    public class Content
    {
        [JsonProperty("role")] public string role;
        [JsonProperty("parts")] public List<Part> parts;
    }

    [System.Serializable]
    public class RequestBody
    {
        [JsonProperty("contents")] public List<Content> contents;
        [JsonProperty("safetySettings")] public List<SafetySetting> safetySettings;
    }

    [System.Serializable]
    public class GeminiResponse
    {
        [JsonProperty("candidates")] public List<Candidate> candidates;
        [JsonProperty("promptFeedback")] public PromptFeedback promptFeedback;
    }
    
    [System.Serializable]
    public class Candidate
    {
        [JsonProperty("content")] public Content content;
    }
    
    [System.Serializable]
    public class PromptFeedback 
    {
        [JsonProperty("safetyRatings")] public List<SafetyRating> safetyRatings; 
    }

    [System.Serializable]
    public class SafetyRating 
    {
        [JsonProperty("category")] public string category;
        [JsonProperty("probability")] public string probability; 
    }

    [System.Serializable]
    public class GeminiErrorResponse 
    {
        [JsonProperty("error")] public ErrorDetails error; 
    }

    [System.Serializable]
    public class ErrorDetails 
    {
        [JsonProperty("code")] public int code;
        [JsonProperty("message")] public string message;
        [JsonProperty("status")] public string status; 
    }
    
    #endregion

    #region Coroutine-based API Calls

    /// <summary>
    /// (코루틴) 텍스트 프롬프트를 API에 전송합니다.
    /// </summary>
    public static IEnumerator SendTextPrompt(
        string prompt,
        string apiKey,
        System.Action<string> onSuccess,
        System.Action<string> onError = null)
    {
        var textPart = new Part { text = prompt };
        var content = new Content { role = "user", parts = new List<Part> { textPart } };
        var requestBody = new RequestBody { contents = new List<Content> { content }, safetySettings = GetDefaultSafetySettings() };
        
        yield return SendRequestCoroutine(requestBody, apiKey, 
            (aiText, rawJson) => onSuccess?.Invoke(aiText), 
            onError);
    }

    /// <summary>
    /// (코루틴) 텍스트 프롬프트와 이미지를 API에 전송합니다.
    /// </summary>
    public static IEnumerator SendImagePrompt(
        string prompt,
        string base64Image,
        string apiKey,
        System.Action<string> onSuccess,
        System.Action<string> onError = null)
    {
        var textPart = new Part { text = prompt };
        var imagePart = new Part { inlineData = new InlineData { data = base64Image } };
        var content = new Content { role = "user", parts = new List<Part> { textPart, imagePart } };
        var requestBody = new RequestBody { contents = new List<Content> { content }, safetySettings = GetDefaultSafetySettings() };

        yield return SendRequestCoroutine(requestBody, apiKey,
            (aiText, rawJson) => onSuccess?.Invoke(aiText),
            onError);
    }
    
    /// <summary>
    /// (코루틴) 사전에 구성된 복잡한 요청 본문을 직접 전송하는 범용 함수.
    /// </summary>
    public static IEnumerator SendComplexPrompt(
        RequestBody requestBody,
        string apiKey,
        System.Action<string, string> onSuccess,
        System.Action<string> onError = null)
    {
        yield return SendRequestCoroutine(requestBody, apiKey, onSuccess, onError);
    }

    /// <summary>
    /// 실제 HTTP 요청을 보내는 핵심 코루틴.
    /// </summary>
    private static IEnumerator SendRequestCoroutine(RequestBody requestBody, string apiKey, System.Action<string, string> onSuccess, System.Action<string> onError)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[GeminiAPI] API 키가 비어있어 요청을 중단합니다.");
            onError?.Invoke("API Key is not set.");
            yield break;
        }
        
        string json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        string url = $"{visionEndpoint}?key={apiKey}";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();
            string responseJson = req.downloadHandler.text;

            if (req.result == UnityWebRequest.Result.Success)
            {
                string aiText = ParseGeminiMessage(responseJson);
                onSuccess?.Invoke(aiText, responseJson);
            }
            else
            {
                Debug.LogWarning($"[GeminiAPI] 요청 실패: {req.error}\n응답 본문: {responseJson}");
                onError?.Invoke(ParseErrorMessage(responseJson));
            }
        }
    }

    #endregion

    #region UniTask-based API Calls

    /// <summary>
    /// (UniTask) 텍스트 프롬프트를 비동기적으로 전송하고 응답을 받습니다.
    /// </summary>
    public static async UniTask<string> AskAsync(
        string apiKey,
        string prompt,
        System.Action<string> onToken = null, // 스트리밍 미지원으로 현재 사용 안 함
        System.Threading.CancellationToken ct = default)
    {
        var textPart = new Part { text = prompt };
        var content = new Content { role = "user", parts = new List<Part> { textPart } };
        var requestBody = new RequestBody { contents = new List<Content> { content }, safetySettings = GetDefaultSafetySettings() };

        return await SendRequestAsync(requestBody, apiKey, ct);
    }
    
    /// <summary>
    /// (UniTask) 텍스트 프롬프트와 이미지를 비동기적으로 전송하고 응답을 받습니다.
    /// </summary>
    public static async UniTask<string> AskWithImageAsync(
        string apiKey,
        string prompt,
        string base64Image,
        System.Threading.CancellationToken ct = default)
    {
        var textPart = new Part { text = prompt };
        var imagePart = new Part { inlineData = new InlineData { data = base64Image } };
        var content = new Content { role = "user", parts = new List<Part> { textPart, imagePart } };
        var requestBody = new RequestBody { contents = new List<Content> { content }, safetySettings = GetDefaultSafetySettings() };

        return await SendRequestAsync(requestBody, apiKey, ct);
    }

    /// <summary>
    /// 실제 HTTP 요청을 보내는 핵심 비동기 함수.
    /// </summary>
    private static async UniTask<string> SendRequestAsync(RequestBody requestBody, string apiKey, System.Threading.CancellationToken ct)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[GeminiAPI] API 키가 비어있어 요청을 중단합니다.");
            return "(API Key is not set)";
        }

        string json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        string url = $"{visionEndpoint}?key={apiKey}";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    return ParseGeminiMessage(req.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning($"[GeminiAPI] 요청 실패: {req.error}\n응답 본문: {req.downloadHandler.text}");
                    return $"(오류: {ParseErrorMessage(req.downloadHandler.text)})";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GeminiAPI] 요청 중 예외 발생: {ex.Message}");
                return "(네트워크 요청 실패)";
            }
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gemini API 응답 JSON에서 실제 메시지 텍스트만 추출합니다.
    /// </summary>
    public static string ParseGeminiMessage(string rawJson)
    {
        try
        {
            var res = JsonConvert.DeserializeObject<GeminiResponse>(rawJson);

            // 성공적인 응답에서 텍스트 추출
            if (res?.candidates != null && res.candidates.Any() &&
                res.candidates[0].content?.parts != null && res.candidates[0].content.parts.Any() &&
                !string.IsNullOrEmpty(res.candidates[0].content.parts[0].text))
            {
                return res.candidates[0].content.parts[0].text;
            }
            
            // 안전 설정에 의해 차단된 경우
            if (res?.promptFeedback?.safetyRatings != null && res.promptFeedback.safetyRatings.Any(r => r.probability != "NEGLIGIBLE"))
            {
                var blockedCategory = res.promptFeedback.safetyRatings.First(r => r.probability != "NEGLIGIBLE").category;
                return $"(메시지가 안전 등급({blockedCategory})에 의해 차단되었습니다.)";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GeminiAPI] 응답 JSON 파싱 실패: {e.Message}\n원본 JSON: {rawJson}");
        }
        return "(응답 파싱 실패)";
    }
    
    /// <summary>
    /// 에러 응답 JSON에서 사용자에게 보여줄 메시지를 추출합니다.
    /// </summary>
    private static string ParseErrorMessage(string errorJson)
    {
        try
        {
            var errorRes = JsonConvert.DeserializeObject<GeminiErrorResponse>(errorJson);
            if (!string.IsNullOrEmpty(errorRes?.error?.message))
            {
                return errorRes.error.message;
            }
        }
        catch { /* 파싱 실패 시 원본 텍스트 반환 */ }
        return errorJson;
    }

    /// <summary>
    /// 기본 안전 설정을 반환하는 헬퍼 함수.
    /// </summary>
    private static List<SafetySetting> GetDefaultSafetySettings()
    {
        return new List<SafetySetting>
        {
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        };
    }

    #endregion
}