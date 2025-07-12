// --- START OF FILE GroupDetailPanelController.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SFB; // [추가] 파일 브라우저 사용을 위해
using System.Collections; // [추가] 코루틴 사용을 위해
using System.IO; // [추가] 파일 읽기를 위해

public class GroupDetailPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField groupNameField;
    [SerializeField] private TMP_InputField groupConceptField;
    [SerializeField] private TMP_InputField groupDescriptionField;
    [SerializeField] private Button saveChangesButton;
    [SerializeField] private Button deleteGroupButton;
    // [수정] 그룹 아이콘, 모토 UI 참조 추가
    [SerializeField] private Image groupIconImage;
    [SerializeField] private Button changeIconButton;
    [SerializeField] private Sprite defaultGroupIcon; // [추가] 기본 아이콘 (인스펙터에서 할당)
    
    [Header("Member List References")] 
    [SerializeField] private Transform memberListContentParent;
    [SerializeField] private GameObject groupMemberListItemPrefab;
    [SerializeField] private Button addMemberButton;

    [Header("Popup References")] 
    [SerializeField] private AddMemberPopupController addMemberPopup;

    private CharacterGroup currentEditingGroup;
    private GroupPanelController mainPanelController;

    public void Initialize(GroupPanelController mainController)
    {
        this.mainPanelController = mainController;
        saveChangesButton.onClick.AddListener(OnClick_SaveChanges);
        deleteGroupButton.onClick.AddListener(OnClick_DeleteGroup);
        addMemberButton.onClick.AddListener(OnClick_AddMember);
        
        // [추가] 아이콘 변경 버튼 리스너 연결
        changeIconButton.onClick.AddListener(OnClick_ChangeGroupIcon);
    }

    // GroupPanelController가 호출해 줄 함수
    public void LoadGroupData(CharacterGroup group)
    {
        this.currentEditingGroup = group;

        if (group == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 받아온 데이터로 각 UI 필드를 채움
        groupNameField.text = group.groupName;
        groupConceptField.text = group.groupConcept;
        groupDescriptionField.text = group.groupDescription;
        
        // [추가] 아이콘 로드
        LoadIconFromBase64(group.groupSymbol_Base64);
        
        RefreshMemberListUI();
    }
    
    public void RefreshMemberListUI()
    {
        foreach (Transform child in memberListContentParent) Destroy(child.gameObject);

        if (currentEditingGroup == null) return;

        var members = CharacterGroupManager.Instance.GetGroupMembers(currentEditingGroup.groupID);
        foreach (var member in members)
        {
            GameObject itemGO = Instantiate(groupMemberListItemPrefab, memberListContentParent);
            itemGO.GetComponent<GroupMemberListItemUI>().Setup(member, RequestRemoveMember);
        }
    }

    private void RequestRemoveMember(CharacterPreset presetToRemove)
    {
        if (presetToRemove == null) return;

        Debug.Log($"'{presetToRemove.characterName}'을 그룹에서 제거합니다.");
        CharacterGroupManager.Instance.RemoveMemberFromGroup(presetToRemove.presetID);

        RefreshMemberListUI(); 
        mainPanelController.RefreshGroupListUI();
    }
    
    private void OnClick_AddMember()
    {
        if (currentEditingGroup == null) return;
        
        addMemberPopup.OpenPopup(currentEditingGroup, (selectedList) => {
            foreach(var preset in selectedList)
            {
                CharacterGroupManager.Instance.AddMemberToGroup(preset.presetID, currentEditingGroup.groupID);
            }
            
            RefreshMemberListUI();
            mainPanelController.RefreshGroupListUI();
        });
    }

    // '변경사항 저장' 버튼 클릭 시
    private void OnClick_SaveChanges()
    {
        if (currentEditingGroup == null) return;

        // UI 필드의 현재 값들을 실제 데이터에 저장
        currentEditingGroup.groupName = groupNameField.text;
        currentEditingGroup.groupConcept = groupConceptField.text;
        currentEditingGroup.groupDescription = groupDescriptionField.text;

        // [추가] 아이콘 이미지 데이터를 Base64로 인코딩하여 저장
        if (groupIconImage.sprite != null && groupIconImage.sprite != defaultGroupIcon)
        {
            // isReadable 속성이 true인 텍스처만 인코딩 가능
            try
            {
                Texture2D tex = groupIconImage.sprite.texture;
                if (tex.isReadable)
                {
                    byte[] bytes = tex.EncodeToPNG();
                    currentEditingGroup.groupSymbol_Base64 = System.Convert.ToBase64String(bytes);
                }
                else
                {
                    Debug.LogWarning("아이콘 텍스처를 읽을 수 없어(isReadable=false) 저장할 수 없습니다. Import Settings에서 'Read/Write Enabled'를 체크해주세요.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"아이콘 저장 중 오류 발생: {e.Message}");
            }
        }
        else
        {
            // 기본 아이콘이거나 아이콘이 없는 경우, Base64 데이터를 비웁니다.
            currentEditingGroup.groupSymbol_Base64 = null;
        }

        mainPanelController.RefreshActiveGroupChat(currentEditingGroup);
        
        Debug.Log($"'{currentEditingGroup.groupName}' 그룹 정보 저장 완료.");
        
        mainPanelController.RefreshGroupListUI();
        
        SaveController saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
            Debug.Log($"'{currentEditingGroup.groupName}' 그룹 정보 즉시 저장 완료.");
        }
        else
        {
            Debug.LogWarning("SaveController를 찾을 수 없어 즉시 저장을 스킵했습니다. 자동 저장됩니다.");
        }
    }

    // '그룹 삭제' 버튼 클릭 시
    private void OnClick_DeleteGroup()
    {
        if (currentEditingGroup == null) return;

        CharacterGroupManager.Instance.DeleteGroup(currentEditingGroup.groupID);
        Debug.Log($"'{currentEditingGroup.groupName}' 그룹 삭제 완료.");

        mainPanelController.RefreshGroupListUI();
        mainPanelController.SelectGroup(null);
    }
    
    // --- [신규] 아이콘 변경 관련 로직 ---
    
    /// <summary>
    /// 아이콘 변경 버튼 클릭 시 파일 브라우저를 엽니다.
    /// </summary>
    private void OnClick_ChangeGroupIcon()
    {
        var extensions = new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("그룹 아이콘 선택", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            StartCoroutine(LoadGroupIconCoroutine(paths[0]));
        }
    }

    /// <summary>
    /// 선택된 이미지 파일을 불러와 UI에 적용하는 코루틴입니다.
    /// </summary>
    private IEnumerator LoadGroupIconCoroutine(string path)
    {
        // 파일 경로에 file:// 프리픽스를 붙여 WWW가 인식할 수 있는 URL로 만듭니다.
        string url = "file://" + path;

        using (WWW www = new WWW(url))
        {
            yield return www;

            if (string.IsNullOrEmpty(www.error))
            {
                // 성공적으로 로드된 경우
                Texture2D tex = www.texture;
                if (tex != null)
                {
                    // 기존 스프라이트가 있다면 메모리 누수 방지를 위해 파괴
                    if (groupIconImage.sprite != null && groupIconImage.sprite != defaultGroupIcon)
                    {
                        Destroy(groupIconImage.sprite.texture);
                        Destroy(groupIconImage.sprite);
                    }
                    
                    // 새 스프라이트 생성 및 할당
                    Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIconImage.sprite = newSprite;
                }
            }
            else
            {
                Debug.LogError($"이미지 로드 실패: {www.error}");
            }
        }
    }
    
    /// <summary>
    /// Base64 문자열로부터 아이콘을 로드하여 UI에 적용합니다.
    /// </summary>
    private void LoadIconFromBase64(string base64String)
    {
        // 기존 스프라이트가 있다면 메모리 누수 방지를 위해 파괴
        if (groupIconImage.sprite != null && groupIconImage.sprite != defaultGroupIcon)
        {
            Destroy(groupIconImage.sprite.texture);
            Destroy(groupIconImage.sprite);
        }

        if (!string.IsNullOrEmpty(base64String))
        {
            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(base64String);
                Texture2D tex = new Texture2D(2, 2);
                // LoadImage는 텍스처를 자동으로 리사이즈하고 isReadable=false로 만듭니다.
                // 저장을 위해서는 isReadable=true가 필요하지만, 불러오기만 할 때는 상관없습니다.
                if (tex.LoadImage(imageBytes)) 
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIconImage.sprite = sprite;
                    return; // 성공적으로 로드했으면 함수 종료
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Base64 아이콘 디코딩 실패: {e.Message}");
            }
        }

        // Base64 문자열이 없거나 로드에 실패하면 기본 아이콘으로 설정
        groupIconImage.sprite = defaultGroupIcon;
    }
}
// --- END OF FILE GroupDetailPanelController.cs ---