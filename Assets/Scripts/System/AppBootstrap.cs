using UnityEngine;

public static class AppBootstrap
{
    private const string ApiKey        = "APIKey";
    private const string InitFlagKey   = "AppInitialized"; // 0 = 미초기화, 1 = 초기화 완료

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void FirstRunInit()
    {
        // AppData 폴더가 지워졌다면 AppInitialized 플래그도 사라졌을 가능성이 높음
        if (!PlayerPrefs.HasKey(InitFlagKey))
        {
            // 필요하면 추가적인 초기화 로직 삽입
            PlayerPrefs.DeleteKey(ApiKey);      // APIKey 제거
            PlayerPrefs.SetInt(InitFlagKey, 1); // 이제부터는 다시 지우지 않음
            PlayerPrefs.Save();
            Debug.Log("[Bootstrap] 첫 실행 초기화: APIKey 삭제");
        }
    }
}