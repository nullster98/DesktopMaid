// --- START OF FILE LanguageChangePopup.cs ---

using UnityEngine;
using UnityEngine.UI;
using System;

public class LanguageChangePopup : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirmAction;
    private Action onCancelAction;

    void Start()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    /// <summary>
    /// 팝업을 활성화하고 버튼 클릭 시 실행될 행동을 설정합니다.
    /// </summary>
    public void Show(Action onConfirm, Action onCancel)
    {
        this.onConfirmAction = onConfirm;
        this.onCancelAction = onCancel;
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
// --- END OF FILE LanguageChangePopup.cs ---