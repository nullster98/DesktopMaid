// --- START OF FILE MemorySystemController.cs ---

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks; // UniTask 네임스페이스 추가

// [신규] MemoryAgent를 관리하고 주기적으로 실행하는 컨트롤러
public class MemorySystemController : MonoBehaviour
{
    public static MemorySystemController Instance { get; private set; }
    
    public float processInterval = 300f; // 5분마다 모든 기억 처리 시도
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
        // [수정] 코루틴 대신 async 메서드 호출
        ProcessAllMemoriesRoutine().Forget();
    }

    // [수정] IEnumerator -> async UniTaskVoid
    private async UniTaskVoid ProcessAllMemoriesRoutine()
    {
        while (true)
        {
            // UniTask.Delay 사용
            await UniTask.Delay(TimeSpan.FromSeconds(processInterval), cancellationToken: this.GetCancellationTokenOnDestroy());
            
            Debug.Log("[MemorySystem] 주기적인 기억 처리 작업을 시작합니다.");

            // 모든 개인 프리셋 처리
            foreach (var preset in CharacterPresetManager.Instance.presets)
            {
                // await로 비동기 작업 대기
                await agent.CheckAndProcessMemoryAsync(preset);
            }

            // 모든 그룹 처리
            foreach (var group in CharacterGroupManager.Instance.allGroups)
            {
                // await로 비동기 작업 대기
                await agent.CheckAndProcessGroupMemoryAsync(group);
            }
        }
    }
}


/// <summary>
/// 대화 기록을 처리하여 요약(장기기억)하고, 지식(초장기기억)을 추출하는 AI 에이전트.
/// </summary>
public class MemoryAgent
{
    // 요약을 시도할 최소 대화 묶음의 크기
    private const int SUMMARY_CHUNK_SIZE = 20;

    // [수정] IEnumerator -> async UniTask, 메서드명에 Async 접미사 추가
    public async UniTask CheckAndProcessMemoryAsync(CharacterPreset preset)
    {
        if (preset == null) return;
        
        var db = ChatDatabaseManager.Instance.GetDatabase(preset.presetID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > preset.lastSummarizedMessageId).ToList();

        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
            Debug.Log($"[MemoryAgent] '{preset.characterName}'의 기억 요약 시작. (처리할 메시지: {newMessages.Count}개)");
            // await로 비동기 작업 대기
            await SummarizeAndLearnAsync(preset, newMessages);
        }
    }
    
    // [수정] IEnumerator -> async UniTask, 메서드명에 Async 접미사 추가
    public async UniTask ProcessCurrentContextAsync(string ownerID, bool isGroup)
    {
        const int CONTEXT_CHUNK_SIZE = 10;
        List<ChatDatabase.ChatMessage> recentMessages;

        try
        {
            if (isGroup)
            {
                var db = ChatDatabaseManager.Instance.GetGroupDatabase(ownerID);
                if (db == null)
                {
                    Debug.LogError($"[MemoryAgent] ProcessCurrentContext: Group database for ID '{ownerID}' not found. Aborting context summary.");
                    return;
                }
                recentMessages = db.GetRecentMessages(CONTEXT_CHUNK_SIZE);
            }
            else
            {
                var db = ChatDatabaseManager.Instance.GetDatabase(ownerID);
                if (db == null)
                {
                    Debug.LogError($"[MemoryAgent] ProcessCurrentContext: Personal database for ID '{ownerID}' not found. Aborting context summary.");
                    return;
                }
                recentMessages = db.GetRecentMessages(CONTEXT_CHUNK_SIZE);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryAgent] 예외 발생: {ex.Message}\n{ex.StackTrace}");
            return;
        }

        if (recentMessages == null || recentMessages.Count == 0) return;

        string conversationText = FormatConversation(recentMessages);
        string contextPrompt = PromptHelper.GetContextSummarizationPrompt(conversationText);

        // [수정] CoroutineRunner 제거, await와 GeminiAPI.AskAsync 사용
        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), contextPrompt);

        if (!string.IsNullOrEmpty(summary) && !summary.Contains("실패"))
        {
            if (isGroup)
            {
                var group = CharacterGroupManager.Instance.GetGroup(ownerID);
                if (group != null)
                {
                    group.currentContextSummary = summary;
                    Debug.Log($"[MemoryAgent] 그룹 '{group.groupName}' 현재 상황 요약: {summary}");
                }
            }
            else
            {
                var preset = CharacterPresetManager.Instance.GetPreset(ownerID);
                if (preset != null)
                {
                    preset.currentContextSummary = summary;
                    Debug.Log($"[MemoryAgent] '{preset.characterName}' 현재 개인 상황 요약: {summary}");
                }
            }
        }
    }

    // [수정] IEnumerator -> async UniTask, 메서드명에 Async 접미사 추가
    public async UniTask CheckAndProcessGroupMemoryAsync(CharacterGroup group)
    {
        if (group == null) return;
        
        var db = ChatDatabaseManager.Instance.GetGroupDatabase(group.groupID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > group.lastSummarizedGroupMessageId).ToList();
        
        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
             Debug.Log($"[MemoryAgent] '{group.groupName}' 그룹의 역사 요약 시작. (처리할 메시지: {newMessages.Count}개)");
             await SummarizeAndLearnGroupAsync(group, newMessages);
        }
    }

    // [수정] IEnumerator -> async UniTask, 메서드명에 Async 접미사 추가
    private async UniTask SummarizeAndLearnAsync(CharacterPreset preset, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages);
        string summaryPrompt = PromptHelper.GetSummarizationPrompt(preset, conversationText);

        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), summaryPrompt);

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) return;
        
        preset.longTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 개인 기억 요약 생성: {summary}");

        string learningPrompt = PromptHelper.GetLearningPrompt(preset, summary);
        
        string newKnowledgeJson = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), learningPrompt);

        UpdateLibrary(preset.knowledgeLibrary, newKnowledgeJson);
        
        preset.lastSummarizedMessageId = messages.Last().Id;
        
        if (!string.IsNullOrEmpty(summary) && !summary.Contains("요약할 내용 없음"))
        {
            if (!string.IsNullOrEmpty(preset.groupID))
            {
                var group = CharacterGroupManager.Instance.GetGroup(preset.groupID);
                if (group != null)
                {
                    group.groupLongTermMemories.Add($"(개인 약속) {preset.characterName}: {summary}");
                }
            }
        }
    }
    
    // [수정] IEnumerator -> async UniTask, 메서드명에 Async 접미사 추가
    private async UniTask SummarizeAndLearnGroupAsync(CharacterGroup group, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages, true);
        string summaryPrompt = PromptHelper.GetGroupSummarizationPrompt(group, conversationText);

        string summary = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), summaryPrompt);

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) return;

        group.groupLongTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 그룹 역사 요약 생성: {summary}");

        string learningPrompt = PromptHelper.GetGroupLearningPrompt(group, summary);

        string newKnowledgeJson = await GeminiAPI.AskAsync(UserData.Instance.GetAPIKey(), learningPrompt);

        UpdateLibrary(group.groupKnowledgeLibrary, newKnowledgeJson);

        group.lastSummarizedGroupMessageId = messages.Last().Id;
    }

    // ... FormatConversation, UpdateLibrary 메서드는 변경 없음 ...
    private string FormatConversation(List<ChatDatabase.ChatMessage> messages, bool isGroup = false)
    {
        // 이전 답변에서 제안한 data?.textContent 수정이 적용되었다고 가정합니다.
        var formattedLines = messages.Select(m => {
            string speakerName = "사용자";
            if (m.SenderID != "user")
            {
                var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == m.SenderID);
                speakerName = preset?.characterName ?? "알 수 없는 AI";
            }
            
            var data = JsonUtility.FromJson<MessageData>(m.Message);
            string text = data?.textContent ?? "(내용 없음)";
            return $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] {speakerName}: {text}";
        });
        return string.Join("\n", formattedLines);
    }
    
    private void UpdateLibrary(Dictionary<string, string> library, string json)
    {
        try
        {
            var parsedJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (parsedJson != null)
            {
                foreach (var kvp in parsedJson)
                {
                    library[kvp.Key] = kvp.Value;
                    Debug.Log($"[MemoryAgent] 지식 라이브러리 업데이트: {kvp.Key} = {kvp.Value}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MemoryAgent] 지식(JSON) 파싱 오류: {e.Message}\n원본: {json}");
        }
    }
}


// [삭제] 이 클래스는 더 이상 필요 없으므로 파일에서 완전히 삭제하세요.
// public class CoroutineRunner : MonoBehaviour
// {
//     public static CoroutineRunner Instance;
//     void Awake() { if(Instance == null) Instance = this; }
// }

// --- END OF FILE MemorySystemController.cs ---