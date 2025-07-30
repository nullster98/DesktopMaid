using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }
    
    [Header("UI 연결")]
    [Tooltip("언어 선택 드롭다운 UI")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    private int previousLanguageIndex;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

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
        
        FindObjectOfType<SaveController>()?.SaveEverything();
        
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
    
    /// <summary>
    /// [신규] 특정 Transform 하위의 모든 "Title" 태그에 스타일을 적용하는 public 함수입니다.
    /// </summary>
    /// <param name="targetTransform">스타일을 적용할 최상위 부모 Transform</param>
    public void ApplyStylesTo(Transform targetTransform)
    {
        if (targetTransform == null) return;

        string localeCode = LocalizationSettings.SelectedLocale.Identifier.Code;
        TMP_Text[] textComponents = targetTransform.GetComponentsInChildren<TMP_Text>(true); // 비활성화된 자식 포함

        foreach (var titleText in textComponents)
        {
            if (titleText.CompareTag("Title"))
            {
                ApplyStyleToText(titleText, localeCode);
            }
        }
    }

    // 기존 ApplyLanguageSpecificStyles 함수는 이제 새로 만든 함수를 호출하도록 간소화할 수 있습니다.
    private void ApplyLanguageSpecificStyles(string localeCode)
    {
        TMP_Text[] allTextComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (var titleText in allTextComponents)
        {
            if (titleText.gameObject.scene.IsValid() && titleText.CompareTag("Title"))
            {
                ApplyStyleToText(titleText, localeCode);
            }
        }
        
        Debug.Log($"언어 [{localeCode}]에 맞는 스타일 적용 완료. (비활성 오브젝트 포함)");
    }

    // [신규] 스타일 적용 로직을 별도 함수로 분리하여 재사용성을 높입니다.
    private void ApplyStyleToText(TMP_Text textComponent, string localeCode)
    {
        if (textComponent == null) return;

        switch (localeCode)
        {
            case "ko":
            case "en":
                textComponent.fontStyle = FontStyles.Bold;
                break;
            
            default:
                textComponent.fontStyle = FontStyles.Normal;
                break;
        }
        textComponent.ForceMeshUpdate(); 
    }
}