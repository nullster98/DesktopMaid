using UnityEngine.Localization.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class FontChanger : MonoBehaviour
{
    public TMP_Text[] textsToUpdate;
    public TMP_FontAsset koreanFont;
    public TMP_FontAsset englishFont;
    public TMP_FontAsset japaneseFont;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += UpdateFonts;
        UpdateFonts(LocalizationSettings.SelectedLocale);
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= UpdateFonts;
    }

    private void UpdateFonts(Locale locale)
    {
        TMP_FontAsset selectedFont = null;

        switch (locale.Identifier.Code)
        {
            case "ko":
                selectedFont = koreanFont;
                break;
            case "en":
                selectedFont = englishFont;
                break;
            case "ja":
                selectedFont = japaneseFont;
                break;
        }

        foreach (var tmp in textsToUpdate)
        {
            tmp.font = selectedFont;
        }
    }
}