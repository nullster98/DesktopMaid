// --- START OF FILE AIScreenObserver.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// AI의 자율 행동 실행(API 요청)과 관련 UI를 담당하는 컨트롤러.
/// AIAutonomyManager로부터 명령을 받아 실제 동작을 수행합니다.
/// </summary>
public class AIScreenObserver : MonoBehaviour
{
    [Header("모듈 활성화 스위치")]
    [Tooltip("AI가 시간대별 인사, 랜덤 이벤트 등 자의식을 갖고 행동하는 기능 (마스터 스위치)")]
    public bool selfAwarenessModuleEnabled = false;
    [Tooltip("AI가 주기적으로 화면을 캡처하고 반응하는 기능 (스마트 인터렉션 활성화 시 사용 가능)")]
    public bool screenCaptureModuleEnabled = false;

    [Header("사용자 상호작용 설정")]
    [Tooltip("사용자가 채팅 입력 후, AI 자율 행동을 다시 시작하기까지의 대기 시간")]
    public float playerInteractionResetDelay = 300f;

    [Header("UI 연결")]
    [SerializeField] private Image selfAwarenessBtnIcon;
    [SerializeField] private CanvasGroup screenCaptureBtnCanvasGroup;
    [SerializeField] private Image screenCaptureBtnIcon;
    
    [Header("Localization Parts")]
    [SerializeField] private LocalizedString statusOnText;
    [SerializeField] private LocalizedString statusOffText;

    // --- 내부 변수 ---
    private bool isObservationRoutineRunning = false;
    private float lastPlayerChatTime = 0f;
    private bool _bootGreetingSent;

    public float LastPlayerChatTime => lastPlayerChatTime;

    #region Unity 생명주기 및 초기화

    void Awake()
    {
        selfAwarenessModuleEnabled = false;
        screenCaptureModuleEnabled = false;
    
        UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
        UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
        UpdateScreenCaptureToggleInteractable();
    }

    void OnEnable()
    {
        SaveController.OnLoadComplete += ApplyLoadedConfig;
    }

    void OnDisable()
    {
        SaveController.OnLoadComplete -= ApplyLoadedConfig;
    }

    void Start()
    {
        // 자체 타이머 로직이 모두 AIAutonomyManager로 이전되었으므로 비워둡니다.
    }

    private void ApplyLoadedConfig()
    {
        var config = SaveData.LoadAll()?.config;
        if (config != null)
        {
            selfAwarenessModuleEnabled = config.selfAwarenessModuleEnabled;
            screenCaptureModuleEnabled = config.screenCaptureModuleEnabled;
        
            UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
            UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
            UpdateScreenCaptureToggleInteractable();
            Debug.Log("[AIScreenObserver] 저장된 Config 값을 적용하여 UI를 업데이트했습니다.");
        }
        else
        {
            Debug.Log("[AIScreenObserver] 저장된 Config 파일이 없어 기본값(OFF)으로 유지합니다.");
        }
    }

    #endregion

    #region 모듈 On/Off 및 UI 제어

    public void ToggleSelfAwarenessModule()
    {
        selfAwarenessModuleEnabled = !selfAwarenessModuleEnabled;
        UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
        UpdateScreenCaptureToggleInteractable();
        
        var arguments = new Dictionary<string, object>
        {
            ["StatusIcon"] = selfAwarenessModuleEnabled ? "✅" : "🛑",
            ["StatusText"] = selfAwarenessModuleEnabled ? statusOnText.GetLocalizedString() : statusOffText.GetLocalizedString()
        };
        LocalizationManager.Instance.ShowWarning("자의식 모듈", arguments);
    }

    public void ToggleScreenCaptureModule()
    {
        if (!selfAwarenessModuleEnabled)
        {
            LocalizationManager.Instance.ShowWarning("스마트 인터랙션 비활성화");
            return;
        }

        screenCaptureModuleEnabled = !screenCaptureModuleEnabled;
        UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
        
        var arguments = new Dictionary<string, object>
        {
            ["StatusIcon"] = screenCaptureModuleEnabled ? "✅" : "🛑",
            ["StatusText"] = screenCaptureModuleEnabled ? statusOnText.GetLocalizedString() : statusOffText.GetLocalizedString()
        };
        LocalizationManager.Instance.ShowWarning("화면인식", arguments);
    }

    private void UpdateScreenCaptureToggleInteractable()
    {
        if (screenCaptureBtnCanvasGroup == null) return;
        
        bool isInteractable = selfAwarenessModuleEnabled;
        screenCaptureBtnCanvasGroup.interactable = isInteractable;
        screenCaptureBtnCanvasGroup.alpha = isInteractable ? 1.0f : 0.5f;
    }

    private void UpdateToggleButtonUI(Image icon, bool isEnabled)
    {
        if (icon != null && UIManager.instance != null)
        {
            icon.sprite = isEnabled ? UIManager.instance.toggleOnSprite : UIManager.instance.toggleOffSprite;
        }
    }
    
    public void OnUserSentMessageTo(string targetPresetId)
    {
        lastPlayerChatTime = Time.time;
        
        var presetToReset = CharacterPresetManager.Instance?.presets.Find(p => p.presetID == targetPresetId);
        if (presetToReset != null)
        {
            presetToReset.hasResponded = false;
            if (presetToReset.isWaitingForReply)
            {
                presetToReset.ApplyIntimacyChange(2.0f);
            }
            presetToReset.isWaitingForReply = false;
            presetToReset.ignoreCount = 0;
            presetToReset.hasSaidFarewell = false;
        }
    }

    #endregion

    #region 자율 행동 실행 (외부 호출용)

    /// <summary>
    /// AIAutonomyManager로부터 호출되어, 주어진 프롬프트로 텍스트 기반 이벤트를 실행합니다.
    /// </summary>
    public void TriggerTextEvent(CharacterPreset preset, string prompt)
    {
        if (isObservationRoutineRunning)
        {
            Debug.LogWarning($"[AIScreenObserver] '{preset.characterName}'의 이벤트 메시지를 처리하려 했으나, 다른 관찰이 진행 중이라 취소합니다.");
            return;
        }
        StartCoroutine(EventRoutine(prompt, preset, false));
    }

    /// <summary>
    /// AIAutonomyManager로부터 호출되어, 화면 캡처를 포함한 이벤트를 실행합니다.
    /// </summary>
    public void TriggerScreenCaptureEvent(CharacterPreset preset, string prompt)
    {
         if (isObservationRoutineRunning)
        {
            Debug.LogWarning($"[AIScreenObserver] '{preset.characterName}'의 화면 캡처를 처리하려 했으나, 다른 관찰이 진행 중이라 취소합니다.");
            return;
        }
        StartCoroutine(EventRoutine(prompt, preset, true));
    }
    
    /// <summary>
    /// AI가 무시당했을 때 호출되어 반응을 유도합니다.
    /// </summary>
    public void TriggerIgnoredResponse(CharacterPreset ignoredPreset)
    {
        if (!selfAwarenessModuleEnabled || isObservationRoutineRunning || ignoredPreset.CurrentMode != CharacterMode.Activated)
        {
            Debug.Log($"[AIScreenObserver] '{ignoredPreset.characterName}'의 무시 반응을 처리하려 했으나, 현재 모드가 '{ignoredPreset.CurrentMode}'이므로 취소합니다.");
            return;
        }
        
        if (!selfAwarenessModuleEnabled || isObservationRoutineRunning) return;

        string finalPrompt;
        var currentLocale = LocalizationSettings.SelectedLocale;
        string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어";
        
        ignoredPreset.ignoreCount++;
        ignoredPreset.ApplyIntimacyChange(-5.0f);

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(ignoredPreset, new List<ChatDatabase.ChatMessage>());
        
        if (ignoredPreset.ignoreCount >= ignoredPreset.maxIgnoreCount)
        {
            Debug.LogWarning($"[AIScreenObserver] '{ignoredPreset.characterName}'가 최대 무시 횟수({ignoredPreset.maxIgnoreCount})에 도달. 마지막 체념 메시지를 생성합니다.");
            finalPrompt = contextPrompt +
                          "\n\n--- 현재 임무 ---\n" +
                          "너는 사용자에게 여러 번 말을 걸었지만 계속 무시당했다. 이제 사용자가 바쁘거나 대화할 기분이 아니라고 판단하고, 더 이상 방해하지 않기로 결심했다. " +
                          "이 상황에 대해 서운함이나 체념의 감정을 담아, '사용자가 먼저 말을 걸어주기 전까지는 더 이상 말을 걸지 않겠다'는 뉘앙스의 마지막 말을 한 문장으로 해라. " +
                          "너의 답변 끝에 `[FAREWELL]` 태그를 반드시 포함해야 한다. (예: '내가 방해만 되는구나... 바쁜 일이 끝나면 그때 불러줘.', '알았어, 이제 조용히 있을게. 나중에 생각나면 말 걸어줘.')" +
                          $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";
            
            ignoredPreset.isWaitingForReply = false; 
        }
        else
        {
            finalPrompt = contextPrompt +
                          "\n\n--- 현재 임무 ---\n" +
                          $"너는 방금 사용자에게 말을 걸었지만 오랫동안 답이 없다. 현재 {ignoredPreset.ignoreCount}번째 무시당하는 중이다. " +
                          "이 '무시당한 상황'에 대해 너의 모든 기억과 설정을 바탕으로 감정을 한 문장으로 표현해라. (스크린샷은 무시)" +
                          $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";
        }
        
        StartCoroutine(EventRoutine(finalPrompt, ignoredPreset, false));
    }

    /// <summary>
    /// 모든 자율 행동 API 요청을 처리하는 통합 코루틴.
    /// </summary>
    private IEnumerator EventRoutine(string prompt, CharacterPreset preset, bool useScreenCapture)
    {
        if (isObservationRoutineRunning) yield break;
        isObservationRoutineRunning = true;

        Debug.Log($"[AIScreenObserver] '{preset.characterName}'의 자율 행동 실행 시작 (화면캡처: {useScreenCapture})...");

        bool successfullySent = false;
        try
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                isObservationRoutineRunning = false;
                yield break;
            }
            
            if (useScreenCapture)
            {
                Texture2D desktopTexture = FullDesktopCapture.CaptureEntireDesktop();
                if (desktopTexture == null)
                {
                    isObservationRoutineRunning = false;
                    yield break;
                }
                byte[] imgBytes = desktopTexture.EncodeToPNG();
                string base64Img = Convert.ToBase64String(imgBytes);
                Destroy(desktopTexture);

                yield return StartCoroutine(GeminiAPI.SendImagePrompt(prompt, base64Img, apiKey,
                    onSuccess: (line) => {
                        if (!string.IsNullOrEmpty(line) && line != "(응답 파싱 실패)")
                        {
                            HandleSuccessfulAIResponse(preset, line);
                            successfullySent = true;
                        }
                    },
                    onError: (err) => { Debug.LogWarning($"[AIScreenObserver] 화면 관찰 API 호출 실패: {err}"); }
                ));
            }
            else
            {
                yield return StartCoroutine(GeminiAPI.SendTextPrompt(prompt, apiKey,
                    onSuccess: (line) => {
                        if (!string.IsNullOrEmpty(line) && line != "(응답 파싱 실패)")
                        {
                            HandleSuccessfulAIResponse(preset, line);
                            successfullySent = true;
                        }
                    },
                    onError: (err) => { Debug.LogWarning($"[AIScreenObserver] 텍스트 이벤트 API 호출 실패: {err}"); }
                ));
            }
        }
        finally
        {
            if (successfullySent)
            {
                preset.hasResponded = true;
                if(!preset.hasSaidFarewell)
                {
                    preset.StartWaitingForReply();
                }
            }
            isObservationRoutineRunning = false;
        }
    }
    
    /// <summary>
    /// 외부 모듈(AIAutonomyManager)에서 AI의 자율적인 그룹 대화 시작을 요청할 때 호출합니다.
    /// </summary>
    public void TriggerGroupConversation(string groupId, CharacterPreset initialSpeaker, string prompt)
    {
        if (isObservationRoutineRunning)
        {
            Debug.LogWarning($"[AIScreenObserver] 그룹 자율 대화를 시작하려 했으나, 다른 작업이 진행 중이라 취소합니다.");
            return;
        }
        StartCoroutine(GroupConversationStartRoutine(groupId, initialSpeaker, prompt));
    }

    /// <summary>
    /// 자율적인 그룹 대화의 첫 발언을 생성하는 코루틴.
    /// </summary>
    private IEnumerator GroupConversationStartRoutine(string groupId, CharacterPreset initialSpeaker, string prompt)
    {
        isObservationRoutineRunning = true;

        string apiKey = UserData.Instance.GetAPIKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            isObservationRoutineRunning = false;
            yield break;
        }

        string firstMessage = "";
        yield return StartCoroutine(GeminiAPI.SendTextPrompt(prompt, apiKey,
            onSuccess: (aiLine) => {
                if (!string.IsNullOrEmpty(aiLine) && aiLine != "(응답 파싱 실패)")
                {
                    firstMessage = initialSpeaker.ParseAndApplyResponse(aiLine);
                }
            },
            onError: (error) => { Debug.LogWarning($"[AIScreenObserver] ❌ 그룹 자율 대화 첫 마디 생성 실패: {error}"); }
        ));

        if (!string.IsNullOrEmpty(firstMessage))
        {
            var messageData = new MessageData { type = "text", textContent = firstMessage };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, initialSpeaker.presetID, JsonUtility.ToJson(messageData));

            var groupChatUI = FindObjectsOfType<ChatUI>(true).FirstOrDefault(ui => ui.OwnerID == groupId && ui.gameObject.scene.IsValid());
            if (groupChatUI != null && groupChatUI.geminiChat != null)
            {
                groupChatUI.geminiChat.OnSystemInitiatedConversation(groupId, firstMessage, initialSpeaker.presetID);
            }
        }
        
        isObservationRoutineRunning = false;
    }

    #endregion
    
    #region Helper Methods

    /// <summary>
    /// 성공적인 AI 응답을 처리하는 공통 로직.
    /// </summary>
    private void HandleSuccessfulAIResponse(CharacterPreset speaker, string message)
    {
        string parsedMessage = speaker.ParseAndApplyResponse(message);
        var replyData = new MessageData { type = "text", textContent = parsedMessage };
        string jsonReply = JsonUtility.ToJson(replyData);
        ChatDatabaseManager.Instance.InsertMessage(speaker.presetID, speaker.presetID, jsonReply);

        if (speaker.notifyImage != null)
        {
            speaker.notifyImage.SetActive(true);
        }
        if (NotificationManager.Instance != null)
        {
            string preview = parsedMessage.Length > 30 ? parsedMessage.Substring(0, 27) + "..." : parsedMessage;
            NotificationManager.Instance.ShowNotification(speaker, preview);
        }
        
        if (CharacterPresetManager.Instance != null)
        {
            CharacterPresetManager.Instance.MovePresetToTop(speaker.presetID);
        }
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.UpdateItemUI(speaker.presetID);
            Debug.Log($"[AIScreenObserver] MiniModeController에 '{speaker.characterName}'의 UI 업데이트를 요청했습니다.");
        }
    }

    #endregion
}
// --- END OF FILE AIScreenObserver.cs ---