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
    
    // [삭제] 전용 팝업창 연결은 더 이상 필요 없습니다.
    // [SerializeField] private LanguageChangePopup languageChangePopup;

    private int previousLanguageIndex;

    void Start()
    {
        languageDropdown.ClearOptions();
        var locales = LocalizationSettings.AvailableLocales.Locales;
        foreach (var locale in locales)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName));
        }

        previousLanguageIndex = locales.IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.SetValueWithoutNotify(previousLanguageIndex);

        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        
        ApplyLanguageSpecificStyles(LocalizationSettings.SelectedLocale.Identifier.Code);
    }

    private void OnLanguageChanged(int newIndex)
    {
        if (newIndex == previousLanguageIndex) return;

        Action confirmAction = () => {
            ChatDatabaseManager.Instance.ClearAllChatData();
            PerformLanguageChange(newIndex);
        };

        Action cancelAction = () => {
            languageDropdown.SetValueWithoutNotify(previousLanguageIndex);
        };

        // LocalizationManager에게 키(key)로 팝업을 요청합니다.
        LocalizationManager.Instance.ShowConfirmationPopup(
            "Popup_Title_LangChange", 
            "Popup_Msg_LangChange",
            confirmAction, 
            cancelAction
        );
    }

    private void PerformLanguageChange(int newIndex)
    {
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales[newIndex];
        LocalizationSettings.SelectedLocale = selectedLocale;
        previousLanguageIndex = newIndex;
        Debug.Log($"언어가 '{selectedLocale.Identifier.Code}'로 변경되었습니다.");

        ApplyLanguageSpecificStyles(selectedLocale.Identifier.Code);
    }
    
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