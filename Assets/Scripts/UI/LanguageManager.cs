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

    private int previousLanguageIndex;

    void Start()
    {
        languageDropdown.ClearOptions();
        var locales = LocalizationSettings.AvailableLocales.Locales;
        foreach (var locale in locales)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName));
        }

        // 시작 시에는 기본값으로 초기화만 해두고, 실제 값 설정은 이벤트 핸들러에 맡깁니다.
        previousLanguageIndex = locales.IndexOf(LocalizationSettings.SelectedLocale);
        if (previousLanguageIndex < 0) previousLanguageIndex = 0;

        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        
    }
    
    // OnEnable/OnDisable을 통한 이벤트 구독 관리
    private void OnEnable()
    {
        // 데이터 로드 완료 이벤트 구독
        SaveController.OnLoadComplete += HandleLoadComplete;
        
        // 만약 이 오브젝트가 활성화될 때 이미 로드가 끝난 상태라면, 즉시 드롭다운 값을 새로고침합니다.
        if (SaveController.isLoadCompleted)
        {
            RefreshDropdownDisplay();
        }
    }

    private void OnDisable()
    {
        // 데이터 로드 완료 이벤트 구독 해제
        SaveController.OnLoadComplete -= HandleLoadComplete;
    }

    private void HandleLoadComplete()
    {
        RefreshDropdownDisplay();
        ApplyLanguageSpecificStyles(LocalizationSettings.SelectedLocale.Identifier.Code);
    }

    // 드롭다운 UI 값을 현재 언어 설정에 맞게 새로고침하는 함수
    private void RefreshDropdownDisplay()
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        var selectedLocale = LocalizationSettings.SelectedLocale;
        int newIndex = locales.IndexOf(selectedLocale);

        if (newIndex != -1)
        {
            // SetValueWithoutNotify를 사용하여 불필요한 이벤트 호출을 방지합니다.
            languageDropdown.SetValueWithoutNotify(newIndex);
            previousLanguageIndex = newIndex;
        }
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
        
        ChatUI[] allChatUIs = FindObjectsOfType<ChatUI>();
        foreach (ChatUI chatUI in allChatUIs)
        {
            chatUI.ShowChatUI(false);
        }
        
        if (UIManager.instance != null && UIManager.instance.characterPanel != null)
        {
            UIManager.instance.characterPanel.SetActive(false);
            UIManager.instance.settingPanel.SetActive(false);
            UIManager.instance.groupWrapper.SetActive(false);
        }

        ApplyLanguageSpecificStyles(selectedLocale.Identifier.Code);
    }
    
    // 비활성화된 오브젝트까지 모두 찾아 스타일을 적용하도록 변경
    private void ApplyLanguageSpecificStyles(string localeCode)
    {
        // 씬에 있는 모든 TMP_Text 컴포넌트를 (비활성화된 오브젝트 포함하여) 가져옵니다.
        TMP_Text[] allTextComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (var titleText in allTextComponents)
        {
            // 프로젝트 파일(프리팹 등)이 아닌, 씬 내에 존재하는 오브젝트이고 "Title" 태그를 가졌는지 확인합니다.
            if (titleText.gameObject.scene.IsValid() && titleText.CompareTag("Title"))
            {
                if (titleText != null)
                {
                    switch (localeCode)
                    {
                        case "ko":
                        case "en":
                            titleText.fontStyle = FontStyles.Bold;
                            break;
                        
                        case "ja":
                            // 중국어 간체, 번체 등 다른 언어 코드 추가 가능
                            // case "zh-Hans":
                            // case "zh-TW":
                            titleText.fontStyle = FontStyles.Normal;
                            break;
                    }
                    titleText.ForceMeshUpdate(); 
                }
            }
        }
        
        Debug.Log($"언어 [{localeCode}]에 맞는 스타일 적용 완료. (비활성 오브젝트 포함)");
    }
}