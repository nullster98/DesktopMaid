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
    
    [Header("Confirmation Warning")]
    [SerializeField] private GameObject charWarningBox;
    [SerializeField] private Image confirmationBackground; // 배경 이미지를 바꿀 Image 컴포넌트
    [SerializeField] private TMP_Text charWarningTitle;    // 텍스트
    
    // [수정] 각 타입에 맞는 이미지들을 인스펙터에서 직접 연결
    [Header("Confirmation Sprites")]
    [SerializeField] public Sprite charSettingSprite;
    [SerializeField] public Sprite apiSettingSprite;
    [SerializeField] public Sprite localModelSettingSprite;

    // [추가] 각 타입에 맞는 메시지 키를 인스펙터에서 직접 연결
    [Header("Confirmation Messages")]
    [SerializeField] private LocalizedString charSettingMessage;
    [SerializeField] private LocalizedString apiSettingMessage;
    [SerializeField] private LocalizedString localModelSettingMessage;
    
    [Header("Main")]
    [SerializeField] private GameObject managementPanel;
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private GameObject groupWrapper;
    public Sprite modeOnSprite;
    public Sprite modeOffSprite;
    public Sprite modeSleepSprite;
    public Sprite vrmOnSprite;
    public Sprite vrmOffSprite;
    
    [Header("Settings")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private GameObject userSettingPanel;
    [SerializeField] private TMP_Text characterPanelText;
    [SerializeField] private LocalizedString characterPanelBaseTitle; 
    [SerializeField] private GameObject settingScreen;
    [SerializeField] private GameObject sharePanel;
    [SerializeField] private GameObject deletePanel;
    [SerializeField] public Sprite vrmMoveSprite;
    [SerializeField] public Sprite vrmStopSprite;
    [SerializeField] public Sprite vrmVisibleSprite;
    [SerializeField] public Sprite vrmInvisibleSprite;
    
    [Header("Characters")]
    [SerializeField] private GameObject characterPanel;
    
    
    [Header("Configuration")]
    [SerializeField] private GameObject configurationPanel;
    public Sprite toggleOnSprite;
    public Sprite toggleOffSprite;
    
    [Header("Chat")]
    [SerializeField] private GameObject chatPanel;
    
    private string characterPanelOriginalText;
    private Coroutine warningCoroutine;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        
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
        // 경고창은 기본적으로 꺼진 상태로 시작
        if (apiWarningBox != null) apiWarningBox.SetActive(false);
        if (charWarningBox != null) charWarningBox.SetActive(false);
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
            Debug.Log($"[UIManager] 캐릭터 패널 제목 업데이트 완료: {characterPanelText.text}");
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

    // --- 이하 다른 함수들은 변경 없음 ---

    public void OpenAndCloseSettingPanel()
    {
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
    
    public void OpenAndCloseDeletePanel()
    {
        deletePanel.SetActive(!deletePanel.activeSelf);
        settingScreen.SetActive(!settingScreen.activeSelf);
    }
    
    public void OpenAndCloseSharePanel()
    {
        sharePanel.gameObject.SetActive(!sharePanel.activeSelf);
        settingScreen.SetActive(!settingScreen.activeSelf);
    }
    
    /// <summary>
    /// 확인/취소 버튼이 있는 경고창을 표시합니다. 메시지는 현지화를 지원합니다.
    /// </summary>
    /// <param name="titleKey">경고창 제목의 Localization Key</param>
    /// <param name="messageKey">경고창 내용의 Localization Key</param>
    /// <param name="onConfirm">확인 버튼을 눌렀을 때 실행될 함수</param>
    /// <param name="onCancel">취소 버튼을 눌렀을 때 실행될 함수 (선택 사항)</param>
    public void ShowConfirmationWarning(ConfirmationType type)
    {
        Sprite targetSprite = null;
        LocalizedString targetMessage = null;

        // 1. 타입에 따라 사용할 스프라이트와 메시지(LocalizedString)를 선택합니다.
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
            Debug.LogError($"[UIManager] ConfirmationType '{type}'에 대한 스프라이트 또는 메시지가 설정되지 않았습니다.");
            return;
        }
        
        StartCoroutine(ShowConfirmationWarningRoutine(targetSprite, targetMessage));
    }

    private IEnumerator ShowConfirmationWarningRoutine(Sprite sprite, LocalizedString message)
    {
        // 1. 배경 이미지 교체
        confirmationBackground.sprite = sprite;

        // 2. 텍스트 현지화 및 설정
        var titleHandle = message.GetLocalizedStringAsync();
        yield return titleHandle;

        if (titleHandle.Status == AsyncOperationStatus.Succeeded)
        {
            charWarningTitle.text = titleHandle.Result;
        }

        // 3. 경고창 활성화
        charWarningBox.SetActive(true);
        charWarningBox.transform.SetAsLastSibling();
    }
    
    public void TriggerWarning(LocalizedString messageKey, float duration = 2.0f)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);
        
        // 현지화된 문자열을 가져와서 코루틴에 넘겨줍니다.
        StartCoroutine(GetLocalizedWarningAndShow(messageKey, null, duration));
    }
    
    // [추가] Smart String 인자를 받는 TriggerWarning 오버로드
    public void TriggerWarning(LocalizedString messageKey, IDictionary<string, object> arguments, float duration = 2.0f)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        StartCoroutine(GetLocalizedWarningAndShow(messageKey, arguments, duration));
    }

    private IEnumerator GetLocalizedWarningAndShow(LocalizedString messageKey, IDictionary<string, object> arguments, float duration)
    {
        var handle = messageKey.GetLocalizedStringAsync();
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

    public void OnClickWarningBox()
    {
        apiWarningBox.SetActive(false);
    }

    public void QuitApp()
    {
        Application.Quit();
    }

    private void ShowAndHideMainCanvasGroup(bool visible)
    {
        mainCanvasGroup.alpha = visible ? 1 : 0;
        mainCanvasGroup.interactable = visible;
        mainCanvasGroup.blocksRaycasts = visible;
    }
    
    public void ToggleMainCanvasGroup()
    {
        // 현재 CanvasGroup의 alpha 값을 기준으로 켜져 있는지(1) 꺼져 있는지(0) 확인합니다.
        bool isCurrentlyVisible = (mainCanvasGroup.alpha == 1);

        // 기존 함수를 호출하되, 현재 상태의 반대 값을 넣어줍니다.
        // isCurrentlyVisible가 true이면 false를, false이면 true를 전달합니다.
        ShowAndHideMainCanvasGroup(!isCurrentlyVisible);
    }

    public void OnClickGroupButton()
    {
        groupWrapper.SetActive(!groupWrapper.activeSelf);
    }
}