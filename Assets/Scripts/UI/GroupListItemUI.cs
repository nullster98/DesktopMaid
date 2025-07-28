// --- START OF FILE GroupListItemUI.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class GroupListItemUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image groupIcon;
    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private Button selfButton; 
    [SerializeField] private Button expandButton;
    [SerializeField] private Image expandBtnIcon;
    [SerializeField] private Sprite expandBtnIconOn;
    [SerializeField] private Sprite expandBtnIconOff;
    [SerializeField] private LayoutElement indentLayoutElement;
    [SerializeField] private Sprite defaultGroupIcon;

    private Color selectedColor;
    private Color deselectedColor;
    private Color originalColor;
    public CharacterGroup AssignedGroup { get; private set; }
    private GroupPanelController panelController;
    private bool isIconRuntime = false;

    private const float BASE_PADDING = 5f;
    private const float INDENT_PER_DEPTH = 20f;

    public void Setup(CharacterGroup group, int depth, GroupPanelController controller,
        Color selColor, Color deselColor, bool isExpanded, bool hasChildren)
    {
        this.AssignedGroup = group;
        this.panelController = controller;
        this.selectedColor = selColor;
        this.deselectedColor = deselColor;
        this.originalColor = backgroundImage.color;

        groupNameText.text = group.groupName;
        indentLayoutElement.preferredWidth = BASE_PADDING + (depth * INDENT_PER_DEPTH);
        SetIcon(group.groupSymbol_Base64);

        expandButton.gameObject.SetActive(hasChildren);
        if (hasChildren)
        {
            expandBtnIcon.sprite = isExpanded ? expandBtnIconOn : expandBtnIconOff;
        }
        
        selfButton.onClick.RemoveAllListeners();
        expandButton.onClick.RemoveAllListeners();
        
        // 버튼 클릭은 '선택' 기능만 담당하도록 합니다.
        selfButton.onClick.AddListener(OnItemClicked);
        expandButton.onClick.AddListener(OnExpandCollapseClicked);
    }

    // 이 함수는 이제 '선택'만 담당합니다.
    private void OnItemClicked()
    {
        panelController.SelectGroup(AssignedGroup);
    }
    
    // IPointerClickHandler 인터페이스 구현. 더블클릭을 감지합니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        // clickCount를 사용하면 더블클릭 감지가 더 간편하고 정확합니다.
        if (eventData.clickCount == 2)
        {
            // 더블클릭 시, 컨트롤러에게 채팅창을 열어달라고 요청합니다.
            panelController.OpenOrFocusGroupChatWindow(AssignedGroup);
        }
    }

    public void SetSelected(bool isSelected)
    {
        this.originalColor = isSelected ? selectedColor : deselectedColor;
        backgroundImage.color = this.originalColor;
    }
    
    private void OnExpandCollapseClicked()
    {
        panelController.ToggleGroupExpansion(AssignedGroup.groupID);
    }
    
    private void SetIcon(string base64String)
    {
        if (isIconRuntime && groupIcon.sprite != null)
        {
            Destroy(groupIcon.sprite.texture);
            Destroy(groupIcon.sprite);
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
                    groupIcon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIcon.gameObject.SetActive(true);
                    isIconRuntime = true;
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GroupListItemUI: Base64 아이콘 디코딩 실패 (ID: {AssignedGroup.groupID}): {e.Message}");
            }
        }
        groupIcon.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PresetListItemUI.draggedPreset != null && PresetListItemUI.draggedPreset.groupID != AssignedGroup.groupID)
        {
            backgroundImage.color = new Color(0.4f, 0.6f, 0.8f, 0.5f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        backgroundImage.color = originalColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        backgroundImage.color = originalColor;

        CharacterPreset presetToDrop = PresetListItemUI.draggedPreset;
        if (presetToDrop != null)
        {
            // 드롭된 캐릭터의 설정이 비어있는지 확인합니다.
            if (string.IsNullOrWhiteSpace(presetToDrop.characterSetting))
            {
                // 설정이 비어있으면 경고창을 띄우고,
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.CharacterSetting);
                // 드롭 로직을 중단하고 드래그 상태를 정리합니다.
                PresetListItemUI.EndDragCleanup();
                return; // 여기서 함수를 종료하여 그룹에 추가되지 않도록 합니다.
            }
            
            // [해결책] 드롭 전에 이전 그룹 ID를 기억합니다.
            string previousGroupID = presetToDrop.groupID;

            if (presetToDrop.groupID == this.AssignedGroup.groupID)
            {
                Debug.Log("이미 같은 그룹의 멤버입니다.");
                PresetListItemUI.EndDragCleanup(); // 드래그 상태 정리 추가
                return;
            }

            CharacterGroupManager.Instance.AddMemberToGroup(presetToDrop.presetID, this.AssignedGroup.groupID);

            // 왼쪽 리스트는 항상 새로고침합니다.
            panelController.RefreshGroupListUI();
        
            // 드롭된 그룹의 상세 패널을 새로고침합니다.
            panelController.RefreshDetailPanelIfShowingByID(this.AssignedGroup.groupID);
        
            // [해결책] 프리셋이 원래 속해있던 그룹의 상세 패널도 새로고침합니다.
            if (!string.IsNullOrEmpty(previousGroupID))
            {
                panelController.RefreshDetailPanelIfShowingByID(previousGroupID);
            }
        }

        PresetListItemUI.EndDragCleanup();
    }
}
// --- END OF FILE GroupListItemUI.cs ---