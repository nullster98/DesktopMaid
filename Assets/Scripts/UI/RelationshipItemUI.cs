// --- START OF FILE RelationshipItemUI.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RelationshipItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_InputField relationshipInputField;
    [SerializeField] private Sprite defaultIcon; // [선택사항] 아이콘이 없을 때 표시할 기본 이미지

    // 이 UI가 어떤 '대상' 캐릭터와의 관계를 나타내는지 식별하기 위한 ID
    public string TargetPresetID { get; private set; }

    /// <summary>
    /// UI를 초기화하고 데이터를 채웁니다.
    /// </summary>
    /// <param name="targetPreset">관계를 설정할 대상 캐릭터</param>
    /// <param name="currentRelationship">현재 설정된 관계 내용 (없으면 빈 문자열)</param>
    public void Setup(CharacterPreset targetPreset, string currentRelationship)
    {
        if (targetPreset == null)
        {
            gameObject.SetActive(false);
            return;
        }

        this.TargetPresetID = targetPreset.presetID;
        
        // 대상 캐릭터의 정보로 UI를 채웁니다.
        if (targetPreset.characterImage != null && targetPreset.characterImage.sprite != null)
        {
            characterIcon.sprite = targetPreset.characterImage.sprite;
        }
        else
        {
            characterIcon.sprite = defaultIcon;
        }

        // 현재 저장된 관계 내용으로 입력 필드를 채웁니다.
        relationshipInputField.text = currentRelationship;
    }

    /// <summary>
    /// 현재 입력 필드에 적힌 관계 내용을 반환합니다.
    /// </summary>
    /// <returns>사용자가 입력한 관계 서술</returns>
    public string GetCurrentRelationshipInput()
    {
        return relationshipInputField.text;
    }
}
// --- END OF FILE RelationshipItemUI.cs ---