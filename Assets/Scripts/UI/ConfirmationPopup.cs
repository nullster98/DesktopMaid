// --- START OF FILE ConfirmationPopup.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ConfirmationPopup : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TMP_Text titleText; // 제목 텍스트 추가
    [SerializeField] private TMP_Text messageText; // 내용 텍스트
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirmAction;
    private Action onCancelAction;
    
    private float originalMessageFontSize;

    void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
        
        if (messageText != null)
        {
            originalMessageFontSize = messageText.fontSize;
        }
        
        gameObject.SetActive(false); // 시작 시 비활성화
    }

    /// <summary>
    /// 팝업을 활성화하고 제목, 내용, 버튼 클릭 시 실행될 행동을 설정합니다.
    /// </summary>
    public void Show(string title, string message, Action onConfirm, Action onCancel = null, float? messageFontSize = null)
    {
        this.titleText.text = title;
        this.messageText.text = message;
        this.onConfirmAction = onConfirm;
        this.onCancelAction = onCancel;
        
        if (messageText != null)
        {
            // messageFontSize에 값이 있으면 해당 크기로, 없으면(null) 원래 크기로 설정
            messageText.fontSize = messageFontSize.HasValue ? messageFontSize.Value : originalMessageFontSize;
        }

        gameObject.SetActive(true);
        // 팝업이 다른 UI에 가려지지 않도록 맨 위로 가져옵니다.
        transform.SetAsLastSibling();
    }

    private void OnConfirm()
    {
        gameObject.SetActive(false);
        onConfirmAction?.Invoke();
    }

    private void OnCancel()
    {
        gameObject.SetActive(false);
        onCancelAction?.Invoke();
    }
}
// --- END OF FILE ConfirmationPopup.cs ---