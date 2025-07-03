// --- START OF FILE GroupPanelController.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

public class GroupPanelController : MonoBehaviour
{
    [Header("UI Prefabs")]
    [SerializeField] private GameObject groupListItemWrapperPrefab;
    [SerializeField] private GameObject presetListItemWrapperPrefab;

    [Header("UI References")]
    [SerializeField] private Transform listContentParent;
    [SerializeField] private Button newGroupButton;

    [Header("Right Panel References")]
    [SerializeField] private GameObject rightPanel_GroupDetails;
    [SerializeField] private GroupDetailPanelController detailPanelController;
    [SerializeField] private GameObject rightPanel_Placeholder;

    [Header("Chat UI References")] // [수정됨]
    [Tooltip("그룹 채팅창으로 사용할 ChatUI 프리팹")]
    [SerializeField] private GameObject chatUIPrefab;
    [Tooltip("생성된 채팅창들이 위치할 부모 Transform (Canvas 등)")]
    [SerializeField] private Transform chatUIParent;

    [Header("UI Style")]
    [SerializeField] private Color selectedColor = new Color(0.29f, 0.33f, 0.41f, 1f);
    [SerializeField] private Color deselectedColor = new Color(0, 0, 0, 0);

    // --- 내부 상태 관리 변수 ---
    private List<GroupListItemUI> generatedGroupItems = new List<GroupListItemUI>();
    private List<PresetListItemUI> generatedPresetItems = new List<PresetListItemUI>();
    private string selectedGroupID = null;
    private string selectedPresetID = null;
    private HashSet<string> expandedGroupIDs = new HashSet<string>();
    
    // [신규] 생성된 그룹 채팅 UI를 관리하기 위한 딕셔너리
    private Dictionary<string, ChatUI> activeGroupChats = new Dictionary<string, ChatUI>();


    private void Start()
    {
        if (detailPanelController != null)
        {
            detailPanelController.Initialize(this);
        }
    }

    private void OnEnable()
    {
        newGroupButton.onClick.RemoveAllListeners();
        newGroupButton.onClick.AddListener(OnClick_AddNewGroup);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        SaveController.OnLoadComplete += HandleLoadComplete;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        SaveController.OnLoadComplete -= HandleLoadComplete;
    }

    private void HandleLoadComplete()
    {
        RefreshGroupListUI();
        SelectGroup(null);
    }

    #region --- List UI Drawing ---

    public void RefreshGroupListUI()
    {
        // ... (이 함수는 변경 없음)
        if (CharacterGroupManager.Instance == null || CharacterPresetManager.Instance == null) return;

        string previouslySelectedGroup = selectedGroupID;
        string previouslySelectedPreset = selectedPresetID;
        
        generatedGroupItems.Clear();
        generatedPresetItems.Clear();
        foreach (Transform child in listContentParent) Destroy(child.gameObject);

        var topLevelGroups = CharacterGroupManager.Instance.allGroups.Where(g => string.IsNullOrEmpty(g.parentGroupID)).ToList();
        foreach (var group in topLevelGroups)
        {
            DrawGroupAndChildren(group, 0);
        }

        var unassignedPresets = CharacterPresetManager.Instance.presets.Where(p => string.IsNullOrEmpty(p.groupID)).ToList();
        foreach (var preset in unassignedPresets)
        {
            DrawPreset(preset, 0);
        }
        
        selectedGroupID = previouslySelectedGroup;
        selectedPresetID = previouslySelectedPreset;
        UpdateSelectionVisuals();
    }

    private void DrawGroupAndChildren(CharacterGroup group, int depth)
    {
        GameObject wrapperGO = Instantiate(groupListItemWrapperPrefab, listContentParent);
        GroupListItemUI itemUI = wrapperGO.GetComponentInChildren<GroupListItemUI>();

        if (itemUI != null)
        {
            var members = CharacterGroupManager.Instance.GetGroupMembers(group.groupID);
            var subGroups = CharacterGroupManager.Instance.allGroups.Where(g => g.parentGroupID == group.groupID).ToList();

            bool hasChildren = members.Any() || subGroups.Any();
            bool isExpanded = expandedGroupIDs.Contains(group.groupID);
            
            itemUI.Setup(group, depth, this, selectedColor, deselectedColor, isExpanded, hasChildren);
            generatedGroupItems.Add(itemUI);

            if (isExpanded && hasChildren)
            {
                foreach (var member in members) DrawPreset(member, depth + 1);
                foreach (var subGroup in subGroups) DrawGroupAndChildren(subGroup, depth + 1);
            }
        }
    }

    private void DrawPreset(CharacterPreset preset, int depth)
    {
        // ... (이 함수는 변경 없음)
        GameObject wrapperGO = Instantiate(presetListItemWrapperPrefab, listContentParent);
        PresetListItemUI itemUI = wrapperGO.GetComponentInChildren<PresetListItemUI>();
        if (itemUI != null)
        {
            itemUI.Setup(preset, depth, this, selectedColor, deselectedColor);
            generatedPresetItems.Add(itemUI);
        }
    }

    #endregion

    #region --- Selection & Interaction ---

    private void UpdateSelectionVisuals()
    {
        foreach (var item in generatedGroupItems)
        {
            item.SetSelected(!string.IsNullOrEmpty(selectedGroupID) && item.AssignedGroup.groupID == selectedGroupID);
        }
        foreach (var item in generatedPresetItems)
        {
            item.SetSelected(!string.IsNullOrEmpty(selectedPresetID) && item.AssignedPreset.presetID == selectedPresetID);
        }
    }

    // [수정됨] 이 함수는 이제 '선택'과 '정보 표시'만 담당합니다. 채팅창을 열지 않습니다.
    public void SelectGroup(CharacterGroup groupToSelect)
    {
        selectedPresetID = null;
        selectedGroupID = groupToSelect?.groupID;
        UpdateSelectionVisuals();

        if (groupToSelect != null)
        {
            // 오른쪽 상세 정보 패널 업데이트
            rightPanel_Placeholder.SetActive(false);
            rightPanel_GroupDetails.SetActive(true);
            detailPanelController.LoadGroupData(groupToSelect);
        }
        else
        {
            // 선택 해제
            rightPanel_Placeholder.SetActive(true);
            rightPanel_GroupDetails.SetActive(false);
        }
    }

    // [수정됨] 이 함수도 '선택'만 담당합니다. 채팅창은 건드리지 않습니다.
    public void SelectPreset(CharacterPreset presetToSelect)
    {
        selectedGroupID = null;
        selectedPresetID = presetToSelect?.presetID;
        UpdateSelectionVisuals();

        if (presetToSelect != null)
        {
            // TODO: 캐릭터 상세 정보 패널 표시 로직
            rightPanel_Placeholder.SetActive(false);
            rightPanel_GroupDetails.SetActive(false);
        }
    }

    // [신규] 그룹 채팅창을 열거나, 이미 열려있다면 앞으로 가져오는 함수. 더블클릭 시 호출됩니다.
    public void OpenOrFocusGroupChatWindow(CharacterGroup group)
    {
        if (group == null) return;

        ChatUI chatUIInstance;

        // 이미 해당 그룹의 채팅창이 메모리에 생성되어 있는지 확인
        if (activeGroupChats.ContainsKey(group.groupID))
        {
            // 있다면, 저장해둔 인스턴스를 가져옵니다.
            chatUIInstance = activeGroupChats[group.groupID];
        }
        else // 없다면 새로 생성
        {
            GameObject chatGO = Instantiate(chatUIPrefab, chatUIParent);
            chatUIInstance = chatGO.GetComponent<ChatUI>();
            
            // TODO (3단계): ChatUI를 그룹 모드로 설정하는 함수 호출
            chatUIInstance.SetupForGroupChat(group); 
            chatGO.name = $"GroupChat_{group.groupName}"; // 디버깅용 이름 설정

            // 새로 만든 인스턴스를 딕셔너리에 저장해 나중에 재사용합니다.
            activeGroupChats.Add(group.groupID, chatUIInstance);
            chatUIInstance.ShowChatUI(false);
        }

        // 채팅창을 활성화하고 다른 UI들 위로 보이게 합니다.
        chatUIInstance.ShowChatUI(true);
        chatUIInstance.transform.SetAsLastSibling();
        
        chatUIInstance.RefreshFromDatabase();
    }

    public void OnClick_AddNewGroup()
    {
        var newGroup = CharacterGroupManager.Instance.CreateGroup("새 그룹", "", "");
        expandedGroupIDs.Add(newGroup.groupID);
        SelectGroup(newGroup); // 새 그룹 생성 후 바로 선택
        RefreshGroupListUI();
    }
    
    public void ToggleGroupExpansion(string groupID)
    {
        if (expandedGroupIDs.Contains(groupID))
        {
            expandedGroupIDs.Remove(groupID);
        }
        else
        {
            expandedGroupIDs.Add(groupID);
        }
        RefreshGroupListUI();
    }
    
    public void RefreshDetailPanelIfShowingByID(string groupID)
    {
        if (!string.IsNullOrEmpty(selectedGroupID) && selectedGroupID == groupID)
        {
            detailPanelController.RefreshMemberListUI();
        }
    }

    #endregion

    #region --- Event Handlers & Utilities ---

    private void OnLocaleChanged(Locale newLocale)
    {
        if (this.gameObject.activeInHierarchy)
        {
            RefreshGroupListUI();
        }
    }

    #endregion
}
// --- END OF FILE GroupPanelController.cs ---