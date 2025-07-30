// --- START OF FILE GeminiAPI.cs ---

using System;
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
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

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
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_ONLY_HIGH" }
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
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_ONLY_HIGH" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_ONLY_HIGH" }
        };
        RequestBody bodyObj = new RequestBody { contents = new List<Content> { content }, safetySettings = safetySettings };

        yield return SendComplexPrompt(bodyObj, apiKey,
            (aiText, rawJson) => onSuccess?.Invoke(aiText),
            onError);
    }
    
    public static IEnumerator SendComplexPrompt(
        RequestBody requestBody,
        string apiKey,
        System.Action<string, string> onSuccess,
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
                // [수정] 오류 발생 시, 파싱된 오류 메시지를 onError 콜백으로 전달
                string parsedError = ParseError(responseJson, req.responseCode);
                onError?.Invoke(parsedError);
            }
        }
    }

    // [수정] 오류 응답을 파싱하는 새로운 함수
    private static string ParseError(string rawJson, long httpStatusCode)
    {
        try
        {
            GeminiErrorResponse errorRes = JsonConvert.DeserializeObject<GeminiErrorResponse>(rawJson);
            if (errorRes?.error != null)
            {
                return $"오류가 발생했습니다. (코드: {errorRes.error.code}, 상태: {errorRes.error.status})";
            }
        }
        catch { /* 파싱 실패 시 아래 기본 오류 메시지 사용 */ }
        
        return $"오류가 발생했습니다. (HTTP 코드: {httpStatusCode})";
    }

    public static string ParseGeminiMessage(string rawJson)
    {
        try
        {
            GeminiResponse res = JsonConvert.DeserializeObject<GeminiResponse>(rawJson);
            
            if (res?.candidates == null || !res.candidates.Any() || res.candidates[0].content == null)
            {
                if (res?.promptFeedback?.safetyRatings != null && res.promptFeedback.safetyRatings.Any(r => r.probability != "NEGLIGIBLE" && r.probability != "LOW"))
                {
                    // [수정] 차단 시, 특수한 키워드를 반환하여 ChatFunction에서 식별할 수 있도록 함
                    return "GEMINI_SAFETY_BLOCKED";
                }
                return "(응답 내용이 비어있습니다.)";
            }
            
            if (res.candidates[0].content?.parts != null && res.candidates[0].content.parts.Any() &&
                !string.IsNullOrEmpty(res.candidates[0].content.parts[0].text))
            {
                return res.candidates[0].content.parts[0].text;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Gemini 응답 JSON 파싱 실패: {e.Message}\n원본 JSON: {rawJson}");
        }

        return "(응답 파싱 실패)";
    }
    
    public static async UniTask<string> AskAsync(
        string apiKey,
        string prompt,
        System.Action<string> onToken = null,
        System.Threading.CancellationToken ct = default)
    {
        string url = $"{visionEndpoint}?key={apiKey}";

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
                new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH",      threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_HARASSMENT",       threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT",threshold = "BLOCK_ONLY_HIGH" }
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

        try
        {
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result == UnityWebRequest.Result.Success)
                return ParseGeminiMessage(req.downloadHandler.text);

            // [수정] UniTask 버전에서도 오류 파싱 함수 사용
            return ParseError(req.downloadHandler.text, req.responseCode);
        }
        catch (Exception ex)
        {
            // 타임아웃 등 네트워크 예외 처리
            return $"오류가 발생했습니다. ({ex.GetType().Name})";
        }
    }
    
    public static async UniTask<string> AskWithImageAsync(
        string apiKey,
        string prompt,
        string base64Image,
        System.Threading.CancellationToken ct = default)
    {
        string url = $"{visionEndpoint}?key={apiKey}";

        var reqBody = new RequestBody
        {
            contents = new List<Content>
            {
                new Content
                {
                    role  = "user",
                    parts = new List<Part>
                    {
                        new Part { text = prompt },
                        new Part { inlineData = new InlineData { data = base64Image } }
                    }
                }
            },
            safetySettings = new List<SafetySetting>
            {
                new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH",      threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_HARASSMENT",       threshold = "BLOCK_ONLY_HIGH" },
                new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT",threshold = "BLOCK_ONLY_HIGH" }
            }
        };

        string json = JsonConvert.SerializeObject(reqBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");

        try
        {
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);

            if (req.result == UnityWebRequest.Result.Success)
                return ParseGeminiMessage(req.downloadHandler.text);
            
            // [수정] UniTask 버전에서도 오류 파싱 함수 사용
            return ParseError(req.downloadHandler.text, req.responseCode);
        }
        catch (Exception ex)
        {
            return $"오류가 발생했습니다. ({ex.GetType().Name})";
        }
    }
}