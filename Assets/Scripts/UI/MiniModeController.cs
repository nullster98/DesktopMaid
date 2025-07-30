// --- START OF FILE MiniModeController.cs ---

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MiniModeController : MonoBehaviour
{
    public static MiniModeController Instance { get; private set; }

    [Header("UI 연결")]
    [Tooltip("미니 모드 아이템들이 생성될 부모 Transform (ScrollView의 Content)")]
    [SerializeField] private Transform contentParent; 
    [Tooltip("캐릭터를 표시할 미니모드 프리팹")]
    [SerializeField] private GameObject miniPresetItemPrefab;
    [Tooltip("그룹을 표시할 미니모드 프리팹")]
    [SerializeField] private GameObject miniGroupItemPrefab; // [추가] 그룹 프리팹 참조

    // [수정] _miniItems 리스트는 이제 범용적인 GameObject를 관리합니다.
    private List<GameObject> _miniItems = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // [추가] 이벤트 구독
        if(MainListController.Instance != null)
        {
            // 메인 리스트가 변경될 때마다 미니모드도 함께 새로고침되도록 연결
            CharacterPresetManager.OnPresetsChanged += RefreshAllItems;
            CharacterGroupManager.OnGroupsChanged += RefreshAllItems;
        }
    }

    private void OnDestroy()
    {
        // [추가] 이벤트 구독 해제
        if(MainListController.Instance != null)
        {
            CharacterPresetManager.OnPresetsChanged -= RefreshAllItems;
            CharacterGroupManager.OnGroupsChanged -= RefreshAllItems;
        }
    }
    
    /// <summary>
    /// [핵심 수정] MainListController의 정렬된 데이터를 기반으로 미니 모드 UI 전체를 다시 생성합니다.
    /// </summary>
    public void RefreshAllItems()
    {
        if (contentParent == null || miniPresetItemPrefab == null || miniGroupItemPrefab == null || MainListController.Instance == null)
        {
            // UIManager의 ToggleViewMode에서 호출될 때 아직 MainListController가 준비되지 않았을 수 있으므로
            // 에러 대신 단순 로그로 변경
            Debug.Log("MiniModeController: 필요한 참조가 아직 설정되지 않아 새로고침을 건너뜁니다.");
            return;
        }
        
        Debug.Log("[MiniModeController] 메인 리스트 컨트롤러의 순서를 기반으로 미니모드 아이템을 새로고침합니다.");

        // 기존 미니모드 아이템들 삭제
        foreach (var item in _miniItems) Destroy(item);
        _miniItems.Clear();

        // [핵심 변경] 데이터의 원천을 MainListController의 정렬된 리스트로 변경합니다.
        var mainListData = MainListController.Instance.GetCurrentListData(); // MainListController에 이 함수를 추가해야 합니다.

        foreach (var dataItem in mainListData)
        {
            if (dataItem is CharacterPreset preset)
            {
                // 캐릭터 프리셋인 경우
                GameObject newItemObj = Instantiate(miniPresetItemPrefab, contentParent);
                newItemObj.GetComponent<MiniModeItem>().LinkPreset(preset);
                _miniItems.Add(newItemObj);
            }
            else if (dataItem is CharacterGroup group)
            {
                // 그룹인 경우
                GameObject newItemObj = Instantiate(miniGroupItemPrefab, contentParent);
                newItemObj.GetComponent<MiniModeGroupItem>().LinkGroup(group);
                _miniItems.Add(newItemObj);
            }
        }
    }
    
    public void RemoveItemForPreset(string id, bool isGroup = false) // isGroup 기본값을 false로 설정
    {
        GameObject itemToRemove = null;
        if (isGroup)
        {
            itemToRemove = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeGroupItem>()?.groupID == id);
        }
        else
        {
            itemToRemove = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeItem>()?.presetID == id);
        }
        
        if (itemToRemove != null)
        {
            _miniItems.Remove(itemToRemove);
            Destroy(itemToRemove.gameObject);
        }
    }
    
    public void MoveItemToTop(string id, bool isGroup = false) // isGroup 기본값을 false로 설정
    {
        GameObject itemToMove = null;
        if (isGroup)
        {
            itemToMove = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeGroupItem>()?.groupID == id);
        }
        else
        {
            itemToMove = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeItem>()?.presetID == id);
        }

        if (itemToMove != null)
        {
            itemToMove.transform.SetAsFirstSibling();
        }
    }

    /// <summary>
    /// [신규] 특정 아이템의 UI 업데이트를 요청합니다. (알림 점 표시 등)
    /// </summary>
    public void UpdateItemUI(string id, bool isGroup = false)
    {
        if (isGroup)
        {
            var itemToUpdate = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeGroupItem>()?.groupID == id);
            itemToUpdate?.GetComponent<MiniModeGroupItem>().UpdateNotifyDot();
        }
        else
        {
            var itemToUpdate = _miniItems.FirstOrDefault(i => i.GetComponent<MiniModeItem>()?.presetID == id);
            itemToUpdate?.GetComponent<MiniModeItem>().UpdateAllUI();
        }
    }
}
// --- END OF FILE MiniModeController.cs ---