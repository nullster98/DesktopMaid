// --- START OF FILE AIBubble.cs ---

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AIBubble : MonoBehaviour
{
    [Header("UI 구성요소")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text messageText; // [중요] 말풍선 크기 조절의 기준이 될 텍스트

    /// <summary>
    /// ChatUI로부터 데이터를 받아 말풍선을 초기화합니다.
    /// </summary>
    public void Initialize(Sprite icon, string name, string message)
    {
        if (characterIcon != null)
        {
            characterIcon.sprite = icon;
            characterIcon.gameObject.SetActive(icon != null);
        }

        if (characterNameText != null)
        {
            characterNameText.text = name;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    /// <summary>
    /// 말풍선 크기 조절을 위해 메시지 텍스트 컴포넌트를 반환합니다.
    /// </summary>
    public TMP_Text GetMessageTextComponent()
    {
        return messageText;
    }
}
// --- END OF FILE AIBubble.cs ---