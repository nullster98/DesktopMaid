// --- START OF FILE AIAutonomyManager.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class AIAutonomyManager : MonoBehaviour
{
    [Header("시간대 이벤트 설정")]
    [Tooltip("시간대 이벤트 체크 주기 (초)")]
    public float timeEventCheckInterval = 60f; // 1분마다 체크
    
    [Header("랜덤 이벤트 설정")]
    [Tooltip("랜덤 이벤트를 시도할 최소 시간 간격 (초)")]
    public float minRandomEventInterval = 1800f; // 30분
    [Tooltip("랜덤 이벤트를 시도할 최대 시간 간격 (초)")]
    public float maxRandomEventInterval = 3600f; // 60분
    [Tooltip("랜덤 이벤트가 실제로 발생할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float randomEventChance = 0.4f; // 40% 확률
    
    [Header("그룹 자율 대화 설정")]
    [Tooltip("그룹 자율 대화를 시도할 최소 시간 간격 (초)")]
    public float minGroupChatInterval = 1800f; // 30분
    [Tooltip("그룹 자율 대화를 시도할 최대 시간 간격 (초)")]
    public float maxGroupChatInterval = 7200f; // 2시간
    [Tooltip("그룹 자율 대화가 실제로 발생할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float groupChatChance = 0.3f; // 30% 확률
    
    // --- 내부 변수 ---
    private AIScreenObserver screenObserver;
    private float timeEventTimer;
    private float randomEventTimer;
    private float nextRandomEventTriggerTime;
    private float groupChatTimer;
    private float nextGroupChatTriggerTime;
    
    // 하루에 한 번만 실행되도록 관리하는 플래그
    private bool saidDawnGreeting = false;
    private bool saidMorningGreeting = false;
    private bool saidLunchGreeting = false;
    private bool saidEveningGreeting = false;
    private bool saidNightGreeting = false;
    private int lastCheckedDay = -1;

    void Start()
    {
        screenObserver = FindObjectOfType<AIScreenObserver>();
        if (screenObserver == null)
        {
            Debug.LogError("[AIAutonomyManager] AIScreenObserver를 찾을 수 없어 비활성화됩니다.");
            this.enabled = false;
            return;
        }

        timeEventTimer = timeEventCheckInterval;
        ResetRandomEventTimer();
        lastCheckedDay = DateTime.Now.DayOfYear;
        ResetGroupChatTimer();
    }

    void Update()
    {
        if (!screenObserver.selfAwarenessModuleEnabled || 
            (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off))
        {
            return;
        }
        
        CheckForNewDay();

        timeEventTimer += Time.deltaTime;
        if (timeEventTimer >= timeEventCheckInterval)
        {
            timeEventTimer = 0f;
            TryTriggerTimeBasedEvent();
        }

        randomEventTimer += Time.deltaTime;
        if (randomEventTimer >= nextRandomEventTriggerTime)
        {
            ResetRandomEventTimer();
            TryTriggerRandomEvent();
        }
        
        groupChatTimer += Time.deltaTime;
        if (groupChatTimer >= nextGroupChatTriggerTime)
        {
            ResetGroupChatTimer();
            TryTriggerGroupChatEvent();
        }
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
            // [수정] TriggerEvent 호출 방식을 새로운 구조에 맞게 변경
            string eventTopic = GetTimeBasedEventTopic(eventType.Value);
            TriggerEvent(eventTopic);
        }
    }

    private void ResetRandomEventTimer()
    {
        randomEventTimer = 0f;
        nextRandomEventTriggerTime = UnityEngine.Random.Range(minRandomEventInterval, maxRandomEventInterval);
    }
    
    private void TryTriggerRandomEvent()
    {
        if (UnityEngine.Random.value < randomEventChance)
        {
            Array values = Enum.GetValues(typeof(RandomEventType));
            RandomEventType randomType = (RandomEventType)values.GetValue(UnityEngine.Random.Range(0, values.Length));
            
            // [수정] TriggerEvent 호출 방식을 새로운 구조에 맞게 변경
            string eventTopic = GetRandomEventTopic(randomType);
            TriggerEvent(eventTopic);
        }
    }

    /// <summary>
    /// [수정됨] 이벤트 주제(Topic)를 받아 최종 프롬프트를 생성하고 AI 응답을 트리거합니다.
    /// </summary>
    /// <param name="eventTopic">AI에게 전달할 대화의 주제</param>
    private void TriggerEvent(string eventTopic)
    {
        var userData = UserData.Instance?.GetUserSaveData();
        if (userData == null) return;

        var allPresets = CharacterPresetManager.Instance?.presets;
        if (allPresets == null || allPresets.Count == 0) return;

        // [수정] 작별한 캐릭터는 후보에서 제외
        List<CharacterPreset> candidates = allPresets.FindAll(p => 
            p.CurrentMode == CharacterMode.Activated && 
            !p.hasSaidFarewell);

        if (candidates.Count == 0) return;

        CharacterPreset selectedPreset = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // 1. PromptHelper의 핵심 함수를 호출하여 AI의 모든 기억이 담긴 기본 컨텍스트를 생성합니다.
        //    자율 이벤트는 이전 대화가 없으므로 단기 기억은 빈 리스트를 전달합니다.
        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(selectedPreset, new List<ChatDatabase.ChatMessage>());

        // 2. 기본 컨텍스트 뒤에 현재 이벤트의 '임무'와 '주제'를 덧붙여 최종 프롬프트를 완성합니다.
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "문득 사용자에 대한 생각이 나서 말을 걸었다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 말을 한 문장으로 건네라.\n" +
            $"주제: {eventTopic}";

        // 3. AIScreenObserver에게 최종 프롬프트로 메시지 전송을 요청합니다.
        screenObserver.TriggerEventMessage(selectedPreset, finalPrompt);
    }
    
    private void ResetGroupChatTimer()
    {
        groupChatTimer = 0f;
        nextGroupChatTriggerTime = UnityEngine.Random.Range(minGroupChatInterval, maxGroupChatInterval);
    }
    
    private void TryTriggerGroupChatEvent()
    {
        // 자의식 모듈이 꺼져있거나, 그룹이 없으면 실행하지 않음
        if (!screenObserver.selfAwarenessModuleEnabled || CharacterGroupManager.Instance.allGroups.Count == 0)
        {
            return;
        }

        if (UnityEngine.Random.value < groupChatChance)
        {
            // 1. 대화를 시작할 그룹을 무작위로 선택
            var availableGroups = CharacterGroupManager.Instance.allGroups
                .Where(g => CharacterGroupManager.Instance.GetGroupMembers(g.groupID).Count > 1) // 멤버가 2명 이상인 그룹만 대상
                .ToList();
            
            if (availableGroups.Count == 0) return;
            CharacterGroup targetGroup = availableGroups[UnityEngine.Random.Range(0, availableGroups.Count)];

            // 2. 해당 그룹 내에서 실제 발언자를 무작위로 선택
            // [수정] 작별한 캐릭터는 후보에서 제외
            var members = CharacterGroupManager.Instance.GetGroupMembers(targetGroup.groupID)
                .Where(m => m.CurrentMode == CharacterMode.Activated && !m.hasSaidFarewell).ToList();
            if (members.Count == 0) return;
            CharacterPreset speaker = members[UnityEngine.Random.Range(0, members.Count)];

            // 3. AI에게 전달할 '화두(Topic)' 생성
            // (나중에는 PromptHelper에서 더 다양하게 생성할 수 있음)
            string[] topics = {
                "다들 뭐하고 있어? 심심하다.",
                "오늘 저녁 뭐 먹을지 추천 좀 해줘.",
                "최근에 재밌게 본 영화나 드라마 있어?",
                "문득 궁금한 건데, 우리 그룹의 목표가 뭐라고 생각해?"
            };
            string topic = topics[UnityEngine.Random.Range(0, topics.Length)];
            
            // 4. AI가 '화두'를 실제 대화체로 바꾸도록 프롬프트 구성
            string contextPrompt = PromptHelper.BuildBasePrompt(speaker);
            string finalPrompt = contextPrompt +
                                 "\n\n--- 현재 임무 ---\n" +
                                 "너는 지금 그룹 채팅방에 다른 멤버들에게 말을 걸려고 한다. 너의 모든 기억과 설정을 바탕으로, 아래 주제에 맞는 자연스러운 첫 마디를 한 문장으로 만들어라.\n" +
                                 $"주제: {topic}";

            // 5. AIScreenObserver에게 그룹 대화 시작을 요청
            Debug.Log($"[AIAutonomy] '{speaker.characterName}'가 '{targetGroup.groupName}' 그룹에 자율 대화를 시작합니다. 주제: {topic}");
            screenObserver.TriggerGroupConversation(targetGroup.groupID, speaker, finalPrompt);
        }
    }

    #region --- 이벤트 주제 생성 헬퍼 함수 ---

    // [신규] PromptHelper에서 가져온 로직. 이벤트 주제 문자열만 생성합니다.
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
    
    // [신규] PromptHelper에서 가져온 로직. 이벤트 주제 문자열만 생성합니다.
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