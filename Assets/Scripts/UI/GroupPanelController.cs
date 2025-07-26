// --- START OF FILE GroupPanelController.cs ---

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

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
    [SerializeField] private GameObject rightPanel_MemberDetails; 
    [SerializeField] private GroupMemberDetailPanelController memberDetailPanelController;
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
    
    private AIConfig _aiConfig;
    
    [Header("Localization")]
    [SerializeField] private LocalizedString newGroupTextKey;

    private void Awake()
    {
        _aiConfig = Resources.Load<AIConfig>("AIConfig");
    }

    private void Start()
    {
        if (detailPanelController != null)
        {
            detailPanelController.Initialize(this);
        }

        if (memberDetailPanelController != null)
        {
            memberDetailPanelController.Initialize(this);
        }
    }

    private void OnEnable()
    {
        newGroupButton.onClick.RemoveAllListeners();
        newGroupButton.onClick.AddListener(OnClick_AddNewGroup);
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        SaveController.OnLoadComplete += HandleLoadComplete;
        CharacterPresetManager.OnPresetsChanged += RefreshGroupListUI;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        SaveController.OnLoadComplete -= HandleLoadComplete;
        CharacterPresetManager.OnPresetsChanged -= RefreshGroupListUI;
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
            rightPanel_MemberDetails.SetActive(false);
            rightPanel_GroupDetails.SetActive(true);
            detailPanelController.LoadGroupData(groupToSelect);
        }
        else
        {
            // 선택 해제
            rightPanel_Placeholder.SetActive(true);
            rightPanel_MemberDetails.SetActive(false);
            rightPanel_GroupDetails.SetActive(false);
        }
    }

    // [수정됨] 이 함수도 '선택'만 담당합니다. 채팅창은 건드리지 않습니다.
    public void SelectPreset(CharacterPreset presetToSelect)
    {
        selectedGroupID = null; // 그룹 선택은 해제
        selectedPresetID = presetToSelect?.presetID;
        UpdateSelectionVisuals();

        // 선택된 캐릭터가 그룹에 속해 있는지 확인
        if (presetToSelect != null && !string.IsNullOrEmpty(presetToSelect.groupID))
        {
            var group = CharacterGroupManager.Instance.GetGroup(presetToSelect.groupID);
            if (group != null)
            {
                // 캐릭터가 속한 그룹 정보를 찾아서 멤버 상세 패널에 함께 넘겨줍니다.
                rightPanel_Placeholder.SetActive(false);
                rightPanel_GroupDetails.SetActive(false); // 그룹 패널 숨기기
                rightPanel_MemberDetails.SetActive(true); // 멤버 패널 보이기

                // 멤버 상세 패널 컨트롤러에게 데이터 로드를 명령
                memberDetailPanelController.LoadMemberData(presetToSelect, group);
            }
        }
        else
        {
            // 그룹에 속하지 않은 캐릭터를 선택했거나, 선택을 해제한 경우
            rightPanel_Placeholder.SetActive(true);
            rightPanel_GroupDetails.SetActive(false);
            rightPanel_MemberDetails.SetActive(false);
        }
    }

    // [신규] 그룹 채팅창을 열거나, 이미 열려있다면 앞으로 가져오는 함수. 더블클릭 시 호출됩니다.
    public async void OpenOrFocusGroupChatWindow(CharacterGroup group)
    {
        if (group == null) return;

        if (_aiConfig.modelMode == ModelMode.GeminiApi)
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.ApiSetting);
                return; // API 키가 없으면 함수를 즉시 종료하여 채팅창을 열지 않습니다.
            }
        }
        
        else if (_aiConfig.modelMode == ModelMode.OllamaHttp)
        {
            bool isConnected = await OllamaClient.CheckConnectionAsync();
            if (!isConnected)
            {
                // 연결 실패 시 경고를 표시하고 함수를 즉시 종료합니다.
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.LocalModelSetting);
                return;
            }
        }

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

    public async void OnClick_AddNewGroup()
    {
        string localizedNewGroupName = "New Group"; 

        // LocalizedString 키가 연결되어 있는지 확인합니다.
        if (newGroupTextKey != null && !newGroupTextKey.IsEmpty)
        {
            // 비동기적으로 로컬라이즈된 문자열을 가져옵니다.
            var handle = newGroupTextKey.GetLocalizedStringAsync();
            await handle; // 작업이 끝날 때까지 기다립니다.

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // 성공적으로 가져왔으면 값을 교체합니다.
                localizedNewGroupName = handle.Result;
            }
            else
            {
                Debug.LogError($"'{newGroupTextKey.TableReference}/{newGroupTextKey.TableEntryReference}' 키의 문자열을 불러오는 데 실패했습니다.");
            }
        }
        else
        {
            Debug.LogWarning("NewGroupTextKey가 설정되지 않아 기본값 'New Group'을 사용합니다.");
        }

        // 가져온 로컬라이즈된 이름으로 그룹을 생성합니다.
        var newGroup = CharacterGroupManager.Instance.CreateGroup(localizedNewGroupName, "", "");
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
    
    public void RefreshActiveGroupChat(CharacterGroup group)
    {
        if (group == null) return;

        // 관리 중인 활성 그룹 채팅 목록에 해당 그룹 ID의 채팅창이 있는지 확인
        if (activeGroupChats.TryGetValue(group.groupID, out ChatUI chatUIInstance))
        {
            // 있다면, ChatUI의 그룹 설정 함수를 다시 호출하여 최신 정보를 주입합니다.
            Debug.Log($"[GroupPanelController] 활성화된 '{group.groupName}' 그룹 채팅창의 설정을 최신 정보로 갱신합니다.");
            chatUIInstance.SetupForGroupChat(group);
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