using System;
using System.Threading;
using Cysharp.Threading.Tasks;   // UniTask
using UnityEngine;
using AI;

/// <summary>
/// 설정(AIConfig)에 따라 Gemini API 또는 로컬 Gemma를 호출하는 래퍼
/// </summary>
public static class ChatService
{
    // Resources/AIConfig.asset 로드 (한 번만)
    static readonly AIConfig cfg = Resources.Load<AIConfig>("AIConfig");

    /// <summary>
    /// 프롬프트를 보내고 최종 답변을 받는다.
    /// onToken == null  ➜ 완성 텍스트만 반환
    /// onToken != null ➜ 토큰 스트리밍; 콜백마다 조각 전달
    /// </summary>
    public static async UniTask<string> AskAsync(
        string prompt,
        string base64Image = null,
        Action<string> onToken = null,
        CancellationToken ct = default)
    {
        switch (cfg.modelMode)
        {
            // --- 1) 온라인: Gemini API --------------------------
            case ModelMode.GeminiApi:
                string key = APIKeyProvider.Get();
                if (string.IsNullOrEmpty(key)) key = cfg.geminiApiKey;

                // [수정] 이미지 유무에 따라 다른 API 호출
                if (string.IsNullOrEmpty(base64Image))
                {
                    // 텍스트만 있을 경우
                    return await GeminiAPI.AskAsync(key, prompt, onToken, ct);
                }
                else
                {
                    // 이미지도 있을 경우 (새로운 메서드 호출 필요)
                    // GeminiAPI에 이미지+텍스트를 받는 UniTask 버전 AskAsync가 필요합니다.
                    // 아래 GeminiAPI.cs 수정본에서 이 부분을 추가할 것입니다.
                    return await GeminiAPI.AskWithImageAsync(key, prompt, base64Image, ct);
                }

            // --- 2) 로컬: Gemma 4B ------------------------------
            case ModelMode.GemmaLocal:
                Debug.LogWarning("GemmaLocal 모드는 현재 비활성화되어 있습니다. OllamaHttp 모드를 사용해주세요.");
                return "(GemmaLocal 모드는 현재 비활성화 상태입니다.)";
            
            // --- 3) 로컬 Ollma HTTP서버 ---------------------------
            case ModelMode.OllamaHttp:
                return await OllamaClient.AskAsync(prompt, cfg.ollamaModelName,base64Image, ct);

            default:
                throw new ArgumentOutOfRangeException(nameof(cfg.modelMode), "지원하지 않는 AI 모델 모드입니다.");
                
        }
    }
}