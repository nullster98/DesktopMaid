// --- START OF FILE GroupListItemMainUI.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 메인 UI 리스트에 표시되는 '그룹' 아이템의 UI를 제어하는 스크립트입니다.
/// </summary>
public class GroupListItemMainUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image groupIcon;
    [SerializeField] private TMP_Text groupNameText;
    [SerializeField] private TMP_Text lastMessageText; // 마지막 메시지 표시용 (선택적)
    [SerializeField] private GameObject notificationDot; // Red Dot 알림
    [SerializeField] private Button selfButton;

    private CharacterGroup assignedGroup;
    private GroupPanelController groupPanelController;
    private bool isIconRuntime = false; // 런타임에 생성된 아이콘 리소스 관리를 위한 플래그
    
    public CharacterGroup AssignedGroup => assignedGroup;

    /// <summary>
    /// 이 UI 아이템을 특정 그룹 데이터와 연결하고 초기화합니다.
    /// </summary>
    /// <param name="group">표시할 그룹 데이터</param>
    /// <param name="controller">그룹 관련 동작(채팅창 열기 등)을 처리할 컨트롤러</param>
    public void Setup(CharacterGroup group, GroupPanelController controller)
    {
        this.assignedGroup = group;
        this.groupPanelController = controller;

        // UI 요소 업데이트
        groupNameText.text = group.groupName;
        SetIcon(group.groupSymbol_Base64);
        UpdateNotification(group.HasNotification); // HasNotification 속성 반영

        // TODO: 마지막 메시지 및 시간 업데이트 로직 추가
        // 예: UpdateLastMessage(group.lastMessage, group.lastInteractionTime);

        // 버튼 클릭 이벤트 연결
        selfButton.onClick.RemoveAllListeners();
        selfButton.onClick.AddListener(OnClickItem);
    }

    /// <summary>
    /// 알림(Red Dot)의 표시 상태를 업데이트합니다.
    /// </summary>
    /// <param name="show">표시할지 여부</param>
    public void UpdateNotification(bool show)
    {
        if (notificationDot != null)
        {
            notificationDot.SetActive(show);
        }
    }

    /// <summary>
    /// 아이템 클릭 시 호출되는 함수입니다.
    /// </summary>
    private void OnClickItem()
    {
        if (assignedGroup == null || groupPanelController == null) return;

        // GroupPanelController에게 그룹 채팅창을 열도록 요청합니다.
        groupPanelController.OpenOrFocusGroupChatWindow(assignedGroup);

        // 채팅창을 열었으므로, 알림 상태를 false로 변경하고 UI를 즉시 업데이트합니다.
        assignedGroup.HasNotification = false;
        UpdateNotification(false);
    }

    /// <summary>
    /// Base64로 인코딩된 이미지 문자열을 받아 아이콘 스프라이트를 설정합니다.
    /// </summary>
    /// <param name="base64String">Base64 인코딩된 이미지 데이터</param>
    private void SetIcon(string base64String)
    {
        // 런타임에 생성된 기존 스프라이트가 있다면 메모리에서 해제합니다.
        if (isIconRuntime && groupIcon.sprite != null)
        {
            if(groupIcon.sprite.texture != null)
            {
                Destroy(groupIcon.sprite.texture);
            }
            Destroy(groupIcon.sprite);
        }
        isIconRuntime = false;

        if (!string.IsNullOrEmpty(base64String))
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(imageBytes))
                {
                    // 로드된 이미지 데이터로 새 스프라이트를 생성합니다.
                    Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    groupIcon.sprite = newSprite;
                    groupIcon.gameObject.SetActive(true);
                    isIconRuntime = true; // 이 스프라이트는 런타임에 생성되었음을 표시
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GroupListItemMainUI] Base64 아이콘 디코딩 실패 (Group ID: {assignedGroup?.groupID}): {e.Message}");
            }
        }
        
        // 아이콘이 없거나 로드에 실패하면 비활성화합니다.
        groupIcon.gameObject.SetActive(false);
    }

    // 오브젝트 파괴 시 런타임 생성 리소스 해제
    private void OnDestroy()
    {
        if (isIconRuntime && groupIcon.sprite != null)
        {
            if(groupIcon.sprite.texture != null)
            {
                Destroy(groupIcon.sprite.texture);
            }
            Destroy(groupIcon.sprite);
        }
    }
}

// --- END OF FILE GroupListItemMainUI.cs ---