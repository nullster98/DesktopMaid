using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Action을 사용하기 위해 추가

public class GroupMemberListItemUI : MonoBehaviour
{
    [SerializeField] private Image characterIcon;
    [SerializeField] private Button removeButton;

    private CharacterPreset assignedPreset;
    private Action<CharacterPreset> onRemoveClicked;

    public void Setup(CharacterPreset preset, Action<CharacterPreset> removeCallback)
    {
        this.assignedPreset = preset;
        this.onRemoveClicked = removeCallback;
        
        if (preset.characterImage != null)
            characterIcon.sprite = preset.characterImage.sprite;

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveButtonClicked);
    }

    private void OnRemoveButtonClicked()
    {
        // 내가 제거되어야 함을 컨트롤러에게 알림
        onRemoveClicked?.Invoke(assignedPreset);
    }
}