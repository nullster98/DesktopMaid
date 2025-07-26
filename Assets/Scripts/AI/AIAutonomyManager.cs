// --- START OF FILE AIAutonomyManager.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Localization.Settings;

/// <summary>
/// 모든 AI 자율 행동을 총괄하는 마스터 컨트롤러.
/// 통합 타이머를 통해 언제, 어떤 행동을 할지 결정합니다.
/// </summary>
public class AIAutonomyManager : MonoBehaviour
{
    [Header("핵심 컨트롤러 연결")]
    [SerializeField] private AIScreenObserver screenObserver;

    [Header("통합 자율 행동 설정")]
    [Tooltip("자율 행동을 시도할 최소 시간 간격 (초)")]
    public float minAutonomyInterval = 90f;
    [Tooltip("자율 행동을 시도할 최대 시간 간격 (초)")]
    public float maxAutonomyInterval = 300f;

    [Header("자율 행동 확률 가중치")]
    [Tooltip("화면 캡처 후 반응할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float screenCaptureChance = 0.4f;
    [Tooltip("랜덤 이벤트를 발생시킬 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float randomEventChance = 0.3f;
    [Tooltip("그룹 자율 대화를 시작할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float groupChatChance = 0.2f;
    // 나머지 10%는 '아무것도 하지 않음'

    [Header("시간대 이벤트 설정")]
    [Tooltip("시간대 이벤트 체크 주기 (초)")]
    public float timeEventCheckInterval = 60f;
    
    // --- 내부 변수 ---
    private float timeEventTimer;
    private float autonomyTimer;
    private float nextAutonomyTriggerTime;
    
    private bool saidDawnGreeting = false, saidMorningGreeting = false, saidLunchGreeting = false, saidEveningGreeting = false, saidNightGreeting = false;
    private int lastCheckedDay = -1;

    void Start()
    {
        if (screenObserver == null)
        {
            Debug.LogError("[AIAutonomyManager] AIScreenObserver가 연결되지 않아 비활성화됩니다.");
            this.enabled = false;
            return;
        }

        timeEventTimer = timeEventCheckInterval;
        ResetAutonomyTimer();
        lastCheckedDay = DateTime.Now.DayOfYear;
    }

    void Update()
    {
        if (!screenObserver.selfAwarenessModuleEnabled || (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off))
        {
            return;
        }
        
        CheckForNewDay();

        // 1. 시간대 이벤트는 별도의 타이머로 계속 체크
        timeEventTimer += Time.deltaTime;
        if (timeEventTimer >= timeEventCheckInterval)
        {
            timeEventTimer = 0f;
            TryTriggerTimeBasedEvent();
        }

        // 2. 사용자가 최근에 채팅했다면, 통합 타이머를 리셋하고 대기
        if (Time.time - screenObserver.LastPlayerChatTime < screenObserver.playerInteractionResetDelay)
        {
            ResetAutonomyTimer();
            return;
        }
        
        // 3. 통합 자율 행동 타이머
        autonomyTimer += Time.deltaTime;
        if (autonomyTimer >= nextAutonomyTriggerTime)
        {
            AttemptAutonomousAction();
            ResetAutonomyTimer();
        }
    }

    private void ResetAutonomyTimer()
    {
        autonomyTimer = 0f;
        nextAutonomyTriggerTime = UnityEngine.Random.Range(minAutonomyInterval, maxAutonomyInterval);
    }

    private void CheckForNewDay()
    {
        int currentDay = DateTime.Now.DayOfYear;
        if (currentDay != lastCheckedDay)
        {
            lastCheckedDay = currentDay;
            saidDawnGreeting = false;
            saidMorningGreeting = false;
            saidLunchGreeting = false;
            saidEveningGreeting = false;
            saidNightGreeting = false;
            Debug.Log("[AIAutonomyManager] 날짜가 변경되어 모든 시간대 인사 플래그를 초기화합니다.");
        }
    }

    /// <summary>
    /// 통합 타이머가 끝나면 호출되는 마스터 함수.
    /// 어떤 행동을 할지 결정하고 실행을 요청합니다.
    /// </summary>
    private void AttemptAutonomousAction()
    {
        float randomValue = UnityEngine.Random.value;

        if (randomValue < screenCaptureChance)
        {
            TryTriggerScreenCapture();
        }
        else if (randomValue < screenCaptureChance + randomEventChance)
        {
            TryTriggerRandomEvent();
        }
        else if (randomValue < screenCaptureChance + randomEventChance + groupChatChance)
        {
            TryTriggerGroupChatEvent();
        }
        else
        {
            Debug.Log("[AIAutonomyManager] 자율 행동 확률에 따라 이번 턴은 쉬어갑니다.");
        }
    }

    // --- 개별 이벤트 트리거 함수들 ---

    private void TryTriggerTimeBasedEvent()
    {
        int currentHour = DateTime.Now.Hour;
        TimeEventType? eventType = null;

        if (currentHour >= 1 && currentHour < 5 && !saidDawnGreeting) { eventType = TimeEventType.Dawn; saidDawnGreeting = true; }
        else if (currentHour >= 7 && currentHour < 12 && !saidMorningGreeting) { eventType = TimeEventType.Morning; saidMorningGreeting = true; }
        else if (currentHour >= 12 && currentHour < 14 && !saidLunchGreeting) { eventType = TimeEventType.Lunch; saidLunchGreeting = true; }
        else if (currentHour >= 18 && currentHour < 22 && !saidEveningGreeting) { eventType = TimeEventType.Evening; saidEveningGreeting = true; }
        else if ((currentHour >= 22 || currentHour < 1) && !saidNightGreeting) { eventType = TimeEventType.Night; saidNightGreeting = true; }

        if (eventType.HasValue)
        {
            string eventTopic = GetTimeBasedEventTopic(eventType.Value);
            TriggerTextEvent(eventTopic);
        }
    }
    
    private void TryTriggerRandomEvent()
    {
        Array values = Enum.GetValues(typeof(RandomEventType));
        RandomEventType randomType = (RandomEventType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        string eventTopic = GetRandomEventTopic(randomType);
        TriggerTextEvent(eventTopic);
    }
    
    private void TryTriggerScreenCapture()
    {
        if (!screenObserver.screenCaptureModuleEnabled)
        {
            Debug.Log("[AIAutonomyManager] 화면 캡처를 시도했으나 모듈이 비활성화되어 있습니다.");
            return;
        }

        var preset = SelectCandidateForAction();
        if (preset == null) return;

        Debug.Log($"[AIAutonomyManager] '{preset.characterName}'가 화면을 보고 반응합니다.");

        var currentLocale = LocalizationSettings.SelectedLocale;
        string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어";

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(preset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "너는 지금 사용자의 컴퓨터 화면을 보고 있다. 첨부된 스크린샷과 너의 모든 기억을 바탕으로, 사용자에게 할 가장 적절한 말을 한 문장으로 해봐라. " +
            "만약 화면에 너 자신이나 동료의 모습이 보이면 반드시 인지하고 반응해야 한다." +
            $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";

        screenObserver.TriggerScreenCaptureEvent(preset, finalPrompt);
    }

    private void TryTriggerGroupChatEvent()
    {
        if (CharacterGroupManager.Instance.allGroups.Count == 0) return;
        
        var availableGroups = CharacterGroupManager.Instance.allGroups
            .Where(g => CharacterGroupManager.Instance.GetGroupMembers(g.groupID).Count > 1)
            .ToList();
        
        if (availableGroups.Count == 0) return;
        CharacterGroup targetGroup = availableGroups[UnityEngine.Random.Range(0, availableGroups.Count)];

        var members = CharacterGroupManager.Instance.GetGroupMembers(targetGroup.groupID)
            .Where(m => !m.isLocked && m.CurrentMode == CharacterMode.Activated && !m.hasSaidFarewell).ToList();
        if (members.Count == 0) return;
        CharacterPreset speaker = members[UnityEngine.Random.Range(0, members.Count)];

        string[] topics = { "다들 뭐하고 있어? 심심하다.", "오늘 저녁 뭐 먹을지 추천 좀 해줘.", "최근에 재밌게 본 영화나 드라마 있어?", "문득 궁금한 건데, 우리 그룹의 목표가 뭐라고 생각해?" };
        string topic = topics[UnityEngine.Random.Range(0, topics.Length)];
        
        var currentLocale = LocalizationSettings.SelectedLocale;
        string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어";
        
        string contextPrompt = PromptHelper.BuildBasePrompt(speaker);
        string finalPrompt = contextPrompt +
                             "\n\n--- 현재 임무 ---\n" +
                             "너는 지금 그룹 채팅방에 다른 멤버들에게 말을 걸려고 한다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 첫 마디를 한 문장으로 만들어라.\n" +
                             $"주제: {topic}\n" +
                             $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";

        Debug.Log($"[AIAutonomy] '{speaker.characterName}'가 '{targetGroup.groupName}' 그룹에 자율 대화를 시작합니다. 주제: {topic}");
        screenObserver.TriggerGroupConversation(targetGroup.groupID, speaker, finalPrompt);
    }

    /// <summary>
    /// 텍스트 기반 이벤트(시간, 랜덤)를 실행하는 공통 함수
    /// </summary>
    private void TriggerTextEvent(string eventTopic)
    {
        var preset = SelectCandidateForAction();
        if (preset == null) return;

        Debug.Log($"[AIAutonomyManager] '{preset.characterName}'가 텍스트 이벤트를 발생시킵니다. 주제: {eventTopic}");

        var currentLocale = LocalizationSettings.SelectedLocale;
        string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어";

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(preset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "문득 사용자에 대한 생각이 나서 말을 걸었다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 말을 한 문장으로 건네라.\n" +
            $"주제: {eventTopic}\n" +
            $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";

        screenObserver.TriggerTextEvent(preset, finalPrompt);
    }

    /// <summary>
    /// 모든 자율 행동에서 발언할 캐릭터를 선정하는 공통 함수
    /// </summary>
    private CharacterPreset SelectCandidateForAction()
    {
        var allPresets = CharacterPresetManager.Instance?.presets;
        if (allPresets == null || allPresets.Count == 0) return null;

        List<CharacterPreset> candidates = allPresets.FindAll(p => 
            !p.isLocked &&
            p.CurrentMode == CharacterMode.Activated && 
            !p.hasResponded &&
            !p.hasSaidFarewell);

        if (candidates.Count == 0) return null;

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    #region --- 이벤트 주제 생성 헬퍼 함수 ---
    
    private string GetTimeBasedEventTopic(TimeEventType eventType)
    {
        switch (eventType)
        {
            case TimeEventType.Dawn:
                return "사용자가 아직 잠들지 않았거나 혹은 매우 일찍 일어난 상황에 대해, 왜 깨어있는지 궁금해하거나 건강을 걱정하는 내용";
            case TimeEventType.Morning:
                return "사용자에게 아침 인사를 건네며 오늘 하루를 응원하는 내용";
            case TimeEventType.Lunch:
                return "사용자에게 점심 시간임을 알리거나, 점심은 먹었는지 안부를 묻는 내용";
            case TimeEventType.Evening:
                return "사용자에게 저녁이 되었음을 알리며, 오늘 하루가 어땠는지 질문하거나 수고했다는 위로를 건네는 내용";
            case TimeEventType.Night:
                return "사용자에게 잠들 시간임을 상기시키며, 좋은 밤 되라는 인사를 건네는 내용";
            default:
                return "";
        }
    }
    
    private string GetRandomEventTopic(RandomEventType eventType)
    {
        switch (eventType)
        {
            case RandomEventType.Compliment:
                return "이유 없이 그냥 사용자를 칭찬하거나, 사용자의 좋은 점에 대해 이야기하는 내용";
            case RandomEventType.Question:
                return "사용자의 취미나 최근 관심사, 또는 좋아하는 것에 대해 가볍게 질문하는 내용";
            case RandomEventType.Joke:
                return "너의 지능 수준에 맞는 아재 개그나 짧은 농담을 던지는 내용";
            case RandomEventType.Encouragement:
                return "사용자가 무언가에 지쳐 보일 수 있다고 가정하고, 힘내라고 응원하거나 격려하는 내용";
            default:
                return "";
        }
    }

    #endregion
}
// --- END OF FILE AIAutonomyManager.cs ---