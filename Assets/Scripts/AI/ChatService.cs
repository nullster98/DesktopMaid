using System;
using System.Threading;
using Cysharp.Threading.Tasks;   // UniTask
using UnityEngine;

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
        Action<string> onToken = null,
        CancellationToken ct = default)
    {
        switch (cfg.modelMode)
        {
            // --- 1) 온라인: Gemini API --------------------------
            case ModelMode.GeminiApi:
            string key = APIKeyProvider.Get();
                if(string.IsNullOrEmpty(key)) key = cfg.geminiApiKey;
                return await GeminiAPI.AskAsync(key, prompt, onToken, ct);

            // --- 2) 로컬: Gemma 4B ------------------------------
            case ModelMode.GemmaLocal:
            default:
                var gemma = AIBootstrap.Instance;
                await UniTask.WaitUntil(() => gemma.Initialized, cancellationToken: ct);

                return await gemma.GenerateResponseAsync(
                    prompt,
                    tok =>
                    {
                        onToken?.Invoke(tok);
                        return !ct.IsCancellationRequested;   // 취소 지원
                    });
        }
    }
}