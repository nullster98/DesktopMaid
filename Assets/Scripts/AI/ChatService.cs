using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using AI;

/// <summary>
/// AI 모델 백엔드(Gemini API, Ollama 등)를 추상화하는 정적 서비스 클래스(Facade).
/// 설정(AIConfig)에 따라 적절한 클라이언트를 호출하여 채팅 요청을 처리합니다.
/// </summary>
public static class ChatService
{
    // 앱 실행 시 한 번만 로드되는 AI 설정 파일
    private static readonly AIConfig cfg = Resources.Load<AIConfig>("AIConfig");

    #region Public Chat API

    /// <summary>
    /// 단일 프롬프트 문자열을 사용하는 모델(예: Gemini API)에 요청을 보냅니다.
    /// 이미지(base64)를 선택적으로 포함할 수 있습니다.
    /// </summary>
    /// <param name="prompt">AI에게 전달할 전체 프롬프트</param>
    /// <param name="base64Image">첨부할 이미지의 base64 인코딩 문자열 (없으면 null)</param>
    /// <param name="onToken">스트리밍 응답을 위한 콜백 (현재 구현에서는 사용되지 않음)</param>
    /// <param name="ct">비동기 작업을 취소하기 위한 CancellationToken</param>
    /// <returns>AI의 최종 응답 문자열</returns>
    public static async UniTask<string> AskAsync(
        string prompt,
        string base64Image = null,
        Action<string> onToken = null, // 현재는 스트리밍 미지원으로 사용되지 않음
        CancellationToken ct = default)
    {
        switch (cfg.modelMode)
        {
            case ModelMode.GeminiApi:
                // 사용자가 입력한 API 키를 우선적으로 사용하고, 없으면 설정 파일의 키를 사용
                string key = APIKeyProvider.Get();
                if (string.IsNullOrEmpty(key))
                {
                    key = cfg.geminiApiKey;
                }

                // 이미지 첨부 유무에 따라 다른 GeminiAPI 함수 호출
                if (string.IsNullOrEmpty(base64Image))
                {
                    return await GeminiAPI.AskAsync(key, prompt, onToken, ct);
                }
                else
                {
                    // GeminiAPI에 이미지와 텍스트를 함께 받는 UniTask 버전 호출
                    return await GeminiAPI.AskWithImageAsync(key, prompt, base64Image, ct);
                }

            case ModelMode.GemmaLocal:
                Debug.LogWarning("[ChatService] GemmaLocal 모드는 현재 지원되지 않습니다. OllamaHttp 모드를 사용해 주세요.");
                return "(GemmaLocal 모드는 현재 비활성화 상태입니다.)";
            
            case ModelMode.OllamaHttp:
                // Ollama 모델은 구조화된 메시지 리스트를 사용해야 하므로, 이 메서드 호출은 잘못된 사용임
                throw new InvalidOperationException("[ChatService] OllamaHttp 모드에서는 List<OllamaMessage>를 사용하는 AskAsync 오버로드를 호출해야 합니다.");

            default:
                throw new ArgumentOutOfRangeException(nameof(cfg.modelMode), "[ChatService] 지원하지 않는 AI 모델 모드입니다.");
        }
    }
    
    /// <summary>
    /// 구조화된 대화 기록(메시지 리스트)을 사용하는 모델(예: Ollama)에 요청을 보냅니다.
    /// </summary>
    /// <param name="messages">역할(role)과 내용(content)으로 구성된 메시지 리스트</param>
    /// <param name="ct">비동기 작업을 취소하기 위한 CancellationToken</param>
    /// <returns>AI의 최종 응답 문자열</returns>
    public static async UniTask<string> AskAsync(
        List<OllamaMessage> messages,
        CancellationToken ct = default)
    {
        if (cfg.modelMode != ModelMode.OllamaHttp)
        {
            throw new InvalidOperationException($"[ChatService] 현재 모델 모드({cfg.modelMode})에서는 이 메서드를 사용할 수 없습니다. OllamaHttp 모드에서만 사용 가능합니다.");
        }
        
        return await OllamaClient.AskAsync(cfg.ollamaModelName, messages, ct);
    }
    
    #endregion
}