using UnityEngine;

public static class APIKeyProvider
{
    const string PlayerPrefsKey = "GeminiApiKey";

    public static void Set(string key)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, key);
        PlayerPrefs.Save();
    }

    public static string Get()
        => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
}