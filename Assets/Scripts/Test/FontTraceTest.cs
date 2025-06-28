using UnityEngine;
using TMPro;

// 이 스크립트를, 이모지가 표시되는 TMP_Text 오브젝트에 붙여주세요.
[RequireComponent(typeof(TMP_Text))] 
public class FontTraceTest : MonoBehaviour
{
    private TMP_Text textComponent;

    void Start()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    // Update에서 매 프레임 확인하거나, 버튼 클릭 등으로 특정 시점에 확인 가능
    void Update()
    {
        // 키보드의 'T' 키를 누르면 현재 텍스트의 정보를 분석합니다.
        if (Input.GetKeyDown(KeyCode.T))
        {
            TraceTextInfo();
        }
    }

    public void TraceTextInfo()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }

        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;

        Debug.Log("========== 폰트 추적 시작 ==========");
        Debug.Log($"총 문자 수 (Character Count): {textInfo.characterCount}");

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            TMP_FontAsset fontAsset = charInfo.fontAsset;
            Material material = charInfo.material;
        
            // 문자의 32비트 유니코드 값을 가져옵니다.
            uint unicodeValue = charInfo.textElement.unicode;

            Debug.Log($"문자 '{charInfo.character}' (유니코드: U+{unicodeValue:X}) " +
                      $"-> 폰트 에셋: [{(fontAsset != null ? fontAsset.name : "None")}] | " +
                      $"머티리얼: [{(material != null ? material.name : "None")}]");

            // 유니코드 값으로 직접 비교하여 이모지를 찾습니다.
            if (unicodeValue == 0x1F609) // 😉 이모지
            {
                Debug.LogWarning($"!!!!!!!!!! 이모지(U+1F609) 발견! 폰트: {fontAsset.name}, 머티리얼: {material.name}, 셰이더: {material.shader.name} !!!!!!!!!!");
            }
        }
        Debug.Log("========== 폰트 추적 종료 ==========");
    }
}