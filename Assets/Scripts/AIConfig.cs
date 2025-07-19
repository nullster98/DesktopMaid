using System.Collections.Generic;
using UnityEngine;

public enum ModelMode { GeminiApi, GemmaLocal, OllamaHttp }

[CreateAssetMenu(menuName = "AI/Configuration")]
public class AIConfig : ScriptableObject
{
    [Header("API 설정")]
    public string geminiApiKey; // 구글 API 키

    [Header("모델 선택")]
    public ModelMode modelMode;

    [Header("Ollama 설정")]
    [Tooltip("현재 적용된 Ollama 모델 이름 (예: gemma:2b)")]
    public string ollamaModelName; // 현재 선택된 모델
    [Tooltip("드롭다운에 표시할 Ollama 모델 이름 목록")]
    public List<string> ollamaModelNames = new List<string>(); // 사용 가능한 모델 리스트
}