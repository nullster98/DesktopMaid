using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using TMPro;
using UnityEngine;
// Localization 관련 네임스페이스 추가
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
// 비동기 작업 핸들링을 위한 네임스페이스 추가 (AsyncOperationHandleStatus 포함)
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Application = UnityEngine.Application;

public enum ConfirmationType
{
    CharacterSetting, // 캐릭터 설정이 비어있을 때
    ApiSetting,       // API 키가 비어있을 때
    LocalModelSetting // 로컬 모델 설정이 필요할 때
}

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    
    [Header("Public")]
    [SerializeField] private GameObject apiWarningBox;
    [SerializeField] private TMP_Text warningBoxText;
    public CharacterPresetManager presetManager;
    public Sprite defaultCharacterSprite;
    [SerializeField] public Transform uiCanvasTransform;
    [SerializeField] public CanvasGroup mainUiCanvasGroup;
    
    [Header("Confirmation Warning")]
    [SerializeField] private GameObject charWarningBox;
    [SerializeField] private Image confirmationBackground; // 배경 이미지를 바꿀 Image 컴포넌트
    [SerializeField] private TMP_Text charWarningTitle;    // 텍스트
    
    [Header("Confirmation Sprites")]
    [SerializeField] public Sprite charSettingSprite;
    [SerializeField] public Sprite apiSettingSprite;
    [SerializeField] public Sprite localModelSettingSprite;

    [Header("Confirmation Messages")]
    [SerializeField] private LocalizedString charSettingMessage;
    [SerializeField] private LocalizedString apiSettingMessage;
    [SerializeField] private LocalizedString localModelSettingMessage;
    
    [Header("Main")]
    [SerializeField] private GameObject managementPanel;
    [SerializeField] public CanvasGroup mainCanvasGroup;
    [SerializeField] public GameObject groupWrapper;
    public Sprite modeOnSprite;
    public Sprite modeOffSprite;
    public Sprite modeSleepSprite;
    public Sprite vrmOnSprite;
    public Sprite vrmOffSprite;
    
    [Header("Settings")]
    [SerializeField] public GameObject settingPanel;
    [SerializeField] private GameObject userSettingPanel;
    [SerializeField] private TMP_Text characterPanelText;
    [SerializeField] private LocalizedString characterPanelBaseTitle; 
    [SerializeField] private GameObject settingScreen;
    [SerializeField] private GameObject sharePanel;
    [SerializeField] public Sprite vrmMoveSprite;
    [SerializeField] public Sprite vrmStopSprite;
    [SerializeField] public Sprite vrmVisibleSprite;
    [SerializeField] public Sprite vrmInvisibleSprite;
    
    [Header("Characters")]
    [SerializeField] public GameObject characterPanel;
    
    [Header("Configuration")]
    [SerializeField] private GameObject configurationPanel;
    public Sprite toggleOnSprite;
    public Sprite toggleOffSprite;
    
    [Header("Chat")]
    [SerializeField] private GameObject chatPanel;

    [Header("범용 팝업")]
    [SerializeField] private ConfirmationPopup confirmationPopupPrefab; // 범용 팝업 프리팹 연결
    private ConfirmationPopup _confirmationPopupInstance;
    
    [Header("View Modes")]
    [SerializeField] private CanvasGroup mainModeCanvasGroup;
    [SerializeField] private CanvasGroup miniModeCanvasGroup;
    
    private string characterPanelOriginalText;
    private Coroutine warningCoroutine;
    private bool isMiniMode = false;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (confirmationPopupPrefab != null && _confirmationPopupInstance == null)
        {
            _confirmationPopupInstance = Instantiate(confirmationPopupPrefab, uiCanvasTransform);
        }
        
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }
    }

    private void Start()
    {
        if (apiWarningBox != null) apiWarningBox.SetActive(false);
        if (charWarningBox != null) charWarningBox.SetActive(false);

        ShowMainMode();
    }
    
    private void ShowMainMode()
    {
        mainModeCanvasGroup.alpha = 1;
        mainModeCanvasGroup.interactable = true;
        mainModeCanvasGroup.blocksRaycasts = true;

        miniModeCanvasGroup.alpha = 0;
        miniModeCanvasGroup.interactable = false;
        miniModeCanvasGroup.blocksRaycasts = false;
    
        isMiniMode = false;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        if (characterPanel.activeSelf)
        {
            StartCoroutine(UpdateCharacterPanelTitle());
        }
    }
    
    private IEnumerator UpdateCharacterPanelTitle()
    {
        var currentPreset = presetManager.GetCurrentPreset();
        if (currentPreset == null) yield break;

        AsyncOperationHandle<string> titleHandle = characterPanelBaseTitle.GetLocalizedStringAsync();
        yield return titleHandle;

        if (titleHandle.Status == AsyncOperationStatus.Succeeded)
        {
            string localizedBaseTitle = titleHandle.Result;
            characterPanelOriginalText = localizedBaseTitle;
            characterPanelText.text = $"{localizedBaseTitle} ({currentPreset.characterName})";
        }
    }

    public void OpenAndCloseCharacterPanel()
    {
        bool willOpen = !characterPanel.activeSelf;

        characterPanel.SetActive(willOpen);
        characterPanel.transform.SetAsLastSibling();
        
        if (settingPanel.activeSelf)
            settingPanel.SetActive(false);

        if (willOpen)
        {
            StartCoroutine(UpdateCharacterPanelTitle());
            
            var controller = characterPanel.GetComponent<SettingPanelController>();
            if (controller != null)
            {
                var currentPreset = presetManager.GetCurrentPreset();
                if (currentPreset != null)
                {
                    controller.targetPreset = currentPreset;
                    controller.LoadPresetToUI();
                }
            }
        }
        else
        {
            if(characterPanelText != null && !string.IsNullOrEmpty(characterPanelOriginalText))
                characterPanelText.text = characterPanelOriginalText;
        }
    }

    public void OpenAndCloseSettingPanel()
    {
        var currentPreset = presetManager.GetCurrentPreset();
        // 기본 프리셋 ID가 "DefaultPreset"이라고 가정합니다.
        if (currentPreset != null && currentPreset.presetID.StartsWith("DefaultPreset_"))
        {
            // "기본 프리셋 수정 불가"에 해당하는 키를 LocalizationManager를 통해 호출합니다.
            LocalizationManager.Instance.ShowWarning("기본 프리셋 삭제");
            return; // 함수를 여기서 종료하여 패널이 열리지 않도록 합니다.
        }
        
        settingPanel.SetActive(!settingPanel.activeSelf);
        settingPanel.transform.SetAsLastSibling();
    }

    public void OpenAndCloseConfigurationPanel()
    {
        configurationPanel.SetActive(!configurationPanel.activeSelf);
        configurationPanel.transform.SetAsLastSibling();
    }

    public void OpenAndCloseChatPanel()
    {
        chatPanel.SetActive(!chatPanel.activeSelf);
        chatPanel.transform.SetAsLastSibling();
    }

    public void OpenUserSettingPanel()
    {
        userSettingPanel.SetActive(true);
        userSettingPanel.transform.SetAsLastSibling();
    }

    public void CloseUserSettingPanel()
    {
        userSettingPanel.SetActive(false);
    }
    
    public void OpenAndCloseSharePanel()
    {
        sharePanel.gameObject.SetActive(!sharePanel.activeSelf);
        settingScreen.SetActive(!settingScreen.activeSelf);
    }
    
    public void ShowConfirmationWarning(ConfirmationType type)
    {
        Sprite targetSprite = null;
        LocalizedString targetMessage = null;

        switch (type)
        {
            case ConfirmationType.CharacterSetting:
                targetSprite = charSettingSprite;
                targetMessage = charSettingMessage;
                break;
            case ConfirmationType.ApiSetting:
                targetSprite = apiSettingSprite;
                targetMessage = apiSettingMessage;
                break;
            case ConfirmationType.LocalModelSetting:
                targetSprite = localModelSettingSprite;
                targetMessage = localModelSettingMessage;
                break;
        }

        if (targetSprite == null || targetMessage == null || targetMessage.IsEmpty)
        {
            return;
        }
        
        StartCoroutine(ShowConfirmationWarningRoutine(targetSprite, targetMessage));
    }

    private IEnumerator ShowConfirmationWarningRoutine(Sprite sprite, LocalizedString message)
    {
        confirmationBackground.sprite = sprite;
        var titleHandle = message.GetLocalizedStringAsync();
        yield return titleHandle;

        if (titleHandle.Status == AsyncOperationStatus.Succeeded)
        {
            charWarningTitle.text = titleHandle.Result;
        }

        charWarningBox.SetActive(true);
        charWarningBox.transform.SetAsLastSibling();
    }
    
    public void TriggerWarning(LocalizedString messageKey, float duration = 2.0f)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);
        
        StartCoroutine(GetLocalizedWarningAndShow(messageKey, null, duration));
    }
    
    public void TriggerWarning(LocalizedString messageKey, IDictionary<string, object> arguments, float duration = 2.0f)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        StartCoroutine(GetLocalizedWarningAndShow(messageKey, arguments, duration));
    }

    private IEnumerator GetLocalizedWarningAndShow(LocalizedString messageKey, IDictionary<string, object> arguments, float duration)
    {
        var handle = messageKey.GetLocalizedStringAsync(arguments); // 인자 전달
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            warningCoroutine = StartCoroutine(ShowWarningMessage(handle.Result, duration));
        }
        else
        {
            Debug.LogError($"Failed to load localized string for key: {messageKey.TableReference} / {messageKey.TableEntryReference}");
        }
    }

    public IEnumerator ShowWarningMessage(string message, float duration)
    {
        if (apiWarningBox.activeSelf)
        {
            apiWarningBox.SetActive(false);
            yield return null;
        }

        warningBoxText.text = message;
        apiWarningBox.SetActive(true);
        apiWarningBox.transform.SetAsLastSibling();
        yield return new WaitForSeconds(duration);
        apiWarningBox.SetActive(false);
    }
    
    /// <summary>
    /// LocalizedString 객체를 받아 확인 팝업을 띄우는 최종 실행 함수.
    /// LocalizationManager에 의해서만 호출됩니다.
    /// </summary>
    public void ShowLocalizedConfirmationPopup(LocalizedString title, LocalizedString message, Action onConfirm, Action onCancel = null, IDictionary<string, object> messageArguments = null, float? messageFontSize = null)
    {
        if (_confirmationPopupInstance != null)
        {
            // messageFontSize 값을 코루틴으로 전달
            StartCoroutine(ShowLocalizedPopupRoutine(title, message, onConfirm, onCancel, messageArguments, messageFontSize));
        }
        else
        {
            Debug.LogError("Confirmation Popup 인스턴스가 없습니다!");
        }
    }

    private IEnumerator ShowLocalizedPopupRoutine(LocalizedString localizedTitle, LocalizedString localizedMessage, Action onConfirm, Action onCancel, IDictionary<string, object> messageArguments, float? messageFontSize)
    {
        string title   = null;
        string message = null;

        var titleHandle   = localizedTitle.GetLocalizedStringAsync();
        var messageHandle = localizedMessage.GetLocalizedStringAsync(messageArguments);

        titleHandle.Completed   += op => title   = op.Result;
        messageHandle.Completed += op => message = op.Result;

        yield return titleHandle;
        yield return messageHandle;

        if (title != null && message != null)
        {
            // [수정] 최종적으로 Show 함수에 messageFontSize 값을 전달
            _confirmationPopupInstance.Show(title, message, onConfirm, onCancel, messageFontSize);
        }
        else
        {
            Debug.LogError("[UIManager] 로컬라이즈 문자열을 불러오지 못했습니다.");
            _confirmationPopupInstance.Show("Error", "Failed to load text.", onConfirm, onCancel);
        }
    }

    public void OnClickWarningBox()
    {
        apiWarningBox.SetActive(false);
    }

    public void QuitApp()
    {
        Action onConfirm = () => {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        };
        // UIManager가 직접 키를 아는 대신, LocalizationManager에게 키로 요청합니다.
        LocalizationManager.Instance.ShowConfirmationPopup("Popup_Title_Quit", "Popup_Msg_Quit", onConfirm, null, null, 25f);
    }

    public void ToggleViewMode()
    {
        isMiniMode = !isMiniMode; // 상태 전환

        if (isMiniMode)
        {
            // 미니 모드로 전환
            ShowMiniMode();
        }
        else
        {
            // 메인 모드로 전환
            ShowMainMode();
            // 메인 모드로 돌아올 때는 굳이 Refresh 할 필요가 없습니다. (계속 업데이트 되고 있었으므로)
        }
    }
    
    private void ShowMiniMode()
    {
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.RefreshAllItems();
        }
        
        mainModeCanvasGroup.alpha = 0;
        mainModeCanvasGroup.interactable = false;
        mainModeCanvasGroup.blocksRaycasts = false;

        miniModeCanvasGroup.alpha = 1;
        miniModeCanvasGroup.interactable = true;
        miniModeCanvasGroup.blocksRaycasts = true;

        isMiniMode = true;
    }


    public void OnClickGroupButton()
    {
        groupWrapper.SetActive(!groupWrapper.activeSelf);
    }
}