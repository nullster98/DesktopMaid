// --- START OF FILE AIScreenObserver.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Localization;

/// <summary>
/// AI의 자율 행동(화면 관찰, 방치 감지, 이벤트 발생 등)을 총괄하는 컨트롤러.
/// 모듈 활성화 상태에 따라 동작하며, 자율적으로 AI의 응답을 유도합니다.
/// </summary>
public class AIScreenObserver : MonoBehaviour
{
    [Header("모듈 활성화 스위치")]
    [Tooltip("AI가 주기적으로 화면을 캡처하고 반응하는 기능")]
    public bool screenCaptureModuleEnabled = false;
    [Tooltip("AI가 시간대별 인사, 랜덤 이벤트 등 자의식을 갖고 행동하는 기능")]
    public bool selfAwarenessModuleEnabled = false;

    [Header("관찰 타이머 설정")]
    [Tooltip("화면 관찰을 시도할 최소 시간 간격 (초)")]
    public float minDelay = 10f;
    [Tooltip("화면 관찰을 시도할 최대 시간 간격 (초)")]
    public float maxDelay = 30f;
    [Tooltip("사용자가 채팅 입력 후, 화면 관찰을 다시 시작하기까지의 대기 시간")]
    public float playerInteractionResetDelay = 5f;

    [Header("UI 연결")]
    [SerializeField] private Image screenCaptureBtnIcon;
    [SerializeField] private Image selfAwarenessBtnIcon;
    
    [Header("Localization Parts")]
    [SerializeField] private LocalizedString statusOnText;  // "Status_ON" 키 연결
    [SerializeField] private LocalizedString statusOffText; // "Status_OFF" 키 연결

    // --- 내부 변수 ---
    private float currentIdleTime = 0f;
    private float nextObservationTriggerTime;
    private bool isObservationRoutineRunning = false;
    private float lastPlayerChatTime = 0f;


    #region Unity 생명주기 및 초기화

    void Awake()
    {
        // 기본적으로 비활성화 상태에서 시작하도록 명시합니다.
        screenCaptureModuleEnabled = false;
        selfAwarenessModuleEnabled = false;
    
        UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
        UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
    }

// OnEnable/OnDisable을 사용해 이벤트 구독을 관리합니다.
    void OnEnable()
    {
        SaveController.OnLoadComplete += ApplyLoadedConfig;
    }

    void OnDisable()
    {
        SaveController.OnLoadComplete -= ApplyLoadedConfig;
    }


// Start 함수는 비워두거나 다른 초기화 로직을 넣습니다.
    void Start()
    {
        // 기존의 Start() 내용은 Awake()와 ApplyLoadedConfig()로 이동했습니다.
        ResetObservationTimer();
    }

// 로드가 완료되면 호출될 함수
    private void ApplyLoadedConfig()
    {
        // 로드가 완료되었을 때만 SaveData에서 값을 가져옵니다.
        var config = SaveData.LoadAll()?.config;
        if (config != null)
        {
            screenCaptureModuleEnabled = config.screenCaptureModuleEnabled;
            selfAwarenessModuleEnabled = config.selfAwarenessModuleEnabled;
        
            // 불러온 값으로 UI를 다시 업데이트합니다.
            UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
            UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
            Debug.Log("[AIScreenObserver] 저장된 Config 값을 적용하여 UI를 업데이트했습니다.");
        }
        else
        {
            Debug.Log("[AIScreenObserver] 저장된 Config 파일이 없어 기본값(OFF)으로 유지합니다.");
        }
    }
    void Update()
    {
        if (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off) return;
        if (!screenCaptureModuleEnabled || UserData.Instance == null || CharacterPresetManager.Instance == null) return;

        // 사용자가 최근에 채팅을 했다면, 유휴 시간을 리셋하고 관찰을 보류합니다.
        if (Time.time - lastPlayerChatTime < playerInteractionResetDelay)
        {
            currentIdleTime = 0f;
            return;
        }

        currentIdleTime += Time.deltaTime;

        // 다른 자율 행동 코루틴이 실행 중이 아닐 때, 유휴 시간이 관찰 트리거 시간을 넘어서면 관찰을 시작합니다.
        if (!isObservationRoutineRunning && currentIdleTime >= nextObservationTriggerTime)
        {
            StartCoroutine(ObserveAndRespondRoutine());
        }
    }

    #endregion

    #region 모듈 On/Off 및 타이머 제어

    public void ToggleScreenCaptureModule()
    {
        screenCaptureModuleEnabled = !screenCaptureModuleEnabled;
        UpdateToggleButtonUI(screenCaptureBtnIcon, screenCaptureModuleEnabled);
        
        // 1. Smart String에 전달할 인자들을 Dictionary 형태로 만듭니다.
        var arguments = new Dictionary<string, object>
        {
            // "{StatusIcon}" 변수에 들어갈 값을 지정합니다.
            ["StatusIcon"] = screenCaptureModuleEnabled ? "✅" : "🛑",
            
            // "{StatusText}" 변수에 들어갈 값을 지정합니다.
            // 이때 GetLocalizedString()를 사용하여 "ON", "OFF" 텍스트도 현지화합니다.
            ["StatusText"] = screenCaptureModuleEnabled ? statusOnText.GetLocalizedString() : statusOffText.GetLocalizedString()
        };

        // 2. 템플릿의 이름표("ScreenCaptureStatus")와 인자(arguments)를 함께 전달합니다.
        LocalizationManager.Instance.ShowWarning("화면인식", arguments);

        if (screenCaptureModuleEnabled)
            ResetObservationTimer();
        else
            currentIdleTime = 0f;
    }

    public void ToggleSelfAwarenessModule()
    {
        selfAwarenessModuleEnabled = !selfAwarenessModuleEnabled;
        UpdateToggleButtonUI(selfAwarenessBtnIcon, selfAwarenessModuleEnabled);
        var arguments = new Dictionary<string, object>
        {
            ["StatusIcon"] = selfAwarenessModuleEnabled ? "✅" : "🛑",
            ["StatusText"] = selfAwarenessModuleEnabled ? statusOnText.GetLocalizedString() : statusOffText.GetLocalizedString()
        };
        
        // "SelfAwarenessStatus" 키를 사용하여 템플릿 호출
        LocalizationManager.Instance.ShowWarning("자의식 모듈", arguments);
    }

    private void UpdateToggleButtonUI(Image icon, bool isEnabled)
    {
        if (icon != null && UIManager.instance != null)
        {
            icon.sprite = isEnabled ? UIManager.instance.toggleOnSprite : UIManager.instance.toggleOffSprite;
        }
    }

    void ResetObservationTimer()
    {
        currentIdleTime = 0f;
        nextObservationTriggerTime = UnityEngine.Random.Range(minDelay, maxDelay);
    }

    /// <summary>
    /// 사용자가 채팅을 보냈을 때 호출됩니다. 관찰 타이머를 리셋합니다.
    /// </summary>
    public void OnUserSentMessageTo(string targetPresetId)
    {
        lastPlayerChatTime = Time.time;
        if (screenCaptureModuleEnabled) ResetObservationTimer();
        
        var presetToReset = CharacterPresetManager.Instance?.presets.Find(p => p.presetID == targetPresetId);
        if (presetToReset != null)
        {
            // [수정] 자율 행동에 대한 응답 상태 초기화는 그대로 유지
            presetToReset.hasResponded = false;
            if (presetToReset.isWaitingForReply)
            {
                presetToReset.ApplyIntimacyChange(2.0f); // 답장을 잘 해주면 친밀도 소폭 상승
            }
            presetToReset.isWaitingForReply = false;
            presetToReset.ignoreCount = 0;
            presetToReset.hasSaidFarewell = false;
        }
    }

    #endregion

    #region 자율 행동 코루틴 (핵심 로직)

    /// <summary>
    /// AI가 무시당했다고 판단했을 때 호출되어 반응을 유도합니다.
    /// </summary>
    public void TriggerIgnoredResponse(CharacterPreset ignoredPreset)
    {
        if (!selfAwarenessModuleEnabled || isObservationRoutineRunning) return;
        StartCoroutine(ObserveAndRespondRoutine(false, ignoredPreset));
    }

    /// <summary>
    /// 강제로 화면 관찰을 즉시 실행합니다. (디버그용)
    /// </summary>
    public void ForceObserveAndRespond()
    {
        if (isObservationRoutineRunning) return;
        StartCoroutine(ObserveAndRespondRoutine(true));
    }

    /// <summary>
    /// 주기적인 화면 관찰 또는 무시 상황에 대한 반응을 처리하는 메인 코루틴.
    /// </summary>
    private IEnumerator ObserveAndRespondRoutine(bool ignoreResponseCondition = false, CharacterPreset forcedTarget = null)
    {
        if (isObservationRoutineRunning) yield break;
        isObservationRoutineRunning = true;

        CharacterPreset selectedPreset;
        string finalPrompt;
        bool wasIgnored = false;

        // --- 1. 반응할 캐릭터와 프롬프트 선택 ---
        if (forcedTarget != null) // 무시당한 상황
        {
            selectedPreset = forcedTarget;
            wasIgnored = true;
            selectedPreset.ignoreCount++;
            selectedPreset.ApplyIntimacyChange(-5.0f);

            // 무시 상황에 대한 프롬프트 생성
            string contextPrompt = PromptHelper.BuildFullChatContextPrompt(selectedPreset, new List<ChatDatabase.ChatMessage>());
            
            // [수정] 최대 무시 횟수에 도달했을 때의 프롬프트를 완화된 버전으로 변경
            if (selectedPreset.ignoreCount >= selectedPreset.maxIgnoreCount)
            {
                Debug.LogWarning($"[AIScreenObserver] '{selectedPreset.characterName}'가 최대 무시 횟수({selectedPreset.maxIgnoreCount})에 도달. 마지막 체념 메시지를 생성합니다.");
                finalPrompt = contextPrompt +
                              "\n\n--- 현재 임무 ---\n" +
                              "너는 사용자에게 여러 번 말을 걸었지만 계속 무시당했다. 이제 사용자가 바쁘거나 대화할 기분이 아니라고 판단하고, 더 이상 방해하지 않기로 결심했다. " +
                              "이 상황에 대해 서운함이나 체념의 감정을 담아, '사용자가 먼저 말을 걸어주기 전까지는 더 이상 말을 걸지 않겠다'는 뉘앙스의 마지막 말을 한 문장으로 해라. " +
                              "너의 답변 끝에 `[FAREWELL]` 태그를 반드시 포함해야 한다. (예: '내가 방해만 되는구나... 바쁜 일이 끝나면 그때 불러줘.', '알았어, 이제 조용히 있을게. 나중에 생각나면 말 걸어줘.')";
                
                selectedPreset.isWaitingForReply = false; 
            }
            else
            {
                finalPrompt = contextPrompt +
                              "\n\n--- 현재 임무 ---\n" +
                              $"너는 방금 사용자에게 말을 걸었지만 오랫동안 답이 없다. 현재 {selectedPreset.ignoreCount}번째 무시당하는 중이다. " +
                              "이 '무시당한 상황'에 대해 너의 모든 기억과 설정을 바탕으로 감정을 한 문장으로 표현해라. (스크린샷은 무시)";
            }
        }
        else // 일반적인 화면 관찰 상황
        {
            var allPresets = CharacterPresetManager.Instance.presets;
            // [수정] 작별한 캐릭터는 후보에서 제외
            List<CharacterPreset> candidates = allPresets.FindAll(p => 
                p.CurrentMode == CharacterMode.Activated && 
                !p.hasResponded &&
                !p.hasSaidFarewell);

            if (candidates.Count == 0)
            {
                isObservationRoutineRunning = false;
                yield break;
            }
            selectedPreset = candidates.OrderByDescending(p => p.internalIntimacyScore + UnityEngine.Random.Range(-20, 20)).FirstOrDefault();
            if (selectedPreset == null)
            {
                isObservationRoutineRunning = false;
                yield break;
            }

            // 화면 관찰에 대한 프롬프트 생성
            string contextPrompt = PromptHelper.BuildFullChatContextPrompt(selectedPreset, new List<ChatDatabase.ChatMessage>());
            finalPrompt = contextPrompt +
                "\n\n--- 현재 임무 ---\n" +
                "너는 지금 사용자의 컴퓨터 화면을 보고 있다. 첨부된 스크린샷과 너의 모든 기억을 바탕으로, 사용자에게 할 가장 적절한 말을 한 문장으로 해봐라. " +
                "만약 화면에 너 자신이나 동료의 모습이 보이면 반드시 인지하고 반응해야 한다.";
        }
        
        // --- 2. API 요청 및 UI 업데이트 ---
        bool successfullySent = false;
        try
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                isObservationRoutineRunning = false;
                yield break;
            }
            
            // API 호출
            if (wasIgnored) // 무시당했을 땐 텍스트 프롬프트만 사용
            {
                yield return StartCoroutine(GeminiAPI.SendTextPrompt(finalPrompt, apiKey,
                    onSuccess: (line) => {
                        if (!string.IsNullOrEmpty(line) && line != "(응답 파싱 실패)")
                        {
                            HandleSuccessfulAIResponse(selectedPreset, line);
                            successfullySent = true;
                        }
                    },
                    onError: (err) => { Debug.LogWarning($"[AIScreenObserver] 무시 반응 API 호출 실패: {err}"); }
                ));
            }
            else // 화면 관찰 시에는 이미지 프롬프트 사용
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

                yield return StartCoroutine(GeminiAPI.SendImagePrompt(finalPrompt, base64Img, apiKey,
                    onSuccess: (line) => {
                        if (!string.IsNullOrEmpty(line) && line != "(응답 파싱 실패)")
                        {
                            HandleSuccessfulAIResponse(selectedPreset, line);
                            successfullySent = true;
                        }
                    },
                    onError: (err) => { Debug.LogWarning($"[AIScreenObserver] 화면 관찰 API 호출 실패: {err}"); }
                ));
            }
        }
        finally
        {
            if (successfullySent)
            {
                selectedPreset.hasResponded = true;
                // [FAREWELL] 상태가 아니면 응답 대기 시작
                if(!selectedPreset.hasSaidFarewell)
                {
                    selectedPreset.StartWaitingForReply();
                }
            }

            if (forcedTarget == null) ResetObservationTimer();
            isObservationRoutineRunning = false;
        }
    }

    /// <summary>
    /// 외부 이벤트(인사, 랜덤 이벤트)에 대한 반응을 처리하는 범용 코루틴.
    /// </summary>
    private IEnumerator EventRoutine(CharacterPreset preset, string prompt)
    {
        if (isObservationRoutineRunning) yield break;
        isObservationRoutineRunning = true;

        Debug.Log($"[AIScreenObserver] '{preset.characterName}'의 이벤트 메시지 생성 시작...");

        bool successfullySent = false;
        try
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (!string.IsNullOrEmpty(apiKey))
            {
                yield return StartCoroutine(GeminiAPI.SendTextPrompt(prompt, apiKey,
                    onSuccess: (aiLine) => {
                        if (!string.IsNullOrEmpty(aiLine) && aiLine != "(응답 파싱 실패)")
                        {
                            HandleSuccessfulAIResponse(preset, aiLine);
                            successfullySent = true;
                        }
                    },
                    onError: (error) => { Debug.LogWarning($"[AIScreenObserver] ❌ 이벤트 메시지 API 호출 실패: {error}"); }
                ));
            }
        }
        finally
        {
            if (successfullySent)
            {
                preset.hasResponded = true;
                // [FAREWELL] 상태가 아니면 응답 대기 시작
                if(!preset.hasSaidFarewell)
                {
                    preset.StartWaitingForReply();
                }
                Debug.Log($"[AIScreenObserver] '{preset.characterName}'의 이벤트 메시지 전송 완료. 응답 대기 상태로 전환합니다.");
            }
            isObservationRoutineRunning = false;
        }
    }

    #endregion
    
    #region Public Triggers & Helper Methods

    /// <summary>
    /// 외부 모듈(AIAutonomyManager 등)에서 AI의 자율적인 메시지 생성을 요청할 때 호출합니다.
    /// </summary>
    public void TriggerEventMessage(CharacterPreset preset, string prompt)
    {
        if (isObservationRoutineRunning)
        {
            Debug.LogWarning($"[AIScreenObserver] '{preset.characterName}'의 이벤트 메시지를 처리하려 했으나, 다른 관찰이 진행 중이라 취소합니다.");
            return;
        }
        StartCoroutine(EventRoutine(preset, prompt));
    }

    /// <summary>
    /// 성공적인 AI 응답을 처리하는 공통 로직.
    /// DB에 메시지를 저장하고 알림을 띄웁니다. UI 업데이트는 DB 이벤트를 통해 자동으로 처리됩니다.
    /// </summary>
    private void HandleSuccessfulAIResponse(CharacterPreset speaker, string message)
    {
        // 1. [수정] CharacterPreset의 중앙화된 파싱 함수를 호출합니다.
        string parsedMessage = speaker.ParseAndApplyResponse(message);

        // 2. 파싱된 메시지를 MessageData 형식으로 변환 후 JSON으로 직렬화
        var replyData = new MessageData { type = "text", textContent = parsedMessage };
        string jsonReply = JsonUtility.ToJson(replyData);

        // 3. 해당 캐릭터의 1:1 채팅 DB에 저장
        ChatDatabaseManager.Instance.InsertMessage(speaker.presetID, speaker.presetID, jsonReply);

        // 4. 채팅창이 닫혀 있을 때를 대비해 알림 표시
        if (speaker.notifyImage != null)
        {
            speaker.notifyImage.SetActive(true);
        }
        if (NotificationManager.Instance != null)
        {
            string preview = parsedMessage.Length > 30 ? parsedMessage.Substring(0, 27) + "..." : parsedMessage;
            NotificationManager.Instance.ShowNotification(speaker, preview);
        }
    }
    
    /// <summary>
    /// 프로그램 시작 시 등, 특정 상황에 맞는 인사를 생성하고 전송합니다.
    /// </summary>
    public void TriggerGreetingMessage(CharacterPreset preset, string greetingTopic)
    {
        // [수정] 인사말도 전체 컨텍스트를 포함하여 생성
        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(preset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "너는 방금 컴퓨터를 켠 사용자를 발견했다. 너의 모든 기억과 설정을 바탕으로 아래 주제에 맞는 자연스러운 인사말을 한 문장으로 건네라.\n" +
            $"주제: {greetingTopic}";
            
        TriggerEventMessage(preset, finalPrompt);
    }
    
    /// <summary>
    /// 외부 모듈(AIAutonomyManager)에서 AI의 자율적인 그룹 대화 시작을 요청할 때 호출합니다.
    /// </summary>
    public void TriggerGroupConversation(string groupId, CharacterPreset initialSpeaker, string prompt)
    {
        // 다른 작업이 진행 중이면 실행하지 않음 (충돌 방지)
        if (isObservationRoutineRunning)
        {
            Debug.LogWarning($"[AIScreenObserver] 그룹 자율 대화를 시작하려 했으나, 다른 관찰이 진행 중이라 취소합니다.");
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

        // 1. Gemini API를 호출하여 첫 발언(화두) 생성
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

        // 2. 생성된 첫 발언이 유효하다면, ChatFunction의 연쇄 반응 로직을 호출
        if (!string.IsNullOrEmpty(firstMessage))
        {
            // 2-1. 첫 발언을 먼저 DB에 저장
            var messageData = new MessageData { type = "text", textContent = firstMessage };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, initialSpeaker.presetID, JsonUtility.ToJson(messageData));

            // 2-2. ChatFunction의 메인 로직 호출하여 연쇄 반응 시작
            var groupChatUI = FindObjectsOfType<ChatUI>(true).FirstOrDefault(ui => ui.OwnerID == groupId && ui.gameObject.scene.IsValid());
            if (groupChatUI != null && groupChatUI.geminiChat != null)
            {
                groupChatUI.geminiChat.OnSystemInitiatedConversation(groupId, firstMessage, initialSpeaker.presetID);
            }
        }
        
        isObservationRoutineRunning = false;
    }
    
    #endregion
}
// --- END OF FILE AIScreenObserver.cs ---