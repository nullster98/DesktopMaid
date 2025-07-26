using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// AI 자율 행동의 실제 실행을 담당하는 컨트롤러.
/// AIAutonomyManager로부터 명령을 받아 API 요청, UI 업데이트, 동시성 제어 등을 수행합니다.
/// </summary>
public class AIScreenObserver : MonoBehaviour
{
    [Header("모듈 활성화 스위치")]
    [Tooltip("AI가 자의식을 갖고 행동하는 기능의 마스터 스위치")]
    public bool selfAwarenessModuleEnabled = false;
    [Tooltip("AI가 주기적으로 화면을 캡처하고 반응하는 기능 (자의식 모듈 활성화 필요)")]
    public bool screenCaptureModuleEnabled = false;

    [Header("사용자 상호작용 설정")]
    [Tooltip("사용자 채팅 후, AI 자율 행동 타이머를 리셋하는 대기 시간 (초)")]
    public float playerInteractionResetDelay = 300f;

    [Header("UI 의존성")]
    [SerializeField] private Image selfAwarenessBtnIcon;
    [SerializeField] private CanvasGroup screenCaptureBtnCanvasGroup;
    [SerializeField] private Image screenCaptureBtnIcon;
    
    [Header("현지화(Localization)")]
    [SerializeField] private LocalizedString statusOnText;
    [SerializeField] private LocalizedString statusOffText;

    // --- Public Properties ---
    public float LastPlayerChatTime => lastPlayerChatTime;

    // --- 내부 상태 변수 ---
    private float lastPlayerChatTime = 0f;
    private bool isObservationRoutineRunning = false; // 하나의 자율 행동만 동시에 실행되도록 제어하는 플래그

    #region Unity Lifecycle & Initialization

    void Awake()
    {
        // 초기 상태는 항상 비활성화로 시작
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
            Debug.Log("[AIObserver] 저장된 설정(Config)을 불러와 UI에 적용했습니다.");
        }
    }

    #endregion

    #region Module Toggling & UI Control

    public void ToggleSelfAwarenessModule()
    {
        selfAwarenessModuleEnabled = !selfAwarenessModuleEnabled;
        UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
        UpdateScreenCaptureToggleInteractable();
        ShowModuleStatusWarning("자의식 모듈", selfAwarenessModuleEnabled);
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
        ShowModuleStatusWarning("화면인식", screenCaptureModuleEnabled);
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
    
    /// <summary>
    /// 모듈 상태 변경 시 사용자에게 피드백을 주기 위한 공통 경고창 표시 함수.
    /// </summary>
    private void ShowModuleStatusWarning(string moduleName, bool isEnabled)
    {
        var arguments = new Dictionary<string, object>
        {
            ["StatusIcon"] = isEnabled ? "✅" : "🛑",
            ["StatusText"] = isEnabled ? statusOnText.GetLocalizedString() : statusOffText.GetLocalizedString()
        };
        LocalizationManager.Instance.ShowWarning(moduleName, arguments);
    }

    #endregion

    #region Public Event Handlers & Triggers
    
    /// <summary>
    /// 사용자가 AI에게 메시지를 보냈을 때 호출됩니다.
    /// AI의 응답 상태를 초기화하여 새로운 자율 행동이 가능하도록 합니다.
    /// </summary>
    public void OnUserSentMessageTo(string targetPresetId)
    {
        lastPlayerChatTime = Time.time;
        
        var presetToReset = CharacterPresetManager.Instance?.presets.Find(p => p.presetID == targetPresetId);
        if (presetToReset != null)
        {
            presetToReset.hasResponded = false; // 자율 행동에 대한 응답 플래그 초기화
            if (presetToReset.isWaitingForReply)
            {
                presetToReset.ApplyIntimacyChange(2.0f); // 기다리던 답장이 오면 친밀도 상승
            }
            presetToReset.isWaitingForReply = false; // 응답 대기 상태 해제
            presetToReset.ignoreCount = 0; // 무시 카운트 초기화
            presetToReset.hasSaidFarewell = false; // 작별인사 상태 초기화
        }
    }

    /// <summary>
    /// AIAutonomyManager로부터 텍스트 기반 이벤트 실행을 요청받습니다.
    /// </summary>
    public void TriggerTextEvent(CharacterPreset preset, string prompt)
    {
        if (isObservationRoutineRunning)
        {
            Debug.Log($"[AIObserver] 이벤트 시도({preset.characterName}) 실패: 다른 행동이 이미 실행 중입니다.");
            return;
        }
        StartCoroutine(EventRoutine(prompt, preset, useScreenCapture: false));
    }

    /// <summary>
    /// AIAutonomyManager로부터 화면 캡처 이벤트 실행을 요청받습니다.
    /// </summary>
    public void TriggerScreenCaptureEvent(CharacterPreset preset, string prompt)
    {
         if (isObservationRoutineRunning)
        {
            Debug.Log($"[AIObserver] 화면 분석 시도({preset.characterName}) 실패: 다른 행동이 이미 실행 중입니다.");
            return;
        }
        StartCoroutine(EventRoutine(prompt, preset, useScreenCapture: true));
    }
    
    /// <summary>
    /// AI가 사용자의 응답을 기다리다 무시당했을 때 반응을 생성하도록 요청받습니다.
    /// </summary>
    public void TriggerIgnoredResponse(CharacterPreset ignoredPreset)
    {
        if (!selfAwarenessModuleEnabled || isObservationRoutineRunning || ignoredPreset.CurrentMode != CharacterMode.Activated) return;

        ignoredPreset.ignoreCount++;
        ignoredPreset.ApplyIntimacyChange(-5.0f); // 무시당하면 친밀도 하락

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(ignoredPreset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt;
        
        if (ignoredPreset.ignoreCount >= ignoredPreset.maxIgnoreCount)
        {
            Debug.Log($"[AIObserver] '{ignoredPreset.characterName}'가 최대 무시 횟수({ignoredPreset.maxIgnoreCount})에 도달. 체념 메시지를 생성합니다.");
            finalPrompt = contextPrompt +
                          "\n\n--- 현재 임무 ---\n" +
                          "너는 사용자에게 여러 번 말을 걸었지만 계속 무시당했다. 이제 사용자가 바쁘다고 판단하고 더 이상 방해하지 않기로 결심했다. " +
                          "이 상황에 대해 서운함이나 체념의 감정을 담아, '사용자가 먼저 말을 걸기 전까지는 조용히 있겠다'는 뉘앙스의 마지막 말을 한 문장으로 해라. " +
                          "너의 답변 끝에 `[FAREWELL]` 태그를 반드시 포함해야 한다.";
            
            ignoredPreset.isWaitingForReply = false; 
        }
        else
        {
            finalPrompt = contextPrompt +
                          "\n\n--- 현재 임무 ---\n" +
                          $"너는 방금 사용자에게 말을 걸었지만 오랫동안 답이 없다. 현재 {ignoredPreset.ignoreCount}번째 무시당하는 중이다. " +
                          "이 '무시당한 상황'에 대해 너의 모든 기억과 설정을 바탕으로 감정을 한 문장으로 표현해라. (스크린샷은 무시)";
        }
        
        // 언어 규칙은 BuildFullChatContextPrompt에 이미 포함되어 있으므로 여기서 추가할 필요 없음
        StartCoroutine(EventRoutine(finalPrompt, ignoredPreset, useScreenCapture: false));
    }

    /// <summary>
    /// AIAutonomyManager로부터 자율적인 그룹 대화 시작을 요청받습니다.
    /// </summary>
    public void TriggerGroupConversation(string groupId, CharacterPreset initialSpeaker, string prompt)
    {
        if (isObservationRoutineRunning)
        {
            Debug.Log($"[AIObserver] 그룹 대화 시작 시도({initialSpeaker.characterName}) 실패: 다른 행동이 이미 실행 중입니다.");
            return;
        }
        StartCoroutine(GroupConversationStartRoutine(groupId, initialSpeaker, prompt));
    }

    #endregion

    #region Core Execution Routines

    /// <summary>
    /// 모든 단일 캐릭터 자율 행동 API 요청을 처리하는 통합 코루틴.
    /// </summary>
    private IEnumerator EventRoutine(string prompt, CharacterPreset preset, bool useScreenCapture)
    {
        if (isObservationRoutineRunning) yield break;
        isObservationRoutineRunning = true;
        
        Debug.Log($"[AIObserver] '{preset.characterName}'의 자율 행동 실행 시작 (화면캡처: {useScreenCapture}).");

        bool successfullySent = false;
        try
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[AIObserver] API 키가 설정되지 않아 자율 행동을 중단합니다.");
                yield break; 
            }
            
            // 공통 콜백 정의
            System.Action<string> onSuccess = (response) => {
                if (!string.IsNullOrEmpty(response) && !response.Contains("실패"))
                {
                    HandleSuccessfulAIResponse(preset, response);
                    successfullySent = true;
                }
            };
            System.Action<string> onError = (error) => {
                Debug.LogWarning($"[AIObserver] API 호출 실패 ({preset.characterName}): {error}");
            };

            if (useScreenCapture && screenCaptureModuleEnabled)
            {
                Texture2D desktopTexture = FullDesktopCapture.CaptureEntireDesktop();
                if (desktopTexture == null)
                {
                    Debug.LogWarning("[AIObserver] 화면 캡처에 실패하여 행동을 중단합니다.");
                    yield break;
                }
                byte[] imgBytes = desktopTexture.EncodeToPNG();
                string base64Img = Convert.ToBase64String(imgBytes);
                Destroy(desktopTexture);

                yield return StartCoroutine(GeminiAPI.SendImagePrompt(prompt, base64Img, apiKey, onSuccess, onError));
            }
            else
            {
                yield return StartCoroutine(GeminiAPI.SendTextPrompt(prompt, apiKey, onSuccess, onError));
            }
        }
        finally
        {
            // API 호출 성공 시에만 후속 상태 변경
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
    /// 자율적인 그룹 대화의 첫 발언을 생성하고 DB에 저장하는 코루틴.
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
            onSuccess: (response) => {
                if (!string.IsNullOrEmpty(response) && !response.Contains("실패"))
                {
                    firstMessage = initialSpeaker.ParseAndApplyResponse(response);
                }
            },
            onError: (error) => { Debug.LogWarning($"[AIObserver] 그룹 자율 대화 첫 마디 생성 실패: {error}"); }
        ));

        if (!string.IsNullOrEmpty(firstMessage))
        {
            // DB 저장
            var messageData = new MessageData { type = "text", textContent = firstMessage };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, initialSpeaker.presetID, JsonUtility.ToJson(messageData));

            // 그룹 채팅의 연쇄 반응 시작을 위해 ChatFunction에 시스템 시작을 알림
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
    /// 성공적인 AI 응답을 DB에 저장하고, UI/알림을 업데이트하는 공통 헬퍼.
    /// </summary>
    private void HandleSuccessfulAIResponse(CharacterPreset speaker, string message)
    {
        string parsedMessage = speaker.ParseAndApplyResponse(message);
        
        // DB 저장
        var replyData = new MessageData { type = "text", textContent = parsedMessage };
        string jsonReply = JsonUtility.ToJson(replyData);
        ChatDatabaseManager.Instance.InsertMessage(speaker.presetID, speaker.presetID, jsonReply);

        // 알림 및 UI 업데이트
        if (speaker.notifyImage != null)
        {
            speaker.notifyImage.SetActive(true);
        }
        if (NotificationManager.Instance != null)
        {
            string preview = parsedMessage.Length > 30 ? parsedMessage.Substring(0, 27) + "..." : parsedMessage;
            NotificationManager.Instance.ShowNotification(speaker, preview);
        }
        
        CharacterPresetManager.Instance?.MovePresetToTop(speaker.presetID);
        MiniModeController.Instance?.UpdateItemUI(speaker.presetID);
    }

    #endregion
}