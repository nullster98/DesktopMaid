#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ClearAPIKey
{
    private const string ApiKey = "APIKey";

    [MenuItem("Tools/Clear PlayerPrefs/APIKey")]
    private static void ClearKey()
    {
        PlayerPrefs.DeleteKey(ApiKey);
        PlayerPrefs.Save();
        Debug.Log($"[Tools] {ApiKey} 삭제 완료");
    }

    [MenuItem("Tools/Clear PlayerPrefs/ALL")]
    private static void ClearAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[Tools] PlayerPrefs 전체 삭제 완료");
    }
}
#endif