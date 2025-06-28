using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

public class LanguageManager : MonoBehaviour
{
    public TMP_Dropdown languageDropdown;

    // Fallback 폰트만 필요하다면 여기에 추가합니다.
    // public TMP_FontAsset fallbackFont;
    // CJK_Main_Font에 미리 Fallback을 설정해두었다면 이 변수도 필요 없습니다.

    private void Start()
    {
        languageDropdown.ClearOptions();

        var locales = LocalizationSettings.AvailableLocales.Locales;
        foreach (var locale in locales)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName));
        }

        languageDropdown.value = locales.IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        
        // 앱 시작 시 현재 언어에 맞는 스타일 적용
        ApplyLanguageSpecificStyles(LocalizationSettings.SelectedLocale.Identifier.Code);
    }

    private void OnLanguageChanged(int index)
    {
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        LocalizationSettings.SelectedLocale = selectedLocale;

        // 폰트를 교체하는 대신, 언어별 '스타일'만 조정합니다.
        ApplyLanguageSpecificStyles(selectedLocale.Identifier.Code);
    }

    /// <summary>
    /// 언어에 따라 특정 UI 요소의 폰트 스타일(굵기 등)을 조정합니다.
    /// </summary>
    private void ApplyLanguageSpecificStyles(string localeCode)
    {
        // "Title" 태그를 가진 모든 텍스트 오브젝트를 찾습니다.
        // 또는 특정 클래스나 스크립트를 가진 오브젝트를 찾는 등, 원하는 방식으로 대상을 선택할 수 있습니다.
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
                        // 한국어와 영어 제목은 Bold로 표시
                        titleText.fontStyle = FontStyles.Bold;
                        Debug.Log($"'{titleText.gameObject.name}'의 스타일을 Bold로 변경.");
                        break;
                    
                    case "ja":
                    case "zh-Hans":
                    case "zh-TW":
                        // 일본어와 중국어 제목은 Regular(Normal)로 표시
                        titleText.fontStyle = FontStyles.Normal;
                        Debug.Log($"'{titleText.gameObject.name}'의 스타일을 Normal로 변경.");
                        break;
                }
                // 스타일 변경 후 강제 업데이트가 필요할 수 있습니다.
                titleText.ForceMeshUpdate(); 
            }
        }
        
        Debug.Log($"언어 [{localeCode}]에 맞는 스타일 적용 완료.");
    }
    
    // 이 함수는 이제 필요 없을 수 있지만, 다른 이유로 전체 텍스트를 새로고침해야 할 때를 위해 남겨둘 수 있습니다.
    public void RefreshAllText()
    {
        // 예를 들어, 동적으로 생성된 UI의 언어를 갱신할 때 호출할 수 있습니다.
        if (LocalizationSettings.SelectedLocale == null) return;
        ApplyLanguageSpecificStyles(LocalizationSettings.SelectedLocale.Identifier.Code);
    }
}