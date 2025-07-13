// --- START OF FILE LanguageManager.cs ---

using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System;

public class LanguageManager : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("언어 선택 드롭다운 UI")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    
    [Header("전용 팝업창 연결")]
    [Tooltip("언어 변경 시 띄울 전용 팝업창 GameObject. LanguageChangePopup 스크립트가 있어야 합니다.")]
    [SerializeField] private LanguageChangePopup languageChangePopup;

    private int previousLanguageIndex;

    void Start()
    {
        // 드롭다운 메뉴 초기화
        languageDropdown.ClearOptions();
        var locales = LocalizationSettings.AvailableLocales.Locales;
        foreach (var locale in locales)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName));
        }

        // 현재 언어를 기준으로 초기값 설정
        previousLanguageIndex = locales.IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.SetValueWithoutNotify(previousLanguageIndex);

        // 리스너 등록
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        
        // 앱 시작 시 현재 언어에 맞는 스타일을 한번 적용
        ApplyLanguageSpecificStyles(LocalizationSettings.SelectedLocale.Identifier.Code);
    }

    /// <summary>
    /// 사용자가 드롭다운 메뉴에서 다른 언어를 선택했을 때 호출됩니다.
    /// </summary>
    private void OnLanguageChanged(int newIndex)
    {
        if (newIndex == previousLanguageIndex) return;

        // '확인' 버튼을 눌렀을 때 실행될 행동을 정의합니다.
        Action confirmAction = () => {
            // 1. 모든 채팅 데이터와 기억을 삭제합니다.
            ChatDatabaseManager.Instance.ClearAllChatData();
            // 2. 실제 언어 변경을 수행합니다.
            PerformLanguageChange(newIndex);
        };

        // '취소' 버튼을 눌렀을 때 실행될 행동을 정의합니다.
        Action cancelAction = () => {
            // 드롭다운의 UI 선택을 이전 상태로 되돌립니다.
            languageDropdown.SetValueWithoutNotify(previousLanguageIndex);
        };

        // 전용 팝업창을 띄우고, 위에서 정의한 행동들을 전달합니다.
        languageChangePopup.Show(confirmAction, cancelAction);
    }

    /// <summary>
    /// 실제 언어를 변경하고, 관련 UI 스타일을 업데이트합니다.
    /// </summary>
    private void PerformLanguageChange(int newIndex)
    {
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales[newIndex];
        LocalizationSettings.SelectedLocale = selectedLocale;
        previousLanguageIndex = newIndex;
        Debug.Log($"언어가 '{selectedLocale.Identifier.Code}'로 변경되었습니다.");

        // 언어 변경이 확정된 후, 해당 언어에 맞는 스타일을 적용합니다.
        ApplyLanguageSpecificStyles(selectedLocale.Identifier.Code);
    }
    
    /// <summary>
    /// 언어에 따라 특정 UI 요소의 폰트 스타일(굵기 등)을 조정합니다.
    /// </summary>
    private void ApplyLanguageSpecificStyles(string localeCode)
    {
        GameObject[] titleObjects = GameObject.FindGameObjectsWithTag("Title");

        foreach (var obj in titleObjects)
        {
            TMP_Text titleText = obj.GetComponent<TMP_Text>();
            if (titleText != null)
            {
                switch (localeCode)
                {
                    case "ko":
                    case "en":
                        titleText.fontStyle = FontStyles.Bold;
                        break;
                    
                    case "ja":
                    case "zh-Hans":
                    case "zh-TW":
                        titleText.fontStyle = FontStyles.Normal;
                        break;
                }
                titleText.ForceMeshUpdate(); 
            }
        }
        
        Debug.Log($"언어 [{localeCode}]에 맞는 스타일 적용 완료.");
    }
}
// --- END OF FILE LanguageManager.cs ---