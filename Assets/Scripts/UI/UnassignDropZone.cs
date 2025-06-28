using UnityEngine;
using UnityEngine.EventSystems;

public class UnassignDropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private GroupPanelController mainPanelController;

    public void OnDrop(PointerEventData eventData)
    {
        CharacterPreset presetToUnassign = PresetListItemUI.draggedPreset;
        
        // [수정!] 이전에 속해있던 그룹의 ID를 저장할 변수
        string previousGroupID = null;

        // 실제로 그룹에서 제외할 프리셋이 있는지 먼저 확인
        if (presetToUnassign != null && !string.IsNullOrEmpty(presetToUnassign.groupID))
        {
            // [핵심!] 그룹에서 제거하기 전에, 이전 그룹 ID를 기억해 둡니다.
            previousGroupID = presetToUnassign.groupID;

            Debug.Log($"'{presetToUnassign.characterName}'을(를) 그룹 '{previousGroupID}'에서 제외합니다.");
            CharacterGroupManager.Instance.RemoveMemberFromGroup(presetToUnassign.presetID);
            
            // 만약 이전에 속해있던 그룹이 있었다면, 그 그룹의 오른쪽 패널을 새로고침하도록 요청합니다.
            if (!string.IsNullOrEmpty(previousGroupID))
            {
                // GroupPanelController의 헬퍼 함수를 호출합니다.
                mainPanelController.RefreshDetailPanelIfShowingByID(previousGroupID);
            }

            // 왼쪽 패널은 항상 새로고침합니다.
            mainPanelController.RefreshGroupListUI();
            

        }
        
        // 마지막으로, 드래그 상태를 정리합니다.
        PresetListItemUI.EndDragCleanup();
    }
}