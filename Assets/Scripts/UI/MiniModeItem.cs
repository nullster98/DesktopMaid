// MiniModeItem.cs - 프리팹에 붙일 간단한 컴포넌트
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MiniModeItem : MonoBehaviour
{
    [Header("UI")]
    public string presetID;
    public Image characterImage;
    public Image modeImage;
    public Image vrmStatusImage;
    public GameObject notifyImage;
    public GameObject lockOverlay;
    
    [Header("Buttons")]
    public Button mainButton;
    public Button modeCycleButton;
    public Button vrmToggleButton;

    private CharacterPreset _linkedPreset;

    public void LinkPreset(CharacterPreset preset)
    {
        _linkedPreset = preset;
        if (_linkedPreset == null) return;
        
        presetID = preset.presetID;
        characterImage.sprite = preset.characterImage.sprite;

        // 1. 메인 버튼: 채팅 UI 열기
        if (mainButton != null)
        {
            mainButton.onClick.RemoveAllListeners();
            mainButton.onClick.AddListener(() => _linkedPreset.OnClickPresetButton());
        }

        // 2. 컨디션 모드 전환 버튼
        if (modeCycleButton != null)
        {
            modeCycleButton.onClick.RemoveAllListeners();
            modeCycleButton.onClick.AddListener(() => _linkedPreset.CycleCharacterMode());
        }

        // 3. VRM 보이기/숨기기 토글 버튼
        if (vrmToggleButton != null)
        {
            vrmToggleButton.onClick.RemoveAllListeners();
            vrmToggleButton.onClick.AddListener(() => _linkedPreset.ToggleVrmVisibility());
        }
        
        // CharacterPreset의 이벤트 구독 (상태가 바뀔 때마다 UI 자동 업데이트)
        _linkedPreset.OnVrmStateChanged -= UpdateVrmIcon; // 중복 구독 방지
        _linkedPreset.OnVrmStateChanged += UpdateVrmIcon;

        UpdateAllUI();
    }
    
    public void UpdateLockState()
    {
        if (_linkedPreset == null || lockOverlay == null) return;
        
        bool isLocked = _linkedPreset.isLocked;
        lockOverlay.SetActive(isLocked);

        // 잠겨있을 때는 모든 버튼 비활성화
        if (mainButton != null) mainButton.interactable = !isLocked;
        if (modeCycleButton != null) modeCycleButton.interactable = !isLocked;
        if (vrmToggleButton != null) vrmToggleButton.interactable = !isLocked;
    }
    
    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 이벤트 구독 해제 (메모리 누수 방지)
        if (_linkedPreset != null)
        {
            _linkedPreset.OnVrmStateChanged -= UpdateVrmIcon;
        }
    }
    public void UpdateVrmIcon()
    {
        if (_linkedPreset == null || vrmStatusImage == null) return;

        vrmStatusImage.sprite = _linkedPreset.isVrmVisible 
            ? UIManager.instance.vrmVisibleSprite 
            : UIManager.instance.vrmInvisibleSprite;
    }
    
    // 모든 UI를 한번에 업데이트 하는 헬퍼 함수
    public void UpdateAllUI()
    {
        if (_linkedPreset == null) return;
        UpdateModeIcon();
        UpdateVrmIcon();
        UpdateNotifyDot();
        UpdateLockState();
    }

    public void UpdateModeIcon()
    {
        if (_linkedPreset == null) return;
        switch (_linkedPreset.CurrentMode)
        {
            case CharacterMode.Activated:
                modeImage.sprite = UIManager.instance.modeOnSprite;
                modeImage.color = Color.green; // 예시 색상
                break;
            case CharacterMode.Sleep:
                modeImage.sprite = UIManager.instance.modeSleepSprite;
                modeImage.color = Color.yellow; // 예시 색상
                break;
            case CharacterMode.Off:
                modeImage.sprite = UIManager.instance.modeOffSprite;
                modeImage.color = Color.grey; // 예시 색상
                break;
        }
    }

    public void UpdateNotifyDot()
    {
        if (_linkedPreset == null || notifyImage == null) return;
        // CharacterPreset의 notifyImage GameObject의 활성 상태를 직접 따라감
        notifyImage.SetActive(_linkedPreset.notifyImage.activeSelf);
    }
    
    public void MoveToTop()
    {
        transform.SetAsFirstSibling();
    }
}