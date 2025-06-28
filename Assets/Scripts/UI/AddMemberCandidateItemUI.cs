using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[RequireComponent(typeof(Toggle))] // 이 스크립트에는 Toggle 컴포넌트가 반드시 필요함을 명시
public class AddMemberCandidateItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image characterIcon;
    
    
    // --- 내부 변수 ---
    [SerializeField] private Toggle selfToggle;
    private CharacterPreset assignedPreset;
    private Action<CharacterPreset, bool> onSelectionChanged;

    // 스크립트가 처음 로드될 때 한 번만 호출
    private void Awake()
    {
        selfToggle = GetComponent<Toggle>();
    }

    public void Setup(CharacterPreset preset, Action<CharacterPreset, bool> selectionCallback)
    {
        this.assignedPreset = preset;
        this.onSelectionChanged = selectionCallback;
        
        if (preset.characterImage != null)
            characterIcon.sprite = preset.characterImage.sprite;
        
        // 토글 값이 변경될 때마다 OnToggleValueChanged 함수가 호출되도록 연결
        selfToggle.onValueChanged.RemoveAllListeners();
        selfToggle.onValueChanged.AddListener(OnToggleValueChanged);
        
        // 초기 상태는 비선택
        selfToggle.isOn = false;
    }

    // 토글의 체크 상태가 바뀔 때마다 호출
    private void OnToggleValueChanged(bool isOn)
    {
        // 컨트롤러에게 "나의 선택 상태가 'isOn'으로 바뀌었어!" 라고 알림
        onSelectionChanged?.Invoke(assignedPreset, isOn);
    }
}