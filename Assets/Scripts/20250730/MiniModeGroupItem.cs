// --- START OF FILE MiniModeGroupItem.cs ---

using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 미니모드에 표시되는 '그룹' 아이템의 UI와 상호작용을 제어합니다.
/// </summary>
public class MiniModeGroupItem : MonoBehaviour
{
    [Header("UI References")]
    public string groupID;
    [Tooltip("그룹의 프로필 아이콘을 표시할 이미지")]
    public Image groupIcon;
    [Tooltip("새 메시지 알림(Red Dot)으로 사용할 게임 오브젝트")]
    public GameObject notifyImage;
    
    [Header("Button")]
    [Tooltip("채팅창을 열기 위한 메인 버튼")]
    public Button mainButton;

    private CharacterGroup _linkedGroup;
    private bool isIconRuntime = false; // 런타임에 생성된 아이콘 리소스 관리를 위함

    /// <summary>
    /// 이 UI 아이템을 특정 CharacterGroup 데이터와 연결합니다.
    /// </summary>
    public void LinkGroup(CharacterGroup group)
    {
        _linkedGroup = group;
        if (_linkedGroup == null) return;
        
        groupID = group.groupID;
        SetIcon(group.groupSymbol_Base64);
        UpdateNotifyDot();

        if (mainButton != null)
        {
            mainButton.onClick.RemoveAllListeners();
            // 버튼 클릭 시, GroupPanelController를 찾아 그룹 채팅창을 열도록 요청합니다.
            mainButton.onClick.AddListener(() => {
                var groupPanelController = FindObjectOfType<GroupPanelController>();
                groupPanelController?.OpenOrFocusGroupChatWindow(_linkedGroup);
                
                // 채팅창을 열면 알림을 끕니다.
                _linkedGroup.HasNotification = false; 
                UpdateNotifyDot();
            });
        }
    }

    /// <summary>
    /// 현재 그룹의 알림 상태에 따라 notifyImage의 활성화 여부를 업데이트합니다.
    /// </summary>
    public void UpdateNotifyDot()
    {
        if (_linkedGroup == null || notifyImage == null) return;
        notifyImage.SetActive(_linkedGroup.HasNotification);
    }
    
    /// <summary>
    /// Base64 문자열로부터 아이콘 이미지를 설정합니다.
    /// </summary>
    private void SetIcon(string base64String)
    {
        if (isIconRuntime && groupIcon.sprite != null)
        {
            if(groupIcon.sprite.texture != null) Destroy(groupIcon.sprite.texture);
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
                    groupIcon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    isIconRuntime = true;
                    return;
                }
            }
            catch (Exception e) { Debug.LogError($"MiniModeGroupItem SetIcon Error: {e.Message}"); }
        }
        // TODO: 아이콘 로드 실패 시 표시할 기본 아이콘 이미지를 설정할 수 있습니다.
        // groupIcon.sprite = UIManager.instance.defaultGroupIcon; 
    }

    private void OnDestroy()
    {
        // 런타임에 생성된 텍스처와 스프라이트가 있다면 메모리 누수 방지를 위해 파괴합니다.
        if (isIconRuntime && groupIcon.sprite != null)
        {
            if(groupIcon.sprite.texture != null) Destroy(groupIcon.sprite.texture);
            Destroy(groupIcon.sprite);
        }
    }
}
// --- END OF FILE MiniModeGroupItem.cs ---