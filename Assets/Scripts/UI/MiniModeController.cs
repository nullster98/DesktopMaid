// MiniModeController.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MiniModeController : MonoBehaviour
{
    public static MiniModeController Instance { get; private set; }

    [Header("UI 연결")]
    [Tooltip("미니 모드 프리팹들이 생성될 부모 Transform (ScrollView의 Content)")]
    [SerializeField] private Transform contentParent; 
    [SerializeField] private GameObject miniPresetItemPrefab;
    
    [Header("Main Mode Link")]
    [Tooltip("메인 모드의 프리셋들이 있는 ScrollView Content Transform")]
    [SerializeField] private Transform mainModeContentParent;

    private List<MiniModeItem> _miniItems = new List<MiniModeItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (contentParent == null)
        {
            Debug.LogError("[MiniModeController] 'Content Parent'가 설정되지 않았습니다! ScrollView의 Content 오브젝트를 연결해주세요.");
        }
    }
    
    // 모든 캐릭터 프리셋을 기반으로 미니 모드 UI를 재생성
    public void RefreshAllItems()
    {
        if (contentParent == null || miniPresetItemPrefab == null || mainModeContentParent == null)
        {
            Debug.LogError("MiniModeController에 필요한 참조가 설정되지 않았습니다!");
            return;
        }
        
        Debug.Log("[MiniModeController] 메인 모드의 UI 순서를 기반으로 미니모드 아이템 목록을 새로고침합니다.");

        // 기존 미니모드 아이템들 삭제
        foreach (Transform child in contentParent.transform)
        {
            Destroy(child.gameObject);
        }
        _miniItems.Clear();

        // [핵심 변경] CharacterPresetManager의 데이터 리스트가 아닌,
        // 메인 모드의 UI 계층(Hierarchy)을 직접 순회합니다.
        foreach (Transform mainModeItemTransform in mainModeContentParent)
        {
            // 메인 모드 UI 오브젝트에서 CharacterPreset 컴포넌트를 가져옵니다.
            CharacterPreset preset = mainModeItemTransform.GetComponent<CharacterPreset>();

            if (preset != null)
            {
                // 이 프리셋을 기반으로 미니모드 아이템을 생성합니다.
                GameObject newItemObj = Instantiate(miniPresetItemPrefab, contentParent);
                MiniModeItem newItem = newItemObj.GetComponent<MiniModeItem>();
                newItem.LinkPreset(preset);
                _miniItems.Add(newItem);
            }
        }
    }

    // // 특정 프리셋에 대한 아이템 생성
    // public void CreateItemForPreset(CharacterPreset preset)
    // {
    //     if (contentParent == null) return; // contentParent가 없으면 실행하지 않음
    //
    //     Debug.Log($"[MiniModeController] Creating item for: {preset.characterName} (ID: {preset.presetID})");
    //
    //     GameObject newItemObj = Instantiate(miniPresetItemPrefab);
    //     // [수정] 부모를 contentParent로 명확히 지정
    //     newItemObj.transform.SetParent(contentParent, false);
    //
    //     MiniModeItem newItem = newItemObj.GetComponent<MiniModeItem>();
    //     newItem.LinkPreset(preset);
    //     _miniItems.Add(newItem);
    // }

    // 특정 프리셋에 대한 아이템 삭제
    public void RemoveItemForPreset(string presetId)
    {
        MiniModeItem itemToRemove = _miniItems.FirstOrDefault(i => i.presetID == presetId);
        if (itemToRemove != null)
        {
            _miniItems.Remove(itemToRemove);
            Destroy(itemToRemove.gameObject);
        }
    }

    // 특정 프리셋 아이템의 UI 업데이트 요청
    public void UpdateItemUI(string presetId)
    {
        MiniModeItem itemToUpdate = _miniItems.FirstOrDefault(i => i.presetID == presetId);
        if (itemToUpdate != null)
        {
            itemToUpdate.UpdateAllUI();
        }
    }
    
    // 특정 프리셋을 목록 최상단으로 이동
    public void MoveItemToTop(string presetId)
    {
        MiniModeItem itemToMove = _miniItems.FirstOrDefault(i => i.presetID == presetId);
        if (itemToMove != null)
        {
            itemToMove.MoveToTop();
        }
    }
}