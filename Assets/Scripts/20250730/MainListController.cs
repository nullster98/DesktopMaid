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
        // ①현재 UI 순서를 ID List로 기억
        var prevOrder = GetCurrentItemIDOrder();      // preset_xxx / group_xxx
        // ②그 순서를 기준 삼아 새로고침
        RefreshList(prevOrder);
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

        // 1. _listData 초기화 부분 – 기존 AddRange 두 번은 그대로 둬도 무방
        _listData.Clear();
        _listData.AddRange(allPresets);
        _listData.AddRange(allGroups);

        // 2. 정렬 파트
        if (savedOrder != null && savedOrder.Any())
        {
            _listData = _listData
                .OrderBy(item => {
                    // savedOrder 인덱스가 있으면 그대로, 없으면 '맨 뒤' 대신 savedOrder.Count 로 반환
                    string id = item is CharacterPreset p ? $"preset_{p.presetID}"
                        : item is CharacterGroup g ? $"group_{g.groupID}"
                        : "";
                    int idx = savedOrder.IndexOf(id);
                    return idx >= 0 ? idx : savedOrder.Count;   // 새 항목은 우선 savedOrder 뒤로
                })
                // → 그리고 똑같은 인덱스(=새 항목들) 사이에서는 최신 상호작용순으로 섞기
                .ThenByDescending(GetLastInteractionTime)
                .ToList();
        }
        else
        {
            // 기존 “대화 여부 → 마지막 상호작용 시간” 정렬 유지
            _listData = _listData
                .OrderByDescending(item => HasChatHistory(item))
                .ThenByDescending(GetLastInteractionTime)
                .ThenBy(item => GetCreationTimestamp(item))
                .ToList();
        }

        DrawUI();
    }

    // --- 정렬을 위한 헬퍼 함수들 ---
    
    private long GetCreationTimestamp(object item)
    {
        if (item is CharacterPreset p) return p.creationTimestamp;
        if (item is CharacterGroup g) return g.lastInteractionTime.Ticks; // 그룹에는 생성필드가 없으므로 fallback
        return 0;
    }

    /// <summary>
    /// 해당 아이템(프리셋 또는 그룹)의 마지막 상호작용 시간을 가져옵니다.
    /// </summary>
    private DateTime GetLastInteractionTime(object item)
    {
        if (item is CharacterPreset p) return p.lastSpokeTime;
        if (item is CharacterGroup g) return g.lastInteractionTime;
        return DateTime.MinValue;
    }

    /// <summary>
    /// 해당 아이템에 채팅 기록이 하나라도 있는지 확인합니다.
    /// </summary>
    private bool HasChatHistory(object item)
    {
        if (ChatDatabaseManager.Instance == null) return false;

        string id = null;
        bool isGroup = false;

        if (item is CharacterPreset p)
        {
            id = p.presetID;
            isGroup = false;
        }
        else if (item is CharacterGroup g)
        {
            id = g.groupID;
            isGroup = true;
        }

        if (string.IsNullOrEmpty(id)) return false;

        // DB에서 메시지를 1개만 가져와서 존재하는지(Any) 확인하는 것이 효율적입니다.
        if (isGroup)
        {
            return ChatDatabaseManager.Instance.GetRecentGroupMessages(id, 1).Any();
        }
        else
        {
            return ChatDatabaseManager.Instance.GetRecentMessages(id, 1).Any();
        }
    }
    
    public List<object> GetCurrentListData()
    {
        return _listData;
    }

    /// <summary>
    /// 정렬된 데이터 리스트(_listData)를 기반으로 실제 UI 게임 오브젝트를 생성합니다.
    /// </summary>
    private void DrawUI()
    {
        if (scrollContent == null) return;
        
        // --- 1. 준비 단계: 죽은 참조 제거 및 현재 UI 상태 파악 ---
        _uiItems.RemoveAll(item => item == null);

        // 현재 존재하는 모든 그룹 UI를 맵으로 만들어 쉽게 찾을 수 있도록 함
        var groupUiMap = _uiItems
            .Select(ui => ui.GetComponent<GroupListItemMainUI>())
            .Where(script => script != null)
            // [수정] AssignedGroup이 null일 경우를 대비한 안전장치 추가
            .Where(script => script.AssignedGroup != null) 
            .ToDictionary(script => script.AssignedGroup.groupID, script => script.gameObject);

        // --- 2. 생성 단계: 데이터에는 있지만 UI가 없는 그룹 아이템 생성 ---
        foreach (var groupData in _listData.OfType<CharacterGroup>())
        {
            if (!groupUiMap.ContainsKey(groupData.groupID))
            {
                GameObject newItemGo = Instantiate(groupItemPrefab, scrollContent);
                
                newItemGo.GetComponent<GroupListItemMainUI>().Setup(groupData, groupPanelController);
                
                _uiItems.Add(newItemGo);
                groupUiMap[groupData.groupID] = newItemGo; // 새로 만든 UI도 맵에 추가
            }
        }
        
        // --- 3. 정렬 및 활성화 단계: 정렬된 데이터 순서대로 UI 순서 맞추기 ---
        var activeItemIds = new HashSet<string>();

        foreach (var dataItem in _listData)
        {
            GameObject uiObject = null;
            string itemId = null;

            if (dataItem is CharacterPreset preset)
            {
                uiObject = preset.gameObject;
                itemId = preset.presetID;
            }
            else if (dataItem is CharacterGroup group)
            {
                if (groupUiMap.TryGetValue(group.groupID, out var groupUi))
                {
                    uiObject = groupUi;
                    // [핵심 수정] 기존에 존재하던 UI 아이템의 내용을 최신 데이터로 업데이트합니다.
                    uiObject.GetComponent<GroupListItemMainUI>().Setup(group, groupPanelController);
                }
                itemId = group.groupID;
            }

            if (uiObject != null)
            {
                uiObject.SetActive(true);
                uiObject.transform.SetParent(scrollContent, false);
                uiObject.transform.SetAsLastSibling(); // 정렬된 순서대로 맨 뒤에 붙임
                activeItemIds.Add(itemId);
            }
        }

        // --- 4. 정리 단계: 활성화되지 않은 나머지 UI 비활성화 ---
        
        // 캐릭터 프리셋 비활성화
        foreach (var preset in CharacterPresetManager.Instance.presets)
        {
            if (!activeItemIds.Contains(preset.presetID))
            {
                preset.gameObject.SetActive(false);
            }
        }
        
        // 그룹 UI 비활성화
        foreach (var groupUi in groupUiMap.Values)
        {
            var script = groupUi.GetComponent<GroupListItemMainUI>();
            // [수정] AssignedGroup이 null일 경우를 대비한 안전장치 추가
            if (script != null && script.AssignedGroup != null && !activeItemIds.Contains(script.AssignedGroup.groupID))
            {
                groupUi.SetActive(false); // Destroy 대신 비활성화
            }
        }
    }
    
    /// <summary>
    /// [추가] 그룹의 마지막 메시지를 가져오는 로직을 별도 함수로 분리
    /// </summary>
    private string GetLastMessageForGroup(string groupId)
    {
        var rec = ChatDatabaseManager.Instance
            .GetRecentGroupMessages(groupId, 1).FirstOrDefault();
        if (rec == null) return " ";

        var data = JsonUtility.FromJson<MessageData>(rec.Message);
        if (data == null) return " ";

        if (!string.IsNullOrEmpty(data.textContent))
            return data.textContent.Replace("\n", " ").Trim();

        if (data.type == "image")                     return "Image";
        if (data.type == "text" && data.fileSize > 0) return $"Text : {data.fileName}";
        if (rec.SenderID == "system")                 return data.textContent;

        return " ";
    }
}

// --- END OF FILE MainListController.cs ---