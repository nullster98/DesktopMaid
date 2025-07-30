// --- START OF FILE MainListController.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 메인 화면의 캐릭터/그룹 통합 리스트를 관리하고 UI를 동적으로 생성/정렬하는 중앙 컨트롤러입니다.
/// </summary>
public class MainListController : MonoBehaviour
{
    public static MainListController Instance { get; private set; }

    [Header("UI Prefabs")]
    [Tooltip("메인 리스트에 표시될 캐릭터 아이템 프리팹 (CharacterPreset.cs가 붙어있는 프리팹)")]
    [SerializeField] private GameObject characterItemPrefab;
    [Tooltip("메인 리스트에 표시될 그룹 아이템 프리팹 (GroupListItemMainUI.cs가 붙어있는 프리팹)")]
    [SerializeField] private GameObject groupItemPrefab;

    [Header("UI References")]
    [Tooltip("프리팹들이 생성될 부모 Transform (ScrollView의 Content)")]
    [SerializeField] private Transform scrollContent;

    [Header("Dependencies")]
    [Tooltip("그룹 아이템 클릭 시 채팅창을 열기 위해 필요한 컨트롤러 참조")]
    [SerializeField] private GroupPanelController groupPanelController;

    // 내부 데이터 관리용 리스트
    private List<object> _listData = new List<object>(); // CharacterPreset 또는 CharacterGroup 인스턴스를 저장
    private List<GameObject> _uiItems = new List<GameObject>(); // 생성된 UI 게임 오브젝트들을 관리

    /// <summary>
    /// SaveController에서 사용할 수 있도록 현재 UI 리스트의 순서를 ID 목록으로 반환합니다.
    /// </summary>
    public List<string> GetCurrentItemIDOrder()
    {
        return _listData.Select(item => {
            if (item is CharacterPreset p) return "preset_" + p.presetID; // 접두사를 붙여 구분
            if (item is CharacterGroup g) return "group_" + g.groupID;   // 접두사를 붙여 구분
            return null;
        }).Where(id => id != null).ToList();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        ChatDatabaseManager.OnPersonalMessageAdded += (id, isUser) => OnNewMessage(id, ListItemType.Character, isUser);
        ChatDatabaseManager.OnGroupMessageAdded += (id, isUser) => OnNewMessage(id, ListItemType.Group, isUser);
        CharacterPresetManager.OnPresetsChanged += RefreshListFromEvent;
        CharacterGroupManager.OnGroupsChanged += RefreshListFromEvent;
        SaveController.OnLoadComplete += HandleLoadComplete;
    }

    private void OnDisable()
    {
        ChatDatabaseManager.OnPersonalMessageAdded -= (id, isUser) => OnNewMessage(id, ListItemType.Character, isUser);
        ChatDatabaseManager.OnGroupMessageAdded -= (id, isUser) => OnNewMessage(id, ListItemType.Group, isUser);
        CharacterPresetManager.OnPresetsChanged -= RefreshListFromEvent;
        CharacterGroupManager.OnGroupsChanged -= RefreshListFromEvent;
        SaveController.OnLoadComplete -= HandleLoadComplete;
    }
    
    private void HandleLoadComplete()
    {
        RefreshList();
    }
    
    private void RefreshListFromEvent()
    {
        RefreshList();
    }


    /// <summary>
    /// 새로운 메시지가 수신되었을 때 호출되는 이벤트 핸들러입니다.
    /// </summary>
    private void OnNewMessage(string id, ListItemType type, bool isSentByUser)
    {
        object targetItem = null;
        if (type == ListItemType.Character)
        {
            targetItem = CharacterPresetManager.Instance.GetPreset(id);
        }
        else // type == ListItemType.Group
        {
            targetItem = CharacterGroupManager.Instance.GetGroup(id);
        }

        if (targetItem == null) return;

        if (targetItem is CharacterPreset p) p.lastSpokeTime = DateTime.Now;
        if (targetItem is CharacterGroup g) g.lastInteractionTime = DateTime.Now;

        if (!isSentByUser)
        {
            bool isChatWindowVisible = IsChatWindowVisible(id, type);
            if (!isChatWindowVisible)
            {
                if (targetItem is CharacterPreset p_notify) p_notify.notifyImage.SetActive(true);
                if (targetItem is CharacterGroup g_notify)
                {
                    g_notify.HasNotification = true;
                    // [수정] GetInstanceID() 오류 해결. UI 아이템과 데이터 아이템을 직접 비교합니다.
                    var uiItem = _uiItems.FirstOrDefault(ui => {
                        var script = ui.GetComponent<GroupListItemMainUI>();
                        return script != null && script.AssignedGroup == g_notify;
                    });
                    uiItem?.GetComponent<GroupListItemMainUI>()?.UpdateNotification(true);
                }
            }
        }
        RefreshList();
    }
    
    private bool IsChatWindowVisible(string id, ListItemType type)
    {
        var chatUIs = FindObjectsOfType<ChatUI>(true);
        foreach(var chatUI in chatUIs)
        {
            if (chatUI.OwnerID == id && chatUI.gameObject.activeInHierarchy && chatUI.GetComponent<CanvasGroup>().alpha > 0)
            {
                if ((type == ListItemType.Character && !chatUI.isGroupChat) || (type == ListItemType.Group && chatUI.isGroupChat))
                {
                    return true;
                }
            }
        }
        return false;
    }


    /// <summary>
    /// 모든 데이터 소스를 기반으로 리스트를 새로고침하고 UI를 다시 그립니다.
    /// </summary>
    public void RefreshList(List<string> savedOrder = null)
    {
        if (CharacterPresetManager.Instance == null || CharacterGroupManager.Instance == null) return;

        var allPresets = CharacterPresetManager.Instance.presets;
        var allGroups = CharacterGroupManager.Instance.allGroups;

        _listData.Clear();
        _listData.AddRange(allPresets);
        _listData.AddRange(allGroups);

        if (savedOrder != null && savedOrder.Any())
        {
            _listData = _listData.OrderBy(item => {
                string itemID = "";
                // [수정] 변수 이름 변경으로 충돌 회피 (p -> preset) (g -> group)
                if (item is CharacterPreset preset) itemID = "preset_" + preset.presetID;
                else if (item is CharacterGroup group) itemID = "group_" + group.groupID;
                int index = savedOrder.IndexOf(itemID);
                return (index == -1) ? int.MaxValue : index;
            }).ToList();
        }
        else
        {
             _listData = _listData.OrderByDescending(item => {
                // 여기서는 변수 이름이 겹치지 않으므로 그대로 p, g를 사용해도 무방합니다.
                if (item is CharacterPreset p) return p.lastSpokeTime;
                else if (item is CharacterGroup g) return g.lastInteractionTime;
                return DateTime.MinValue;
            }).ToList();
        }

        DrawUI();
    }

    /// <summary>
    /// 정렬된 데이터 리스트(_listData)를 기반으로 실제 UI 게임 오브젝트를 생성합니다.
    /// </summary>
    private void DrawUI()
    {
        if (scrollContent == null) return;

        // 기존 그룹 UI만 파괴하고, 캐릭터 프리셋은 풀에서 관리하도록 변경
        var itemsToDestroy = _uiItems.Where(item => item != null && item.GetComponent<GroupListItemMainUI>() != null).ToList();
        foreach (var item in itemsToDestroy)
        {
            _uiItems.Remove(item);
            Destroy(item);
        }

        // 캐릭터 프리셋은 데이터 리스트에 없는 경우 비활성화
        var activePresetIds = _listData.OfType<CharacterPreset>().Select(p => p.presetID).ToHashSet();
        foreach (var presetInScene in CharacterPresetManager.Instance.presets)
        {
            if (!activePresetIds.Contains(presetInScene.presetID))
            {
                presetInScene.gameObject.SetActive(false);
            }
        }
        
        // 정렬된 데이터 순서대로 UI 생성 및 활성화
        foreach (var data in _listData)
        {
            if (data is CharacterPreset preset)
            {
                preset.gameObject.SetActive(true);
                preset.transform.SetParent(scrollContent, false);
                preset.transform.SetAsLastSibling(); // 순서 맞추기
                if(!_uiItems.Contains(preset.gameObject)) _uiItems.Add(preset.gameObject);
            }
            else if (data is CharacterGroup group)
            {
                // 이미 해당 그룹의 UI가 있는지 확인 (중복 생성 방지)
                bool exists = _uiItems.Any(ui => ui.GetComponent<GroupListItemMainUI>()?.AssignedGroup == group);
                if (!exists)
                {
                    GameObject itemGO = Instantiate(groupItemPrefab, scrollContent);
                    itemGO.GetComponent<GroupListItemMainUI>().Setup(group, groupPanelController);
                    itemGO.transform.SetAsLastSibling(); // 순서 맞추기
                    _uiItems.Add(itemGO);
                }
            }
        }
    }
}

// --- END OF FILE MainListController.cs ---