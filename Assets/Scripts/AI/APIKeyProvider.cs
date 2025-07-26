using UnityEngine;

/// <summary>
/// Gemini API 키를 PlayerPrefs를 통해 로컬에 저장하고 불러오는 static 유틸리티 클래스.
/// 앱 재시작 시에도 API 키를 유지하는 역할을 합니다.
/// </summary>
public static class APIKeyProvider
{
    // PlayerPrefs에 API 키를 저장하기 위한 고유 키 값.
    private const string PlayerPrefsKey = "GeminiApiKey";

    /// <summary>
    /// API 키를 로컬에 저장합니다.
    /// </summary>
    /// <param name="key">저장할 API 키 문자열</param>
    public static void Set(string key)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, key);
        PlayerPrefs.Save(); // 변경 사항을 디스크에 즉시 기록
    }

    /// <summary>
    /// 로컬에 저장된 API 키를 불러옵니다.
    /// </summary>
    /// <returns>저장된 API 키. 값이 없으면 빈 문자열을 반환합니다.</returns>
    public static string Get()
    {
        return PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
    }
}