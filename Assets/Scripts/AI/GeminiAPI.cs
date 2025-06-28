// --- START OF FILE GeminiAPI.cs ---

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

public static partial class GeminiAPI
{
    private const string visionEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    #region API 데이터 직렬화 클래스 (Public으로 변경)
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
        [JsonProperty("mimeType")]
        public string mimeType = "image/png";

        [JsonProperty("data")]
        public string data;
    }

    [System.Serializable]
    public class Content
    {
        [JsonProperty("role")]
        public string role;

        [JsonProperty("parts")]
        public List<Part> parts;
    }

    [System.Serializable]
    public class RequestBody
    {
        [JsonProperty("contents")]
        public List<Content> contents;

        [JsonProperty("safetySettings")]
        public List<SafetySetting> safetySettings;
    }

    [System.Serializable]
    public class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<Candidate> candidates;

        [JsonProperty("promptFeedback")]
        public PromptFeedback promptFeedback;
    }
    
    [System.Serializable]
    public class Candidate
    {
        [JsonProperty("content")]
        public Content content;
        [JsonProperty("finishReason")]
        public string finishReason;
        [JsonProperty("index")]
        public int index;
        [JsonProperty("safetyRatings")]
        public List<SafetyRating> safetyRatings;
    }
    
    [System.Serializable]
    public class PromptFeedback 
    {
        [JsonProperty("safetyRatings")]
        public List<SafetyRating> safetyRatings; 
    }

    [System.Serializable]
    public class SafetyRating 
    {
        [JsonProperty("category")]
        public string category;
        [JsonProperty("probability")]
        public string probability; 
    }

    [System.Serializable]
    public class GeminiErrorResponse 
    {
        [JsonProperty("error")]
        public ErrorDetails error; 
    }

    [System.Serializable]
    public class ErrorDetails 
    {
        [JsonProperty("code")]
        public int code;
        [JsonProperty("message")]
        public string message;
        [JsonProperty("status")]
        public string status; 
    }
    #endregion

    public static IEnumerator SendTextPrompt(
        string prompt,
        string apiKey,
        System.Action<string> onSuccess,
        System.Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("‼️ GeminiAPI.SendTextPrompt: API 키가 비어있거나 null입니다!");
            onError?.Invoke("API Key is not set.");
            yield break;
        }

        Part textPart = new Part { text = prompt };
        Content content = new Content { role = "user", parts = new List<Part> { textPart } };
        List<SafetySetting> safetySettings = new List<SafetySetting>
        {
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        };
        RequestBody bodyObj = new RequestBody { contents = new List<Content> { content }, safetySettings = safetySettings };
        
        yield return SendComplexPrompt(bodyObj, apiKey, 
            (aiText, rawJson) => onSuccess?.Invoke(aiText), 
            onError);
    }

    public static IEnumerator SendImagePrompt(
        string prompt,
        string base64Image,
        string apiKey,
        System.Action<string> onSuccess,
        System.Action<string> onError = null)
    {
        Part textPart = new Part { text = prompt };
        Part imagePart = new Part { inlineData = new InlineData { data = base64Image } };
        Content content = new Content { role = "user", parts = new List<Part> { textPart, imagePart } };
        List<SafetySetting> safetySettings = new List<SafetySetting>
        {
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        };
        RequestBody bodyObj = new RequestBody { contents = new List<Content> { content }, safetySettings = safetySettings };

        yield return SendComplexPrompt(bodyObj, apiKey,
            (aiText, rawJson) => onSuccess?.Invoke(aiText),
            onError);
    }
    
    /// <summary>
    /// 사전에 구성된 복잡한 요청 본문(RequestBody)을 직접 전송하는 범용 함수.
    /// ChatFunction에서 장/단기 기억을 포함한 컨텍스트를 보낼 때 사용됩니다.
    /// </summary>
    public static IEnumerator SendComplexPrompt(
        RequestBody requestBody,
        string apiKey,
        System.Action<string, string> onSuccess, // 성공 시 (응답 텍스트, 원본 JSON)을 전달
        System.Action<string> onError = null)
    {
        string json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        string url = $"{visionEndpoint}?key={apiKey}";

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
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
                Debug.LogWarning($"❌ Gemini 복합 호출 실패: {req.error}");
                Debug.LogWarning($"❌ 에러 응답 본문: {responseJson}");
                onError?.Invoke(responseJson);
            }
        }
    }

    public static string ParseGeminiMessage(string rawJson)
    {
        try
        {
            GeminiResponse res = JsonConvert.DeserializeObject<GeminiResponse>(rawJson);
            if (res?.candidates != null && res.candidates.Any() &&
                res.candidates[0].content?.parts != null && res.candidates[0].content.parts.Any() &&
                !string.IsNullOrEmpty(res.candidates[0].content.parts[0].text))
            {
                return res.candidates[0].content.parts[0].text;
            }
            
            if (res?.promptFeedback?.safetyRatings != null && res.promptFeedback.safetyRatings.Any())
            {
                return "(메시지가 안전 등급에 의해 차단되었습니다.)";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Gemini 응답 JSON 파싱 실패: {e.Message}\n원본 JSON: {rawJson}");
        }

        return "(응답 파싱 실패)";
    }
    
    /// <summary>
    /// 텍스트 프롬프트를 보내고 한 번에 전체 답변을 문자열로 받는다.
    /// UniTask 기반 비동기 버전. (ChatService에서 사용)
    /// </summary>
    public static async UniTask<string> AskAsync(
        string apiKey,
        string prompt,
        System.Action<string> onToken = null,  // 토큰 스트림이 필요 없으면 null
        System.Threading.CancellationToken ct = default)
    {
        // ChatCompletion - streaming 옵션까지 쓰려면 따로 구현해야 하지만
        // 여기서는 "한 번에 받기" 버전으로 간단히 처리
        string url = $"{visionEndpoint}?key={apiKey}";

        // 이전 SendTextPrompt와 동일한 JSON 구성
        var reqBody = new RequestBody
        {
            contents = new List<Content>
            {
                new Content
                {
                    role  = "user",
                    parts = new List<Part> { new Part { text = prompt } }
                }
            },
            safetySettings = new List<SafetySetting>
            {
                new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH",      threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_HARASSMENT",       threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT",threshold = "BLOCK_NONE" }
            }
        };

        string json = JsonConvert.SerializeObject(reqBody,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");

        await req.SendWebRequest().ToUniTask(cancellationToken: ct);

        if (req.result == UnityWebRequest.Result.Success)
            return ParseGeminiMessage(req.downloadHandler.text);

        Debug.LogWarning($"GeminiAPI.AskAsync 실패: {req.error}\n{req.downloadHandler.text}");
        return "(Gemini 호출 실패)";
    }
}


// --- END OF FILE GeminiAPI.cs ---