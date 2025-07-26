using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// MemoryAgent를 관리하고 주기적으로 기억 처리 프로세스를 실행하는 싱글턴 컨트롤러.
/// </summary>
public class MemorySystemController : MonoBehaviour
{
    public static MemorySystemController Instance { get; private set; }
    
    [Tooltip("기억 처리 작업을 시도할 주기 (초)")]
    public float processInterval = 300f; // 5분
    
    // 실제 기억 처리 로직을 담당하는 에이전트
    public MemoryAgent agent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            agent = new MemoryAgent();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 컨트롤러가 시작되면 주기적인 기억 처리 루틴을 백그라운드에서 실행
        ProcessAllMemoriesRoutine().Forget();
    }

    /// <summary>
    /// 정해진 간격으로 모든 캐릭터와 그룹의 기억을 처리하는 비동기 루틴.
    /// </summary>
    private async UniTaskVoid ProcessAllMemoriesRoutine()
    {
        while (this != null) // 오브젝트가 파괴되면 루프 중단
        {
            await UniTask.Delay(TimeSpan.FromSeconds(processInterval), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            Debug.Log("[MemorySystem] 주기적인 기억 처리 작업을 시작합니다.");

            if (CharacterPresetManager.Instance != null)
            {
                // 모든 개인 캐릭터의 기억 처리
                foreach (var preset in CharacterPresetManager.Instance.presets)
                {
                    await agent.CheckAndProcessMemoryAsync(preset);
                }
            }

            if (CharacterGroupManager.Instance != null)
            {
                // 모든 그룹의 기억 처리
                foreach (var group in CharacterGroupManager.Instance.allGroups)
                {
                    await agent.CheckAndProcessGroupMemoryAsync(group);
                }
            }
        }
    }
}


/// <summary>
/// 대화 기록을 처리하여 장기기억(요약)과 초장기기억(지식)을 추출하는 AI 에이전트.
/// </summary>
public class MemoryAgent
{
    // 요약을 시도할 최소 대화 메시지 개수
    private const int SUMMARY_CHUNK_SIZE = 20;

    #region Public Memory Processing Methods

    /// <summary>
    /// 개인 캐릭터의 새로운 대화 기록이 충분히 쌓였는지 확인하고, 필요 시 기억 형성 프로세스를 시작합니다.
    /// </summary>
    public async UniTask CheckAndProcessMemoryAsync(CharacterPreset preset)
    {
        if (preset == null) return;
        
        var db = ChatDatabaseManager.Instance.GetDatabase(preset.presetID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > preset.lastSummarizedMessageId).ToList();

        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
            Debug.Log($"[MemoryAgent] '{preset.characterName}'의 기억 요약 시작 (대상 메시지: {newMessages.Count}개).");
            await SummarizeAndLearnAsync(preset, newMessages);
        }
    }
    
    /// <summary>
    /// 그룹의 새로운 대화 기록이 충분히 쌓였는지 확인하고, 필요 시 기억 형성 프로세스를 시작합니다.
    /// </summary>
    public async UniTask CheckAndProcessGroupMemoryAsync(CharacterGroup group)
    {
        if (group == null) return;
        
        var db = ChatDatabaseManager.Instance.GetGroupDatabase(group.groupID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > group.lastSummarizedGroupMessageId).ToList();
        
        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
             Debug.Log($"[MemoryAgent] '{group.groupName}' 그룹 기억 요약 시작 (대상 메시지: {newMessages.Count}개).");
             await SummarizeAndLearnGroupAsync(group, newMessages);
        }
    }
    
    /// <summary>
    /// 최근 대화를 바탕으로 '현재 상황'을 한 문장으로 요약하여 캐릭터 또는 그룹의 상태를 업데이트합니다.
    /// </summary>
    public async UniTask ProcessCurrentContextAsync(string ownerID, bool isGroup)
    {
        const int CONTEXT_CHUNK_SIZE = 10;
        List<ChatDatabase.ChatMessage> recentMessages;
        
        try
        {
            recentMessages = isGroup
                ? ChatDatabaseManager.Instance.GetRecentGroupMessages(ownerID, CONTEXT_CHUNK_SIZE)
                : ChatDatabaseManager.Instance.GetRecentMessages(ownerID, CONTEXT_CHUNK_SIZE);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryAgent] 현재 상황 요약을 위한 DB 조회 중 예외 발생: {ex.Message}");
            return;
        }

        if (recentMessages == null || recentMessages.Count == 0) return;

        string conversationText = FormatConversation(recentMessages);
        string contextPrompt = PromptHelper.GetContextSummarizationPrompt(conversationText);

        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), contextPrompt);

        if (!string.IsNullOrEmpty(summary) && !summary.Contains("실패"))
        {
            if (isGroup)
            {
                var group = CharacterGroupManager.Instance.GetGroup(ownerID);
                if (group != null)
                {
                    group.currentContextSummary = summary;
                    Debug.Log($"[MemoryAgent] 그룹 '{group.groupName}' 현재 상황 요약 완료: {summary}");
                }
            }
            else
            {
                var preset = CharacterPresetManager.Instance.GetPreset(ownerID);
                if (preset != null)
                {
                    preset.currentContextSummary = summary;
                    Debug.Log($"[MemoryAgent] '{preset.characterName}' 현재 상황 요약 완료: {summary}");
                }
            }
        }
    }
    
    #endregion
    
    #region Private Core Logic
    
    /// <summary>
    /// 개인 캐릭터의 대화를 요약(장기기억)하고, 요약문에서 지식(초장기기억)을 추출합니다.
    /// </summary>
    private async UniTask SummarizeAndLearnAsync(CharacterPreset preset, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages);
        
        // 1. 장기 기억 (요약) 생성
        string summaryPrompt = PromptHelper.GetSummarizationPrompt(preset, conversationText);
        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), summaryPrompt);

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) return;
        
        preset.longTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 개인 장기 기억 생성: {summary}");

        // 2. 초장기 기억 (지식) 추출
        string learningPrompt = PromptHelper.GetLearningPrompt(preset, summary);
        string newKnowledgeJson = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), learningPrompt);
        UpdateLibrary(preset.knowledgeLibrary, newKnowledgeJson);
        
        // 3. 처리 완료된 위치 기록
        preset.lastSummarizedMessageId = messages.Last().Id;
        
        // 4. 개인적인 약속 등은 그룹 기억에도 공유
        if (!string.IsNullOrEmpty(preset.groupID))
        {
            var group = CharacterGroupManager.Instance.GetGroup(preset.groupID);
            if (group != null)
            {
                group.groupLongTermMemories.Add($"(개인 활동) {preset.characterName}: {summary}");
            }
        }
    }
    
    /// <summary>
    /// 그룹 대화를 요약(장기기억)하고, 요약문에서 지식(초장기기억)을 추출합니다.
    /// </summary>
    private async UniTask SummarizeAndLearnGroupAsync(CharacterGroup group, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages, isGroupContext: true);
        
        // 1. 그룹 장기 기억 (요약) 생성
        string summaryPrompt = PromptHelper.GetGroupSummarizationPrompt(group, conversationText);
        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), summaryPrompt);

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) return;

        group.groupLongTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 그룹 장기 기억 생성: {summary}");

        // 2. 그룹 초장기 기억 (지식) 추출
        string learningPrompt = PromptHelper.GetGroupLearningPrompt(group, summary);
        string newKnowledgeJson = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), learningPrompt);
        UpdateLibrary(group.groupKnowledgeLibrary, newKnowledgeJson);

        // 3. 처리 완료된 위치 기록
        group.lastSummarizedGroupMessageId = messages.Last().Id;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// DB 메시지 리스트를 AI가 이해하기 쉬운 대화록 형식의 문자열로 변환합니다.
    /// </summary>
    private string FormatConversation(List<ChatDatabase.ChatMessage> messages, bool isGroupContext = false)
    {
        var formattedLines = messages.Select(m => {
            string speakerName;
            if (m.SenderID.ToLower() == "user")
            {
                speakerName = "사용자";
            }
            else
            {
                var preset = CharacterPresetManager.Instance.GetPreset(m.SenderID);
                speakerName = preset?.characterName ?? "알 수 없는 AI";
            }
            
            var data = JsonUtility.FromJson<MessageData>(m.Message);
            string text = data?.textContent ?? "(내용 없음)";
            return $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] {speakerName}: {text}";
        });
        return string.Join("\n", formattedLines);
    }
    
    /// <summary>
    /// AI가 생성한 지식 JSON을 파싱하여 실제 라이브러리(딕셔너리)에 업데이트합니다.
    /// </summary>
    private void UpdateLibrary(Dictionary<string, string> library, string json)
    {
        try
        {
            var newKnowledge = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (newKnowledge != null)
            {
                foreach (var kvp in newKnowledge)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        library[kvp.Key] = kvp.Value;
                        Debug.Log($"[MemoryAgent] 초장기 기억 업데이트: Key='{kvp.Key}', Value='{kvp.Value}'");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MemoryAgent] 지식(JSON) 파싱 오류: {e.Message}\n원본 JSON: {json}");
        }
    }
    
    #endregion
}