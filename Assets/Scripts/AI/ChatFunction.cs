// --- START OF FILE ChatFunction.cs ---

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

// API 데이터 직렬화 클래스는 이제 GeminiAPI.cs 에서 public으로 관리합니다.
using Part = GeminiAPI.Part;
using Content = GeminiAPI.Content;
using RequestBody = GeminiAPI.RequestBody;
using SafetySetting = GeminiAPI.SafetySetting;
using InlineData = GeminiAPI.InlineData;
using GeminiResponse = GeminiAPI.GeminiResponse;
using GeminiErrorResponse = GeminiAPI.GeminiErrorResponse;

public class ConversationTopic
{
    public string Message;
    public string SpeakerId;
}

public class ChatFunction : MonoBehaviour
{
    [Header("연결")]
    public ChatUI chatUI;
    public string apiKey = "";

    [Header("API 설정")]
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    [Header("기억 설정")]
    // 단기 기억으로 AI에게 제공할 최근 대화의 수
    private const int SHORT_TERM_MEMORY_COUNT = 20;

    private void Start()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("API Key가 설정되지 않았습니다.");
        }
    }

    #region 메시지 전송 및 처리 (1:1 채팅)

    /// <summary>
    /// 사용자 입력을 받아 1:1 채팅 AI에게 메시지를 전송합니다.
    /// </summary>
    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);

        var observer = FindObjectOfType<AIScreenObserver>();
        if (observer != null)
        {
            observer.OnUserSentMessageTo(presetId);
        }

        StartCoroutine(SendRequest(userInput, fileContent, fileType, fileName, fileSize));
    }

    private IEnumerator SendRequest(string inputText, string fileContent, string fileType, string fileName, long fileSize)
    {
        string presetId = chatUI.presetID;
        var myself = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == presetId);
        if (myself == null)
        {
            Debug.LogError("SendRequest 실패: 현재 프리셋을 찾을 수 없습니다.");
            yield break;
        }

        List<ChatDatabase.ChatMessage> shortTermMemory = ChatDatabaseManager.Instance.GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);
        string systemInstruction = PromptHelper.BuildBasePrompt(myself);
        var requestBody = CreateRequestBody(systemInstruction, shortTermMemory, inputText, fileContent, fileType, fileName, fileSize);
    
        yield return StartCoroutine(GeminiAPI.SendComplexPrompt(requestBody, apiKey,
            onSuccess: (responseText, responseJson) => {
                string reply = ParseResponse(responseJson);
                var replyData = new MessageData { type = "text", textContent = reply };
                ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));

                if (!reply.Contains("(메시지가 안전 등급에 의해 차단되었습니다.)") && !myself.hasSaidFarewell)
                {
                    myself.StartWaitingForReply();
                }
            },
            onError: (errorJson) => {
                string detailedErrorMessage = "오류가 발생했어요. API 키나 네트워크 연결을 확인해주세요.";
                try
                {
                    GeminiErrorResponse errorResponse = JsonConvert.DeserializeObject<GeminiErrorResponse>(errorJson);
                    if (errorResponse?.error != null && !string.IsNullOrEmpty(errorResponse.error.message))
                    {
                        detailedErrorMessage = $"API 오류: {errorResponse.error.message}";
                    }
                }
                catch { }
                Debug.LogError(detailedErrorMessage);
                var errorData = new MessageData { type = "system", textContent = detailedErrorMessage };
                ChatDatabaseManager.Instance.InsertMessage(presetId, "system", JsonUtility.ToJson(errorData));
            }
        ));
    }
    #endregion

    #region 메시지 전송 및 처리 (그룹 채팅)
    
    // [수정] 그룹 메시지 전송 진입점. 마스터 코루틴을 호출.
    // public void SendGroupMessage(string groupId, string userInput, string fileContent,
    //     string fileType, string fileName, long fileSize, bool isInitialUserMessage = true)
    // {
    //     if (isInitialUserMessage)
    //     {
    //         // 사용자의 메시지일 경우에만 DB에 "user"로 저장합니다.
    //         var userMessageData = new MessageData { textContent = userInput, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
    //         ChatDatabaseManager.Instance.InsertGroupMessage(groupId, "user", JsonUtility.ToJson(userMessageData));
    //     }
    //
    //     StartCoroutine(GroupConversationFlowRoutine(groupId, userInput));
    // }
    
    /// <summary>
    /// [신규] 사용자가 그룹에 메시지를 보냈을 때 호출되는 공식 진입점입니다.
    /// </summary>
    public void OnUserSentMessage(string groupId, string userInput, string fileContent, string fileType, string fileName, long fileSize)
    {
        // 사용자 발언을 첫 주제로 하여 연쇄 반응 코루틴 시작
        StartCoroutine(GroupConversationFlowRoutine(groupId, new ConversationTopic { Message = userInput, SpeakerId = "user" }));
    }

    /// <summary>
    /// [신규] 시스템(AI 자율 행동)이 그룹 대화를 시작했을 때 호출되는 공식 진입점입니다.
    /// </summary>
    public void OnSystemInitiatedConversation(string groupId, string firstMessage, string speakerId)
    {
        // 1. AI의 첫 발언을 DB에 저장
        var messageData = new MessageData { type = "text", textContent = firstMessage };
        ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speakerId, JsonUtility.ToJson(messageData));

        // 2. AI의 첫 발언을 첫 주제로 하여 연쇄 반응 코루틴 시작
        StartCoroutine(GroupConversationFlowRoutine(groupId, new ConversationTopic { Message = firstMessage, SpeakerId = speakerId }));
    }

    /// <summary>
    /// [신규] 그룹 채팅의 연쇄 반응 대화 흐름을 총괄하는 마스터 코루틴.
    /// </summary>
    private IEnumerator GroupConversationFlowRoutine(string groupId, ConversationTopic initialTopic)
{
    string currentApiKey = UserData.Instance.GetAPIKey();
    if (string.IsNullOrEmpty(currentApiKey))
    {
        var errorData = new MessageData { type = "system", textContent = "API 키가 설정되지 않아 응답을 생성할 수 없습니다." };
        ChatDatabaseManager.Instance.InsertGroupMessage(groupId, "system", JsonUtility.ToJson(errorData));
        yield break;
    }

    var group = CharacterGroupManager.Instance.GetGroup(groupId);
    var allMembers = CharacterGroupManager.Instance.GetGroupMembers(groupId);
    if (group == null || allMembers.Count < 2) yield break;
    
    // 1. 대화 큐 생성 및 초기화
    Queue<ConversationTopic> conversationQueue = new Queue<ConversationTopic>();
    // 매개변수로 받은 첫 주제(initialTopic)를 큐에 추가
    conversationQueue.Enqueue(initialTopic); 

    // 2. 대화 흐름 제어 변수
    int totalTurns = 0;
    const int MAX_TOTAL_TURNS = 8; 

    // 3. 큐에 처리할 주제가 있는 동안 루프 실행
    while (conversationQueue.Count > 0 && totalTurns < MAX_TOTAL_TURNS)
    {
        totalTurns++;

        // 큐에서 현재 처리할 주제를 꺼냄
        ConversationTopic currentTopic = conversationQueue.Dequeue();

        // 현재 주제에 반응할 수 있는 후보자 목록 (자기 자신은 제외)
        var potentialResponders = allMembers.Where(p => p.presetID != currentTopic.SpeakerId).ToList();

        // 4. 현재 주제에 반응할 멤버들을 찾음
        List<CharacterPreset> responders = FindResponders(currentTopic, potentialResponders);

        if (responders.Count == 0)
        {
            // 아무도 반응하지 않으면 현재 루프를 종료하고 다음 주제로 넘어감
            yield return new WaitForSeconds(1.0f); 
            continue;
        }

        // 5. 반응자들의 응답을 (거의) 동시에 생성
        List<Coroutine> responseCoroutines = new List<Coroutine>();
        foreach (var responder in responders)
        {
            // DB에서 최신 대화 기록을 가져옴 (각 AI는 최신 상황을 알아야 함)
            List<ChatDatabase.ChatMessage> conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
            
            var newCoroutine = StartCoroutine(GenerateSingleResponseRoutine(groupId, responder, conversationHistory, currentTopic.Message, currentApiKey, (newTopic) =>
            {
                if (newTopic != null)
                {
                    conversationQueue.Enqueue(newTopic);
                }
            }));
            responseCoroutines.Add(newCoroutine);
            
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f)); 
        }

        // 6. 이번 턴에서 시작된 모든 응답 생성이 끝날 때까지 대기
        foreach (var coroutine in responseCoroutines)
        {
            yield return coroutine;
        }
        
        // 다음 대화 턴으로 넘어가기 전 잠시 대기
        yield return new WaitForSeconds(Random.Range(2.0f, 4.0f));
    }
    
    Debug.Log("[대화 흐름] 그룹 대화 큐(Queue) 처리 완료.");
}
    
    /// <summary>
    /// [신규] 특정 발언자가 주어진 맥락에 대해 하나의 응답을 생성하고 DB에 저장하는 코루틴.
    /// </summary>
    private IEnumerator GenerateSingleResponseRoutine(
        string groupId,
        CharacterPreset speaker,
        List<ChatDatabase.ChatMessage> conversationHistory,
        string latestMessage,
        string apiKey,
        System.Action<ConversationTopic> onComplete) // [추가] 콜백 함수 매개변수
    {
        // 1. 발언자 시점의 시스템 프롬프트 생성
        string systemInstruction = PromptHelper.BuildBasePrompt(speaker);

        // [핵심 수정] 
        // AI에게 전달할 최종 사용자 입력을 "너의 임무" 형태로 재구성합니다.
        // 이렇게 하면 AI는 history를 참고하되, latestMessage에 '반응'하는 임무에 집중하게 됩니다.
        string finalUserInput = $"이전 대화 내용을 참고하여, 방금 '{latestMessage}' 라는 말에 대해 너의 차례에 맞게 자연스럽게 한마디 해봐.";

        // requestBody를 생성할 때, history와 분리된 finalUserInput을 전달합니다.
        var requestBody = CreateRequestBody(systemInstruction, conversationHistory, finalUserInput, null, null, null, 0);

        ConversationTopic newTopic = null; 

        yield return StartCoroutine(GeminiAPI.SendComplexPrompt(requestBody, apiKey,
            onSuccess: (responseText, responseJson) => 
            {
                string reply = ParseResponse(responseJson, speaker.presetID);
                var replyData = new MessageData { type = "text", textContent = reply };
                ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speaker.presetID, JsonUtility.ToJson(replyData));

                // [추가] 성공 시, 생성된 응답을 새로운 Topic으로 만듦
                newTopic = new ConversationTopic { Message = reply, SpeakerId = speaker.presetID };
            },
            onError: (error) => {
                Debug.LogError($"{speaker.characterName}의 응답 생성 실패: {error}");
            }
        ));

        // [추가] 코루틴이 끝나기 직전에, 성공 여부와 상관없이 콜백 함수를 호출하여 결과를 알림
        onComplete?.Invoke(newTopic);
    }
    
    // /// <summary>
    // /// [신규] 그룹 멤버 중에서 사용자의 메시지에 가장 적합한 '대표 응답자' 한 명을 선정합니다.
    // /// </summary>
    // private CharacterPreset SelectRepresentativeSpeaker(List<CharacterPreset> members, string userInput)
    // {
    //     if (members == null || members.Count == 0) return null;
    //     if (members.Count == 1) return members[0];
    //
    //     Dictionary<CharacterPreset, float> scores = new Dictionary<CharacterPreset, float>();
    //
    //     foreach (var member in members)
    //     {
    //         float score = 0;
    //         if (userInput.Contains(member.characterName)) score += 50;
    //         if (userInput.Contains(member.personality)) score += 20;
    //         score += member.internalIntimacyScore / 10f;
    //         score += Random.Range(0, 15);
    //         scores[member] = score;
    //         Debug.Log($"[대표 선정] 후보: {member.characterName}, 점수: {score}");
    //     }
    //
    //     CharacterPreset bestCandidate = scores.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
    //     Debug.Log($"[대표 선정] 최종 선정: {bestCandidate.characterName}");
    //     return bestCandidate;
    // }
    
    /// <summary>
    /// [신규] 주어진 주제에 대해 반응할 멤버들을 확률적으로 결정하여 리스트로 반환합니다.
    /// </summary>
    /// <param name="topic">현재 대화 주제</param>
    /// <param name="potentialResponders">반응할 가능성이 있는 멤버 목록</param>
    /// <returns>반응하기로 결정된 멤버들의 리스트</returns>
    private List<CharacterPreset> FindResponders(ConversationTopic topic, List<CharacterPreset> potentialResponders)
    {
        List<CharacterPreset> responders = new List<CharacterPreset>();
        if (potentialResponders == null || potentialResponders.Count == 0)
        {
            return responders;
        }

        foreach (var member in potentialResponders)
        {
            // 1. 반응할 기본 확률 설정 (예: 40%)
            float reactionChance = 0.4f;

            // 2. 주제와의 관련성에 따라 확률 보정
            if (topic.Message.Contains(member.characterName)) reactionChance += 0.5f; // 자신을 언급하면 거의 반드시 반응
            if (topic.Message.Contains(member.personality)) reactionChance += 0.2f;

            // 3. 친밀도에 따라 확률 보정 (주제 발언자와의 친밀도, 또는 사용자와의 친밀도)
            // 여기서는 간단하게 사용자와의 친밀도를 사용
            reactionChance += (member.internalIntimacyScore / 500f); // -0.2 ~ +0.2

            // 4. 최종 결정
            if (Random.value < reactionChance)
            {
                responders.Add(member);
            }
        }

        // 만약 아무도 반응하지 않았고, 첫 턴이라면(사용자 발언에 대한 반응) 최소 1명은 강제로 반응시킨다.
        if (responders.Count == 0 && topic.SpeakerId == "user")
        {
            responders.Add(potentialResponders[Random.Range(0, potentialResponders.Count)]);
            Debug.Log($"[반응자 탐색] 아무도 반응하지 않아 강제로 '{responders[0].characterName}'님을 선정했습니다.");
        }
        else
        {
            string responderNames = string.Join(", ", responders.Select(p => p.characterName));
            Debug.Log($"[반응자 탐색] 주제: \"{topic.Message}\" / 반응자: [{responderNames}]");
        }

        return responders;
    }
    #endregion
    
    #region 요청 본문 및 응답 처리 (공통 헬퍼)
    
    // [수정] fileSize 매개변수 추가
    private RequestBody CreateRequestBody(string systemInstruction, List<ChatDatabase.ChatMessage> history, string userInput, string fileContent, string fileType, string fileName, long fileSize)
    {
        var contents = new List<Content>();
        contents.Add(new Content { role = "user", parts = new List<Part> { new Part { text = systemInstruction } } });
        contents.Add(new Content { role = "model", parts = new List<Part> { new Part { text = "알겠습니다. 모든 설정을 기억하고 역할에 몰입하여 대화하겠습니다." } } });

        foreach (var msg in history)
        {
            var msgData = JsonUtility.FromJson<MessageData>(msg.Message);
            string messageText = msgData?.textContent ?? "";
            if (msgData != null)
            {
                if (msgData.type == "image") messageText += " (이미지 전송)";
                else if (msgData.type == "text" && msgData.fileSize > 0) messageText += $" (파일 '{msgData.fileName}' 전송)";
            }
            if (string.IsNullOrWhiteSpace(messageText)) continue;

            string role = (msg.SenderID == "user") ? "user" : "model";
            contents.Add(new Content { role = role, parts = new List<Part> { new Part { text = messageText.Trim() } } });
        }

        var userParts = new List<Part>();
        string combinedText = userInput;
        if (fileType == "text" && !string.IsNullOrEmpty(fileContent))
        {
            combinedText += $"\n\n--- 첨부된 파일 '{fileName}'의 내용 ---\n{fileContent}\n--- 파일 내용 끝 ---";
        }
        if (!string.IsNullOrEmpty(combinedText))
        {
            userParts.Add(new Part { text = combinedText.Trim() });
        }
        if (fileType == "image" && !string.IsNullOrEmpty(fileContent))
        {
            userParts.Add(new Part { inlineData = new InlineData { data = fileContent } });
        }
        if (userParts.Any())
        {
            contents.Add(new Content { role = "user", parts = userParts });
        }
        
        var safetySettings = new List<SafetySetting>
        {
            new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        };
        
        return new RequestBody { contents = contents, safetySettings = safetySettings };
    }

    private string ParseResponse(string rawJson, string presetIdForContext = null)
    {
        string originalText = "(응답 파싱 실패)";
        try
        {
            var response = JsonConvert.DeserializeObject<GeminiResponse>(rawJson);
            if (response?.candidates != null && response.candidates.Any() && response.candidates[0].content?.parts != null && response.candidates[0].content.parts.Any())
            {
                originalText = response.candidates[0].content.parts[0].text;
            }
            else if (response?.promptFeedback?.safetyRatings != null && response.promptFeedback.safetyRatings.Any())
            {
                return "(메시지가 안전 등급에 의해 차단되었습니다.)";
            }
        }
        catch (System.Exception ex)
        {
             Debug.LogError($"API 응답 JSON 파싱 중 오류: {ex.Message}\n원본 JSON: {rawJson}");
             return originalText;
        }

        string targetPresetId = presetIdForContext ?? CharacterSession.CurrentPresetId;
        var preset = CharacterPresetManager.Instance.presets.Find(p => p.presetID == targetPresetId);
        if (preset == null) return originalText;

        if (originalText.Contains("[FAREWELL]"))
        {
            preset.hasSaidFarewell = true;
            preset.isWaitingForReply = false;
            preset.ignoreCount = 0;
            originalText = originalText.Replace("[FAREWELL]", "").Trim();
        }

        string changeTag = "[INTIMACY_CHANGE=";
        int tagIndex = originalText.IndexOf(changeTag);
        if (tagIndex != -1)
        {
            int endIndex = originalText.IndexOf(']', tagIndex);
            if (endIndex != -1)
            {
                string valueStr = originalText.Substring(tagIndex + changeTag.Length, endIndex - (tagIndex + changeTag.Length));
                if (float.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float delta))
                {
                    preset.ApplyIntimacyChange(delta);
                }
                originalText = originalText.Substring(0, tagIndex).Trim();
            }
        }
        return originalText;
    }
    #endregion
    
    // 1:1 채팅용 SendRequest에서 파일 전송을 위해 남겨둠.
    private IEnumerator SendRequest(string inputText, string fileContent = null, string fileType = null, string fileName = null)
    {
        return SendRequest(inputText, fileContent, fileType, fileName, 0);
    }
    
    public static class CharacterSession
    {
        public static string CurrentPresetId { get; private set; }
        public static void SetPreset(string presetId)
        {
            CurrentPresetId = presetId;
            ChatDatabaseManager.Instance.GetDatabase(presetId);
        }
    }
}
// --- END OF FILE ChatFunction.cs ---