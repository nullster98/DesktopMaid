// --- START OF FILE DlcManager.cs ---

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// DLC 소유권을 확인하고 관련 기능을 제공하는 싱글톤 매니저.
/// </summary>
public class DlcManager : MonoBehaviour
{
    public static DlcManager Instance { get; private set; }

    // TODO: 아래 값들을 Steam 파트너 사이트에서 발급받은 실제 DLC App ID로 교체하세요.
    public const uint PREMIUM_CHARACTER_PACK_DLC_ID = 2998670; // 예시: 프리미엄 캐릭터 팩
    // public const uint OUTFIT_PACK_DLC_ID = 456789; // 예시: 의상 팩

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 특정 DLC를 사용자가 소유하고 있는지 확인합니다.
    /// </summary>
    /// <param name="dlcAppId">확인할 DLC의 App ID</param>
    /// <returns>소유하고 있으면 true, 아니면 false</returns>
    public bool IsDlcOwned(uint dlcAppId)
    {
#if !DISABLESTEAMWORKS
        // Steam이 초기화되었는지 먼저 확인합니다.
        if (SteamManager.Initialized)
        {
            // Steam 서버에 이 사용자가 해당 DLC를 설치(소유)했는지 직접 물어봅니다.
            return SteamApps.BIsDlcInstalled(new AppId_t(dlcAppId));
        }
#endif
        // Steam 연동이 안 되어 있거나 (에디터 테스트 등) 실패 시, 무조건 false를 반환합니다.
        return false;
    }

    /// <summary>
    /// 사용자를 Steam 상점의 DLC 페이지로 안내합니다.
    /// </summary>
    /// <param name="dlcAppId">안내할 DLC의 App ID</param>
    public void OpenDlcStorePage(uint dlcAppId)
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
        {
            // Steam 오버레이를 통해 상점 페이지를 엽니다.
            SteamFriends.ActivateGameOverlayToStore(new AppId_t(dlcAppId), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
        }
        else
        {
            Debug.LogWarning("Steam is not running. Cannot open store page.");
        }
#endif
    }
}
// --- END OF FILE DlcManager.cs ---