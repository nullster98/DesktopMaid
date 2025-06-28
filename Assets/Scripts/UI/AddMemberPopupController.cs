using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

// 팝업이 닫힐 때, 선택된 프리셋 정보를 전달하기 위한 콜백 delegate
public delegate void OnAddMembersConfirmed(List<CharacterPreset> selectedPresets);

public class AddMemberPopupController : MonoBehaviour
{
    [SerializeField] private Transform candidateListContentParent;
    [SerializeField] private GameObject candidateItemPrefab;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private CharacterGroup targetGroup;
    private List<CharacterPreset> selectedCandidates = new List<CharacterPreset>();
    private OnAddMembersConfirmed onConfirmCallback;

    private void Start()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(ClosePopup);
    }

    // 팝업을 열 때 호출될 메인 함수
    public void OpenPopup(CharacterGroup group, OnAddMembersConfirmed callback)
    {
        this.targetGroup = group;
        this.onConfirmCallback = callback;
        this.selectedCandidates.Clear();

        RefreshCandidateList();
        gameObject.SetActive(true);
    }

    private void RefreshCandidateList()
    {
        foreach (Transform child in candidateListContentParent) Destroy(child.gameObject);

        // 어떤 그룹에도 속하지 않은 '무소속' 프리셋만 후보로 표시
        var candidates = CharacterPresetManager.Instance.presets
            .Where(p => string.IsNullOrEmpty(p.groupID))
            .ToList();

        foreach (var candidate in candidates)
        {
            // TODO: 후보 아이템 UI 스크립트(AddMemberCandidateItemUI)를 만들어 연결
             GameObject itemGO = Instantiate(candidateItemPrefab, candidateListContentParent);
             itemGO.GetComponent<AddMemberCandidateItemUI>().Setup(candidate, OnCandidateSelected);
        }
    }
    
    // (UI 아이템이 호출) 후보가 선택/해제되었을 때
    private void OnCandidateSelected(CharacterPreset preset, bool isSelected)
    {
        if (isSelected)
        {
            if (!selectedCandidates.Contains(preset)) selectedCandidates.Add(preset);
        }
        else
        {
            selectedCandidates.Remove(preset);
        }
    }

    // '추가' 버튼 클릭 시
    private void OnConfirm()
    {
        // 콜백을 통해 선택된 후보 목록을 GroupDetailPanelController에게 전달
        onConfirmCallback?.Invoke(selectedCandidates);
        ClosePopup();
    }

    private void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}