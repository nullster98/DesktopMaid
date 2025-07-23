// --- START OF FILE GroupDetailPanelController.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SFB;
using System.Collections;
using System.IO;

public class GroupDetailPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField groupNameField;
    [SerializeField] private TMP_InputField groupConceptField;
    [SerializeField] private TMP_InputField groupDescriptionField;
    [SerializeField] private Button saveChangesButton;
    [SerializeField] private Button deleteGroupButton;
    [SerializeField] private Image groupIconImage;
    [SerializeField] private Button changeIconButton;
    [SerializeField] private Sprite defaultGroupIcon; 
    
    [Header("Member List References")] 
    [SerializeField] private Transform memberListContentParent;
    [SerializeField] private GameObject groupMemberListItemPrefab;
    [SerializeField] private GameObject addMemberButtonPrefab;

    [Header("Popup References")] 
    [SerializeField] private AddMemberPopupController addMemberPopup;

    private CharacterGroup currentEditingGroup;
    private GroupPanelController mainPanelController;
    private bool isIconRuntime = false;

    // ... Initialize, LoadGroupData 및 다른 함수들은 이전과 동일하게 유지 ...
    public void Initialize(GroupPanelController mainController)
    {
        this.mainPanelController = mainController;
        saveChangesButton.onClick.AddListener(OnClick_SaveChanges);
        deleteGroupButton.onClick.AddListener(OnClick_DeleteGroup);
        changeIconButton.onClick.AddListener(OnClick_ChangeGroupIcon);
    }

    public void LoadGroupData(CharacterGroup group)
    {
        this.currentEditingGroup = group;

        if (group == null)
        {
            gameObject.SetActive(false);
            return;
        }

        groupNameField.text = group.groupName;
        groupConceptField.text = group.groupConcept;
        groupDescriptionField.text = group.groupDescription;
        
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

        if (addMemberButtonPrefab != null)
        {
            GameObject addBtnGO = Instantiate(addMemberButtonPrefab, memberListContentParent);
            Button btn = addBtnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnClick_AddMember);
            }
        }
    }

    private void RequestRemoveMember(CharacterPreset presetToRemove)
    {
        if (presetToRemove == null) return;
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

    private void OnClick_SaveChanges()
    {
        if (currentEditingGroup == null) return;
        currentEditingGroup.groupName = groupNameField.text;
        currentEditingGroup.groupConcept = groupConceptField.text;
        currentEditingGroup.groupDescription = groupDescriptionField.text;

        if (groupIconImage.sprite != null && groupIconImage.sprite != defaultGroupIcon)
        {
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
            currentEditingGroup.groupSymbol_Base64 = null;
        }

        mainPanelController.RefreshActiveGroupChat(currentEditingGroup);
        mainPanelController.RefreshGroupListUI();
        
        SaveController saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
        }
    }

    private void OnClick_DeleteGroup()
    {
        if (currentEditingGroup == null) return;
        CharacterGroupManager.Instance.DeleteGroup(currentEditingGroup.groupID);
        mainPanelController.RefreshGroupListUI();
        mainPanelController.SelectGroup(null);
    }
    
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
    /// [수정됨] 선택된 이미지 파일을 바이트 배열로 불러와 UI에 적용하는 코루틴입니다.
    /// 이 방법은 원본 파일이 프로젝트 애셋이어도 안전하게 처리합니다.
    /// </summary>
    private IEnumerator LoadGroupIconCoroutine(string path)
    {
        string url = "file://" + path;
        using (WWW www = new WWW(url))
        {
            yield return www;

            if (string.IsNullOrEmpty(www.error))
            {
                // [수정] www.texture 대신 www.bytes를 사용합니다.
                byte[] imageBytes = www.bytes;
                
                // 새 텍스처를 생성하고 바이트 데이터를 로드합니다.
                // 이 과정은 텍스처가 항상 파괴 가능한 런타임 인스턴스임을 보장합니다.
                Texture2D tex = new Texture2D(2, 2);
                
                if (tex.LoadImage(imageBytes))
                {
                    // 기존에 런타임으로 생성된 아이콘이 있다면 안전하게 파괴합니다.
                    if (isIconRuntime && groupIconImage.sprite != null)
                    {
                        // 텍스처와 스프라이트가 모두 존재할 때만 파괴
                        if (groupIconImage.sprite.texture != null)
                        {
                            Destroy(groupIconImage.sprite.texture);
                        }
                        Destroy(groupIconImage.sprite);
                    }
                    
                    // 새로 생성된 런타임 텍스처로 새 스프라이트를 만듭니다.
                    Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIconImage.sprite = newSprite;
                    isIconRuntime = true; // 런타임에 생성되었음을 명시
                }
                else
                {
                    Debug.LogError("이미지 데이터를 텍스처로 로드하는 데 실패했습니다.");
                }
            }
            else
            {
                Debug.LogError($"이미지 로드 실패: {www.error}");
            }
        }
    }
    
    private void LoadIconFromBase64(string base64String)
    {
        if (isIconRuntime && groupIconImage.sprite != null)
        {
            if (groupIconImage.sprite.texture != null)
            {
                Destroy(groupIconImage.sprite.texture);
            }
            Destroy(groupIconImage.sprite);
        }
        isIconRuntime = false;

        if (!string.IsNullOrEmpty(base64String))
        {
            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(base64String);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(imageBytes)) 
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIconImage.sprite = sprite;
                    isIconRuntime = true;
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Base64 아이콘 디코딩 실패: {e.Message}");
            }
        }
        
        groupIconImage.sprite = defaultGroupIcon;
    }
}
// --- END OF FILE GroupDetailPanelController.cs ---