using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class PresetListItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image backgroundImage;
    
    // 이 스크립트가 제어할 UI 요소들을 변수로 선언합니다.
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Button selfButton; // 아이템 자기 자신에 붙어있는 버튼
    [SerializeField] private LayoutElement indentLayoutElement;

    // 나중에 데이터를 받아와서 채워넣을 변수들
    private Color selectedColor;
    private Color deselectedColor;
    public CharacterPreset AssignedPreset { get; private set; }
    private GroupPanelController panelController;
    
    public static CharacterPreset draggedPreset = null;
    private static GameObject draggedItemVisual = null;

    // GroupPanelController가 이 아이템을 생성할 때 호출해 줄 함수
    public void Setup(CharacterPreset preset, int depth, GroupPanelController controller,
        Color selColor, Color deselColor)
    {
        this.AssignedPreset = preset;
        this.panelController = controller;
        
        this.selectedColor =  selColor;
        this.deselectedColor = deselColor;

        // 이름 설정
        characterNameText.text = preset.characterName;

        // 아이콘 설정 (캐릭터의 프로필 이미지를 사용)
        if (preset.characterImage != null && preset.characterImage.sprite != null)
        {
            characterIcon.sprite = preset.characterImage.sprite;
        }

        // 들여쓰기 설정 (부모 그룹의 depth + 1)
        indentLayoutElement.preferredWidth = depth * 20f;

        // 클릭 이벤트를 코드에서 연결
        if (selfButton != null)
        {
            selfButton.onClick.RemoveAllListeners();
            selfButton.onClick.AddListener(OnItemClicked);
        }
    }

    // 아이템이 클릭되었을 때 실행될 함수
    private void OnItemClicked()
    {
        // 메인 컨트롤러에게 "캐릭터 프리셋이 선택되었어!" 라고 알림
        // panelController에게 캐릭터 선택을 처리하는 함수를 만들어 호출할 수 있습니다.
        panelController.SelectPreset(this.AssignedPreset);
    }

    public void SetSelected(bool isSelected)
    {
        if (backgroundImage != null)
        {
            // 저장해둔 색상 정보를 사용하여 배경색을 변경합니다.
            backgroundImage.color = isSelected ? selectedColor : deselectedColor;
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 이미 다른 아이템이 드래그 중이면 무시
        if (draggedItemVisual != null) return;

        Debug.Log($"Drag 시작: {AssignedPreset.characterName}");

        // 1. 현재 드래그 시작된 프리셋 정보를 static 변수에 저장
        draggedPreset = this.AssignedPreset;

        // 2. 마우스를 따라다닐 '유령 이미지' 생성
        //    (자기 자신을 복제해서 만듦)
        draggedItemVisual = Instantiate(this.gameObject, transform.root); // transform.root는 Canvas를 의미
        
        draggedItemVisual.transform.SetAsLastSibling();
        
        // 3. 유령 이미지가 마우스 클릭을 방해하지 않도록 설정
        CanvasGroup canvasGroup = draggedItemVisual.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; // 레이캐스트 통과
        canvasGroup.alpha = 0.7f; // 반투명하게 만들어 유령처럼 보이게 함
    }

    // --- [신규!] 드래그하는 동안 매 프레임 호출되는 함수 ---
    public void OnDrag(PointerEventData eventData)
    {
        if (draggedItemVisual == null) return;

        // 1. 유령 이미지의 부모(Canvas)의 RectTransform을 가져옵니다.
        RectTransform canvasRectTransform = draggedItemVisual.transform.root as RectTransform;

        // 2. 화면 좌표(eventData.position)를 Canvas의 로컬 좌표로 변환합니다.
        //    'eventData.pressEventCamera'는 이 UI 이벤트를 감지한 카메라를 의미하며,
        //    Screen Space - Camera 모드에서는 Canvas에 설정된 'Render Camera'가 됩니다.
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPointerPosition
            ))
        {
            // 3. 변환된 로컬 좌표를 유령 이미지의 localPosition에 설정합니다.
            draggedItemVisual.transform.localPosition = localPointerPosition;
        }
    }
    
    public static void EndDragCleanup()
    {
        Debug.Log("드래그 상태를 정리합니다...");
        draggedPreset = null;
        if (draggedItemVisual != null)
        {
            Destroy(draggedItemVisual);
        }
        draggedItemVisual = null;
    }

    // --- [신규!] 드래그가 끝났을 때 1번 호출되는 함수 ---
    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("Drag 종료 (OnEndDrag에서 호출)");
        EndDragCleanup();
    }
}