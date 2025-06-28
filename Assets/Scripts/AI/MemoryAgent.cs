// --- START OF FILE MemoryAgent.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// [신규] MemoryAgent를 관리하고 주기적으로 실행하는 컨트롤러
public class MemorySystemController : MonoBehaviour
{
    public static MemorySystemController Instance { get; private set; }
    
    public float processInterval = 300f; // 5분마다 모든 기억 처리 시도
    private MemoryAgent agent;

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
        StartCoroutine(ProcessAllMemoriesRoutine());
    }

    private IEnumerator ProcessAllMemoriesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(processInterval);
            
            Debug.Log("[MemorySystem] 주기적인 기억 처리 작업을 시작합니다.");

            // 모든 개인 프리셋 처리
            foreach (var preset in CharacterPresetManager.Instance.presets)
            {
                yield return StartCoroutine(agent.CheckAndProcessMemory(preset));
            }

            // 모든 그룹 처리
            foreach (var group in CharacterGroupManager.Instance.allGroups)
            {
                yield return StartCoroutine(agent.CheckAndProcessGroupMemory(group));
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

    /// <summary>
    /// 지정된 프리셋의 새로운 대화 기록을 확인하고, 충분히 쌓였으면 요약을 시작합니다.
    /// </summary>
    public IEnumerator CheckAndProcessMemory(CharacterPreset preset)
    {
        if (preset == null) yield break;
        
        var db = ChatDatabaseManager.Instance.GetDatabase(preset.presetID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > preset.lastSummarizedMessageId).ToList();

        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
            Debug.Log($"[MemoryAgent] '{preset.characterName}'의 기억 요약 시작. (처리할 메시지: {newMessages.Count}개)");
            yield return CoroutineRunner.Instance.StartCoroutine(SummarizeAndLearn(preset, newMessages));
        }
    }

    /// <summary>
    /// 지정된 그룹의 새로운 대화 기록을 확인하고, 충분히 쌓였으면 요약을 시작합니다.
    /// </summary>
    public IEnumerator CheckAndProcessGroupMemory(CharacterGroup group)
    {
        if (group == null) yield break;
        
        var db = ChatDatabaseManager.Instance.GetGroupDatabase(group.groupID);
        var newMessages = db.GetAllMessages().Where(m => m.Id > group.lastSummarizedGroupMessageId).ToList();
        
        if (newMessages.Count >= SUMMARY_CHUNK_SIZE)
        {
             Debug.Log($"[MemoryAgent] '{group.groupName}' 그룹의 역사 요약 시작. (처리할 메시지: {newMessages.Count}개)");
             yield return CoroutineRunner.Instance.StartCoroutine(SummarizeAndLearnGroup(group, newMessages));
        }
    }

    // 대화를 요약하고, 라이브러리를 업데이트하는 개인용 코루틴
    private IEnumerator SummarizeAndLearn(CharacterPreset preset, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages);
        string summaryPrompt = PromptHelper.GetSummarizationPrompt(preset, conversationText);

        string summary = "";
        yield return CoroutineRunner.Instance.StartCoroutine(GeminiAPI.SendTextPrompt(summaryPrompt, UserData.Instance.GetAPIKey(),
            onSuccess: (result) => { summary = result; }
        ));

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) yield break;
        
        preset.longTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 개인 기억 요약 생성: {summary}");

        string learningPrompt = PromptHelper.GetLearningPrompt(preset, summary);
        
        string newKnowledgeJson = "";
        yield return CoroutineRunner.Instance.StartCoroutine(GeminiAPI.SendTextPrompt(learningPrompt, UserData.Instance.GetAPIKey(),
            onSuccess: (result) => { newKnowledgeJson = result; }
        ));

        UpdateLibrary(preset.knowledgeLibrary, newKnowledgeJson);
        
        preset.lastSummarizedMessageId = messages.Last().Id;
    }
    
    // 그룹 대화 요약 및 학습 코루틴
    private IEnumerator SummarizeAndLearnGroup(CharacterGroup group, List<ChatDatabase.ChatMessage> messages)
    {
        string conversationText = FormatConversation(messages, true);
        string summaryPrompt = PromptHelper.GetGroupSummarizationPrompt(group, conversationText);

        string summary = "";
        yield return CoroutineRunner.Instance.StartCoroutine(GeminiAPI.SendTextPrompt(summaryPrompt, UserData.Instance.GetAPIKey(),
            onSuccess: (result) => { summary = result; }
        ));

        if (string.IsNullOrEmpty(summary) || summary.Contains("요약할 내용 없음")) yield break;

        group.groupLongTermMemories.Add(summary);
        Debug.Log($"[MemoryAgent] 그룹 역사 요약 생성: {summary}");

        string learningPrompt = PromptHelper.GetGroupLearningPrompt(group, summary);

        string newKnowledgeJson = "";
        yield return CoroutineRunner.Instance.StartCoroutine(GeminiAPI.SendTextPrompt(learningPrompt, UserData.Instance.GetAPIKey(),
            onSuccess: (result) => { newKnowledgeJson = result; }
        ));

        UpdateLibrary(group.groupKnowledgeLibrary, newKnowledgeJson);

        group.lastSummarizedGroupMessageId = messages.Last().Id;
    }

    // 대화 기록을 AI가 이해하기 쉬운 텍스트로 변환
    private string FormatConversation(List<ChatDatabase.ChatMessage> messages, bool isGroup = false)
    {
        var formattedLines = messages.Select(m => {
            string speakerName = "사용자";
            if (m.SenderID != "user")
            {
                var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == m.SenderID);
                speakerName = preset?.characterName ?? "알 수 없는 AI";
            }
            
            var data = JsonUtility.FromJson<MessageData>(m.Message);
            string text = data.textContent ?? "(내용 없음)";
            return $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] {speakerName}: {text}";
        });
        return string.Join("\n", formattedLines);
    }
    
    // JSON 형식의 지식을 Dictionary에 업데이트
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

// MonoBehaviour가 아닌 클래스에서 코루틴을 실행하기 위한 헬퍼
public class CoroutineRunner : MonoBehaviour
{
    public static CoroutineRunner Instance;
    void Awake() { if(Instance == null) Instance = this; }
}