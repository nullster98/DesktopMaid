using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AI 채팅 말풍선의 UI 요소(아이콘, 이름, 메시지)를 제어하는 컴포넌트.
/// ChatUI에 의해 생성되고 데이터가 주입됩니다.
/// </summary>
public class AIBubble : MonoBehaviour
{
    [Header("UI 구성요소")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text messageText; // 말풍선 크기 조절의 기준이 되는 핵심 텍스트

    /// <summary>
    /// ChatUI로부터 데이터를 받아 말풍선의 각 UI 요소를 초기화합니다.
    /// </summary>
    /// <param name="icon">표시할 캐릭터의 아이콘 Sprite</param>
    /// <param name="name">표시할 캐릭터의 이름</param>
    /// <param name="message">표시할 채팅 메시지 내용</param>
    public void Initialize(Sprite icon, string name, string message)
    {
        if (characterIcon != null)
        {
            characterIcon.sprite = icon;
            // 아이콘 스프라이트가 없으면 아이콘 영역 자체를 비활성화
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
    /// 말풍선의 메시지 텍스트만 동적으로 변경합니다. (주로 타이핑 애니메이션에 사용)
    /// </summary>
    /// <param name="message">새로 설정할 메시지 내용</param>
    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    /// <summary>
    /// ChatUI가 말풍선 크기를 조절할 수 있도록, 기준이 되는 메시지 텍스트 컴포넌트를 반환합니다.
    /// </summary>
    /// <returns>메시지를 담고 있는 TMP_Text 컴포넌트</returns>
    public TMP_Text GetMessageTextComponent()
    {
        return messageText;
    }
}