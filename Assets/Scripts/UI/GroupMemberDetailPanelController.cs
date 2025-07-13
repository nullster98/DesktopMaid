// --- START OF FILE GroupMemberDetailPanelController.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic; // List를 사용하기 위해 추가

public class GroupMemberDetailPanelController : MonoBehaviour
{
    [Header("고정 UI 참조")]
    [SerializeField] private Image characterIconImage;
    [SerializeField] private TMP_InputField roleInputField;
    [SerializeField] private Button saveButton;
    [SerializeField] private Sprite defaultIcon;

    [Header("동적 UI 참조")]
    [Tooltip("관계 설정 아이템 프리팹(RelationshipItemUI)")]
    [SerializeField] private GameObject relationshipItemPrefab; 
    [Tooltip("관계 설정 아이템들이 생성될 부모 Transform (Scroll View의 Content)")]
    [SerializeField] private Transform relationshipListContent;

    // --- 내부 변수 ---
    private CharacterPreset currentPreset; // 현재 보고 있는 캐릭터 (주체)
    private CharacterGroup currentGroup;   // 현재 속한 그룹
    private GroupPanelController mainPanelController;
    
    // 생성된 관계 UI 아이템들을 관리하기 위한 리스트
    private List<RelationshipItemUI> spawnedRelationshipItems = new List<RelationshipItemUI>();

    public void Initialize(GroupPanelController controller)
    {
        mainPanelController = controller;
        saveButton.onClick.AddListener(OnClick_Save);
    }

    /// <summary>
    /// 외부(GroupPanelController)에서 호출. 멤버 상세 정보를 로드하고 UI를 채웁니다.
    /// </summary>
    public void LoadMemberData(CharacterPreset preset, CharacterGroup group)
    {
        this.currentPreset = preset;
        this.currentGroup = group;

        if (preset == null || group == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 1. 고정 UI 채우기
        characterIconImage.sprite = preset.characterImage.sprite ?? defaultIcon;
        
        // 역할 정보 불러오기
        if (group.memberRoles.TryGetValue(preset.presetID, out string role))
        {
            roleInputField.text = role;
        }
        else
        {
            roleInputField.text = ""; 
        }

        // 2. 동적 관계 리스트 생성
        UpdateRelationshipList();
        
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// 관계 설정 리스트를 현재 데이터에 맞게 다시 그립니다.
    /// </summary>
    private void UpdateRelationshipList()
    {
        // 기존에 생성된 아이템들 모두 파괴
        foreach (Transform child in relationshipListContent)
        {
            Destroy(child.gameObject);
        }
        spawnedRelationshipItems.Clear();

        // 나를 제외한 그룹의 다른 멤버들을 가져옴
        var otherMembers = CharacterGroupManager.Instance.GetGroupMembers(currentGroup.groupID);
        otherMembers.Remove(currentPreset);

        // 다른 멤버 각각에 대해 관계 설정 UI를 생성
        foreach (var member in otherMembers)
        {
            GameObject itemGO = Instantiate(relationshipItemPrefab, relationshipListContent);
            RelationshipItemUI itemUI = itemGO.GetComponent<RelationshipItemUI>();

            // 현재 저장된 관계 정보를 찾음
            string currentRelationship = "";
            if (currentGroup.memberRelationships.ContainsKey(currentPreset.presetID) &&
                currentGroup.memberRelationships[currentPreset.presetID].ContainsKey(member.presetID))
            {
                currentRelationship = currentGroup.memberRelationships[currentPreset.presetID][member.presetID];
            }
            
            // UI에 데이터 설정
            itemUI.Setup(member, currentRelationship);
            spawnedRelationshipItems.Add(itemUI);
        }
    }

    /// <summary>
    /// 저장 버튼 클릭 시 호출됩니다.
    /// </summary>
    private void OnClick_Save()
    {
        if (currentPreset == null || currentGroup == null) return;

        // 1. 역할 정보 저장
        currentGroup.memberRoles[currentPreset.presetID] = roleInputField.text;
        
        // 2. 이중 딕셔너리가 없으면 새로 생성
        if (!currentGroup.memberRelationships.ContainsKey(currentPreset.presetID))
        {
            currentGroup.memberRelationships[currentPreset.presetID] = new Dictionary<string, string>();
        }

        // 3. 동적으로 생성된 모든 관계 UI 아이템을 순회하며 데이터 저장
        foreach (var itemUI in spawnedRelationshipItems)
        {
            string targetId = itemUI.TargetPresetID;
            string relationshipText = itemUI.GetCurrentRelationshipInput();
            
            // 현재 주체(currentPreset)가 대상(targetId)에 대해 어떻게 생각하는지 저장
            currentGroup.memberRelationships[currentPreset.presetID][targetId] = relationshipText;
        }
        
        Debug.Log($"'{currentGroup.groupName}' 그룹의 '{currentPreset.characterName}' 역할/관계 정보 저장 완료.");

        // 변경사항 즉시 파일에 저장 (SaveController가 있다면)
        FindObjectOfType<SaveController>()?.SaveEverything();
        
        LocalizationManager.Instance.ShowWarning("적용문구");
    }
}
// --- END OF FILE GroupMemberDetailPanelController.cs ---