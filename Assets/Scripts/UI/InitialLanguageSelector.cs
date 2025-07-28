// --- START OF FILE InitialLanguageSelector.cs ---

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Linq;
using System;

public class InitialLanguageSelector : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("비활성화 상태로 둘 언어 선택 UI의 최상위 GameObject")]
    [SerializeField] private GameObject languageSelectionObject;
    [SerializeField] private Button koreanButton;
    [SerializeField] private Button englishButton;
    [SerializeField] private Button japaneseButton;
    [SerializeField] private Button chineseSimplifiedButton; 
    [SerializeField] private Button chineseTraditionalButton;
    
    // [추가] UIManager 참조
    private UIManager uiManager;

    // 언어 선택이 완료되었음을 다른 스크립트에 알리기 위한 이벤트
    public static event Action OnLanguageSelected;

    void Start()
    {
        // UIManager 인스턴스를 찾습니다.
        uiManager = UIManager.instance;

        // 각 버튼에 언어 선택 함수를 연결합니다.
        koreanButton.onClick.AddListener(() => SelectLanguageAndProceed("ko"));
        englishButton.onClick.AddListener(() => SelectLanguageAndProceed("en"));
        japaneseButton.onClick.AddListener(() => SelectLanguageAndProceed("ja"));
        chineseSimplifiedButton.onClick.AddListener(() => SelectLanguageAndProceed("zh-Hans")); 
        chineseTraditionalButton.onClick.AddListener(() => SelectLanguageAndProceed("zh-TW"));   
    }

    /// <summary>
    /// 외부(SaveController)에서 호출하여 언어 선택 과정을 시작합니다.
    /// </summary>
    public void StartLanguageSelectionProcess()
    {
        if (uiManager == null || languageSelectionObject == null)
        {
            Debug.LogError("[InitialLanguageSelector] UIManager 또는 LanguageSelectionObject가 연결되지 않았습니다!");
            return;
        }

        // 1. 메인 UI를 투명하고 상호작용 불가능하게 만듭니다.
        uiManager.mainUiCanvasGroup.alpha = 0;
        uiManager.mainUiCanvasGroup.interactable = false;
        uiManager.mainUiCanvasGroup.blocksRaycasts = false;

        // 2. 언어 선택 UI 오브젝트를 활성화합니다.
        languageSelectionObject.SetActive(true);
        languageSelectionObject.transform.SetAsLastSibling(); // 다른 UI 위에 표시
    }

    private void SelectLanguageAndProceed(string localeCode)
    {
        // 1. 선택된 언어 코드로 로케일(Locale)을 찾습니다.
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code == localeCode);
        if (selectedLocale != null)
        {
            // 2. 언어 설정을 변경합니다.
            LocalizationSettings.SelectedLocale = selectedLocale;
            
            // 3. "최초 실행 완료" 플래그를 저장합니다.
            var saveController = FindObjectOfType<SaveController>();
            if (saveController != null)
            {
                saveController.SaveEverything();
            }

            // 4. 언어 선택 UI 오브젝트를 숨깁니다.
            languageSelectionObject.SetActive(false);

            // 5. 메인 UI를 다시 보이게 합니다.
            if (uiManager != null)
            {
                uiManager.mainUiCanvasGroup.alpha = 1;
                uiManager.mainUiCanvasGroup.interactable = true;
                uiManager.mainUiCanvasGroup.blocksRaycasts = true;
            }

            // 6. SaveController에게 "이제 다음 작업을 시작해도 된다"고 알립니다.
            OnLanguageSelected?.Invoke();
        }
        else
        {
            Debug.LogError($"[InitialLanguageSelector] '{localeCode}'에 해당하는 로케일을 찾을 수 없습니다!");
        }
    }
}