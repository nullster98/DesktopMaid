using UnityEngine;

public enum ModelMode { GeminiApi, GemmaLocal, OllamaHttp }

[CreateAssetMenu(menuName = "AI/Configuration")]
public class AIConfig : ScriptableObject
{
    public ModelMode modelMode = ModelMode.GemmaLocal; // 기본은 로컬 Gemma
    public string geminiApiKey = "";                   // 온라인 모드용

    [Header("Ollama")] public string ollamaModelName = "gemma3:4b";
}