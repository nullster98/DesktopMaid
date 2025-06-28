using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
// Localization 관련 네임스페이스 추가
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
// 비동기 작업 핸들링을 위한 네임스페이스 추가 (AsyncOperationHandleStatus 포함)
using UnityEngine.ResourceManagement.AsyncOperations; 

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    
    [Header("Public")]
    [SerializeField] private GameObject apiWarningBox;
    [SerializeField] private TMP_Text warningBoxText;
    public CharacterPresetManager presetManager;
    public Sprite defaultCharacterSprite;
    public GameObject charWarningBox;
    
    [Header("Main")]
    [SerializeField] private GameObject managementPanel;
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private GameObject groupWrapper;
    public Sprite modeOnSprite;
    public Sprite modeOffSprite;
    public Sprite modeSleepSprite;
    
    [Header("Settings")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private GameObject userSettingPanel;
    [SerializeField] private TMP_Text characterPanelText;
    // [수정] UIManager가 직접 참조할 LocalizedString.
    // 인스펙터에서 "Character"에 해당하는 로컬라이제이션 키를 연결해주세요.
    [SerializeField] private LocalizedString characterPanelBaseTitle; 
    [SerializeField] private GameObject settingScreen;
    [SerializeField] private GameObject sharePanel;
    [SerializeField] private GameObject deletePanel;
    
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

    private void Start() { }

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
    
    private IEnumerator ShowWarningMessage(string message, float duration)
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
    public void TriggerWarning(string message, float duration = 1.5f)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        warningCoroutine = StartCoroutine(ShowWarningMessage(message, duration));
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