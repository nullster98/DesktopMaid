using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguageDropdownHandler : MonoBehaviour
{
    public TMP_Dropdown languageDropdown;
    private void Start()
    {
        // 드롭다운 옵션 초기화
        languageDropdown.ClearOptions();

        for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; i++)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[i];
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName));
        }

        // 현재 선택된 언어 표시
        var selectedLocale = LocalizationSettings.SelectedLocale;
        int selectedIndex = LocalizationSettings.AvailableLocales.Locales.IndexOf(selectedLocale);
        languageDropdown.value = selectedIndex;

        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(int index)
    {
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        LocalizationSettings.SelectedLocale = selectedLocale;
    }
}
