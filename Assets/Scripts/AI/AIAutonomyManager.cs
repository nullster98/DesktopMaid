using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// AI 캐릭터의 모든 자율 행동을 총괄하는 마스터 컨트롤러.
/// 독립적인 타이머와 확률 모델을 기반으로 다양한 자율 행동(시간대별 인사, 화면 분석, 랜덤 이벤트 등)을 트리거합니다.
/// </summary>
public class AIAutonomyManager : MonoBehaviour
{
    [Header("코어 의존성")]
    [SerializeField] private AIScreenObserver screenObserver;

    [Header("자율 행동 주기")]
    [Tooltip("자율 행동을 시도할 최소 시간 간격 (초)")]
    [SerializeField] private float minAutonomyInterval = 90f;
    [Tooltip("자율 행동을 시도할 최대 시간 간격 (초)")]
    [SerializeField] private float maxAutonomyInterval = 300f;

    [Header("자율 행동 확률 (총합 1.0 권장)")]
    [Tooltip("화면 캡처 후 반응할 확률")]
    [Range(0f, 1f)] [SerializeField] private float screenCaptureChance = 0.4f;
    [Tooltip("랜덤 이벤트를 발생시킬 확률")]
    [Range(0f, 1f)] [SerializeField] private float randomEventChance = 0.3f;
    [Tooltip("그룹 자율 대화를 시작할 확률")]
    [Range(0f, 1f)] [SerializeField] private float groupChatChance = 0.2f;
    // 나머지 확률은 '아무것도 하지 않음'으로 처리

    [Header("시간대 이벤트 설정")]
    [Tooltip("시간대별 인사 이벤트 체크 주기 (초)")]
    [SerializeField] private float timeEventCheckInterval = 60f;

    // 내부 상태 변수
    private float timeEventTimer;
    private float autonomyTimer;
    private float nextAutonomyTriggerTime;
    
    // 하루에 한 번만 인사하기 위한 플래그
    private int lastCheckedDay = -1;
    private bool saidDawnGreeting, saidMorningGreeting, saidLunchGreeting, saidEveningGreeting, saidNightGreeting;

    void Start()
    {
        if (screenObserver == null)
        {
            Debug.LogError("[AIAutonomy] AIScreenObserver가 할당되지 않아 비활성화합니다. 이 컴포넌트는 필수입니다.");
            this.enabled = false;
            return;
        }

        timeEventTimer = timeEventCheckInterval; // 시작 시 한번 바로 체크하도록
        ResetAutonomyTimer();
        InitializeDailyFlags();
    }

    void Update()
    {
        // AI 자가인식 기능이 꺼져있거나, 유저가 '자리비움' 모드일 경우 모든 자율 행동 중지
        if (!screenObserver.selfAwarenessModuleEnabled || (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off))
        {
            return;
        }
        
        CheckForNewDay();
        
        // 1. 시간대 이벤트 타이머 (독립적으로 동작)
        timeEventTimer += Time.deltaTime;
        if (timeEventTimer >= timeEventCheckInterval)
        {
            timeEventTimer = 0f;
            TryTriggerTimeBasedEvent();
        }

        // 2. 사용자와 상호작용 직후에는 자율 행동을 잠시 멈춤
        if (Time.time - screenObserver.LastPlayerChatTime < screenObserver.playerInteractionResetDelay)
        {
            ResetAutonomyTimer();
            return;
        }
        
        // 3. 메인 자율 행동 타이머
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
        if (DateTime.Now.DayOfYear != lastCheckedDay)
        {
            InitializeDailyFlags();
            Debug.Log("[AIAutonomy] 날짜 변경 감지. 모든 시간대 인사 플래그를 초기화합니다.");
        }
    }
    
    private void InitializeDailyFlags()
    {
        lastCheckedDay = DateTime.Now.DayOfYear;
        saidDawnGreeting = false;
        saidMorningGreeting = false;
        saidLunchGreeting = false;
        saidEveningGreeting = false;
        saidNightGreeting = false;
    }

    /// <summary>
    /// 설정된 확률에 따라 수행할 자율 행동을 결정하고 실행을 요청합니다.
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
            // 의도적으로 아무 행동도 하지 않고 넘어가는 케이스. 로그는 디버깅 시에만 필요.
            // Debug.Log("[AIAutonomy] 자율 행동 확률에 따라 이번 턴은 휴식합니다.");
        }
    }

    #region Individual Event Triggers

    private void TryTriggerTimeBasedEvent()
    {
        int currentHour = DateTime.Now.Hour;
        TimeEventType? eventType = null;

        // 각 시간대별로 하루에 한 번만 인사
        if (currentHour >= 1 && currentHour < 5 && !saidDawnGreeting) { eventType = TimeEventType.Dawn; saidDawnGreeting = true; }
        else if (currentHour >= 7 && currentHour < 12 && !saidMorningGreeting) { eventType = TimeEventType.Morning; saidMorningGreeting = true; }
        else if (currentHour >= 12 && currentHour < 14 && !saidLunchGreeting) { eventType = TimeEventType.Lunch; saidLunchGreeting = true; }
        else if (currentHour >= 18 && currentHour < 22 && !saidEveningGreeting) { eventType = TimeEventType.Evening; saidEveningGreeting = true; }
        else if ((currentHour >= 22 || currentHour < 1) && !saidNightGreeting) { eventType = TimeEventType.Night; saidNightGreeting = true; }

        if (eventType.HasValue)
        {
            string eventTopic = GetTimeBasedEventTopic(eventType.Value);
            TriggerTextEvent(eventTopic, $"시간대 인사 ({eventType.Value})");
        }
    }
    
    private void TryTriggerRandomEvent()
    {
        Array values = Enum.GetValues(typeof(RandomEventType));
        RandomEventType randomType = (RandomEventType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        
        string eventTopic = GetRandomEventTopic(randomType);
        TriggerTextEvent(eventTopic, $"랜덤 이벤트 ({randomType})");
    }
    
    private void TryTriggerScreenCapture()
    {
        if (!screenObserver.screenCaptureModuleEnabled) return;

        CharacterPreset preset = SelectCandidateForAction();
        if (preset == null) return;

        Debug.Log($"[AIAutonomy] 화면 분석 행동 트리거. 주체: '{preset.characterName}'");

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(preset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt = contextPrompt +
                             "\n\n--- 현재 임무 ---\n" +
                             "너는 지금 사용자의 컴퓨터 화면을 보고 있다. 첨부된 스크린샷과 너의 모든 기억을 바탕으로, 사용자에게 할 가장 적절한 말을 한 문장으로 해봐라. " +
                             "만약 화면에 너 자신이나 동료의 모습이 보이면 반드시 인지하고 반응해야 한다.";

        screenObserver.TriggerScreenCaptureEvent(preset, finalPrompt);
    }

    private void TryTriggerGroupChatEvent()
    {
        if (CharacterGroupManager.Instance == null || CharacterGroupManager.Instance.allGroups.Count == 0) return;
        
        // 2명 이상의 멤버가 있는 활성화된 그룹 필터링
        var availableGroups = CharacterGroupManager.Instance.allGroups
            .Where(g => CharacterGroupManager.Instance.GetGroupMembers(g.groupID).Count > 1)
            .ToList();
        
        if (availableGroups.Count == 0) return;

        CharacterGroup targetGroup = availableGroups[UnityEngine.Random.Range(0, availableGroups.Count)];
        
        // 그룹 내에서 발언 가능한 멤버 필터링
        var members = CharacterGroupManager.Instance.GetGroupMembers(targetGroup.groupID)
            .Where(m => !m.isLocked && m.CurrentMode == CharacterMode.Activated && !m.hasSaidFarewell).ToList();

        if (members.Count == 0) return;
        
        CharacterPreset speaker = members[UnityEngine.Random.Range(0, members.Count)];

        string[] topics = { "다들 뭐하고 있어? 심심하다.", "오늘 저녁 뭐 먹을지 추천 좀 해줘.", "최근에 재밌게 본 영화나 드라마 있어?", "문득 궁금한 건데, 우리 그룹의 목표가 뭐라고 생각해?" };
        string topic = topics[UnityEngine.Random.Range(0, topics.Length)];
        
        string contextPrompt = PromptHelper.BuildBasePrompt(speaker);
        string finalPrompt = contextPrompt +
                             "\n\n--- 현재 임무 ---\n" +
                             "너는 지금 그룹 채팅방에 다른 멤버들에게 말을 걸려고 한다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 첫 마디를 한 문장으로 만들어라.\n" +
                             $"주제: {topic}\n";

        Debug.Log($"[AIAutonomy] 그룹 대화 시작. 그룹: '{targetGroup.groupName}', 발언자: '{speaker.characterName}', 주제: {topic}");
        screenObserver.TriggerGroupConversation(targetGroup.groupID, speaker, finalPrompt);
    }

    #endregion

    #region Helper Methods
    
    /// <summary>
    /// 텍스트 기반 이벤트(시간, 랜덤)를 실행하는 공통 헬퍼 함수
    /// </summary>
    private void TriggerTextEvent(string eventTopic, string debugReason)
    {
        CharacterPreset preset = SelectCandidateForAction();
        if (preset == null) return;

        Debug.Log($"[AIAutonomy] 텍스트 이벤트 트리거. 주체: '{preset.characterName}', 이유: {debugReason}");

        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(preset, new List<ChatDatabase.ChatMessage>());
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "문득 사용자에 대한 생각이 나서 말을 걸었다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 말을 한 문장으로 건네라.\n" +
            $"주제: {eventTopic}\n";

        screenObserver.TriggerTextEvent(preset, finalPrompt);
    }

    /// <summary>
    /// 모든 자율 행동에서 발언할 캐릭터를 선정합니다.
    /// (잠금, 비활성화, 응답완료, 작별인사 상태가 아닌) 활성 후보 중에서 랜덤으로 선택합니다.
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