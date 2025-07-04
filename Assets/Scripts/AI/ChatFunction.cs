// --- START OF FILE ChatFunction.cs (그룹 채팅 수정 최종본) ---

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;
using AI;

public class ChatFunction : MonoBehaviour
{
    #region 변수 및 초기화

    [Header("필수 연결")]
    public ChatUI chatUI;

    private const int SHORT_TERM_MEMORY_COUNT = 20;
    private AIConfig cfg;
    
    [Header("그룹 대화 설정")]
    [Tooltip("한 번에 생성할 최대 추가 턴 수")]
    public int maxLoopTurns = 3;
    [Tooltip("1턴 이후 대화를 계속할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float continueChance = 0.7f;

    private void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");
        if (cfg == null)
        {
            Debug.LogError("[ChatFunction] AIConfig 파일을 Resources 폴더에서 찾을 수 없습니다!");
        }
    }

    #endregion

    #region 1:1 채팅 로직 (변경 없음)

    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);
        FindObjectOfType<AIScreenObserver>()?.OnUserSentMessageTo(presetId);
        SendRequestAsync(userInput, fileContent, fileType, fileName, fileSize).Forget();
    }

    private async UniTaskVoid SendRequestAsync(string inputText, string fileContent, string fileType, string fileName, long fileSize)
    {
        if (chatUI == null)
        {
            Debug.LogError("[ChatFunction] chatUI가 할당되지 않았습니다!");
            return;
        }
        
        string presetId = chatUI.presetID;
        if (string.IsNullOrEmpty(presetId))
        {
            Debug.LogError("[ChatFunction] chatUI.presetID가 비어 있습니다!");
            return;
        }

        var manager = CharacterPresetManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[ChatFunction] CharacterPresetManager.Instance가 null입니다!");
            return;
        }

        var myself = manager.GetPreset(presetId);
        if (myself == null)
        {
            Debug.LogError($"SendRequestAsync 실패: 프리셋 ID '{presetId}'가 CharacterPresetManager에 없습니다.");
            return;
        }

        Debug.Log("[DBG] ▶ try 진입");                         // ①
        Debug.Log($"[DBG] SHORT_TERM_MEMORY_COUNT = {SHORT_TERM_MEMORY_COUNT}"); // ②
        
        try
        {
            Debug.Log("[DBG] ▶ AskAsync 분기 전");
            
            string reply;
            List<ChatDatabase.ChatMessage> shortTermMemory = ChatDatabaseManager.Instance.GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);
            Debug.Log($"[DBG] recentMessages count = {shortTermMemory.Count}");
            
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            Debug.Log($"[DBG] cancellationToken = {cancellationToken.IsCancellationRequested}");

            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                Debug.Log("[DBG] OllamaHttp 분기");
                var messages = new List<OllamaMessage>();
                string systemPrompt = PromptHelper.BuildBasePrompt(myself);
                messages.Add(new OllamaMessage { role = "system", content = systemPrompt });

                foreach (var msg in shortTermMemory)
                {
                    string role = (msg.SenderID == "user") ? "user" : "assistant";
                    var messageData = JsonUtility.FromJson<MessageData>(msg.Message);
                    if (messageData != null)
                    {
                        messages.Add(new OllamaMessage { role = role, content = messageData.textContent });
                    }
                }

                var userMessage = new OllamaMessage { role = "user", content = inputText };
                if (fileType == "image" && !string.IsNullOrEmpty(fileContent))
                {
                    if (File.Exists(fileContent))
                        fileContent = Convert.ToBase64String(File.ReadAllBytes(fileContent));
                    userMessage.images = new List<string> { fileContent.Trim() };
                }
                messages.Add(userMessage);

                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else
            {
                Debug.Log("[DBG] GeminiApi 분기"); 
                string contextPrompt = PromptHelper.BuildFullChatContextPrompt(myself, shortTermMemory);
                Debug.Log($"[DBG] contextPrompt length = {contextPrompt?.Length}");
                string finalPrompt = contextPrompt +
                    "\n\n--- 현재 임무 ---\n" +
                    "지금까지의 모든 대화와 설정을 바탕으로, 아래의 사용자 발언에 대해 자연스럽게 대답해라.\n" +
                    $"사용자 발언: \"{inputText}\"";
                Debug.Log($"[DBG] finalPrompt length = {finalPrompt.Length}");

                string imageBase64 = null;
                if (fileType == "text" && !string.IsNullOrEmpty(fileContent))
                {
                    finalPrompt += $"\n\n--- 첨부된 파일 '{fileName}'의 내용 ---\n{fileContent}";
                }
                else if (fileType == "image")
                {
                    if (File.Exists(fileContent))
                        fileContent = Convert.ToBase64String(File.ReadAllBytes(fileContent));
                    imageBase64 = fileContent.Trim();
                }
                reply = await ChatService.AskAsync(finalPrompt, imageBase64, null, cancellationToken);
                Debug.Log($"[DBG] AskAsync 반환 길이 = {reply?.Length}");
            }

            string parsedReply = ParseResponse(reply, myself.presetID);
            var replyData = new MessageData { type = "text", textContent = parsedReply };
            ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));
            
            if (MemorySystemController.Instance != null)
            {
                StartCoroutine(
                    MemorySystemController.Instance.agent.CheckAndProcessMemory(
                        CharacterPresetManager.Instance.GetPreset(presetId)
                        )
                    );
            }

            if (!parsedReply.Contains("차단") && !myself.hasSaidFarewell)
            {
                myself.StartWaitingForReply();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatFunction] 1:1 채팅 AskAsync 호출 중 오류: {ex.Message}\n{ex.StackTrace}");
            string errorMessage = "오류가 발생했어요. API 키, 네트워크 연결, 또는 로컬 모델 설정을 확인해주세요.";
            var errorData = new MessageData { type = "system", textContent = errorMessage };
            ChatDatabaseManager.Instance.InsertMessage(presetId, "system", JsonUtility.ToJson(errorData));
        }
    }

    #endregion

    #region 그룹 채팅 로직 (핵심 수정 영역)

    public void OnUserSentMessage(string groupId, string userInput, string fileContent, string fileType, string fileName, long fileSize)
    {
        if (cfg.modelMode == ModelMode.GemmaLocal && (fileContent != null || fileSize > 0))
        {
            var errorData = new MessageData { type = "system", textContent = "(현재 로컬 AI 모델에서는 파일 및 이미지 첨부를 지원하지 않습니다.)" };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, "system", JsonUtility.ToJson(errorData));
            if (string.IsNullOrWhiteSpace(userInput)) return;
        }
        GroupConversationFlowAsync(groupId, "user").Forget();
    }

    public void OnSystemInitiatedConversation(string groupId, string firstMessage, string speakerId)
    {
        var messageData = new MessageData { type = "text", textContent = firstMessage };
        ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speakerId, JsonUtility.ToJson(messageData));
        GroupConversationFlowAsync(groupId, speakerId).Forget();
    }

    private async UniTask GroupConversationFlowAsync(string groupId, string initialSpeakerId)
{
    var group = CharacterGroupManager.Instance.GetGroup(groupId);
    var allMembers = CharacterGroupManager.Instance.GetGroupMembers(groupId)
        .Where(p => p.CurrentMode == CharacterMode.Activated)
        .ToList();
    if (group == null || allMembers.Count == 0) return;

    // 첫 응답 전 랜덤 지연으로 자연스러운 템포
    await UniTask.Delay(Random.Range(800, 1800), cancellationToken: this.GetCancellationTokenOnDestroy());

    var participated = new HashSet<string> { initialSpeakerId };
    string lastSpeakerId = initialSpeakerId;
    int consecutiveCount = 1;           // initialSpeakerId가 이미 1턴 사용됨
    string lastGeneratedMessage = null;  // 중복 답변 방지용
    int turn = 0;

    while (turn < maxLoopTurns)
    {
        // 2턴 이후 꼬리물기 중단 확률
        if (turn > 0 && Random.value > continueChance)
            break;

        // 후보군 필터링: 연속 3번째 연속 화자는 제외
        var candidates = allMembers
            .Where(p => !(p.presetID == lastSpeakerId && consecutiveCount >= 2))
            .ToList();

        if (candidates.Count == 0)
        {
            // 모두 제외됐다면 원래대로
            candidates = allMembers;
        }

        // 응답자 결정 (이미 발언한 멤버는 낮은 확률, 신규 멤버는 높은 확률)
        CharacterPreset nextSpeaker = FindNextResponder(candidates, participated);
        if (nextSpeaker == null)
            break;

        // 연속 발언 카운트 조정
        if (nextSpeaker.presetID == lastSpeakerId)
        {
            consecutiveCount++;
        }
        else
        {
            lastSpeakerId = nextSpeaker.presetID;
            consecutiveCount = 1;
        }

        bool isFinalTurn = (turn >= maxLoopTurns - 1);
        Debug.Log($"[GroupChat] 턴 {turn + 1}/{maxLoopTurns}, 화자: {nextSpeaker.characterName}, 최종?: {isFinalTurn}");

        // AI 응답 생성
        string generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker, isFinalTurn);
        if (string.IsNullOrEmpty(generatedMessage))
            break;

        // 동일 내용 반복 방지
        if (lastGeneratedMessage != null && generatedMessage == lastGeneratedMessage)
        {
            Debug.Log("[GroupChat] 중복 응답 감지, 대화 종료.");
            break;
        }
        lastGeneratedMessage = generatedMessage;

        participated.Add(nextSpeaker.presetID);
        turn++;

        // 다음 턴 전 랜덤 지연
        await UniTask.Delay(Random.Range(800, 2000), cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    Debug.Log("[GroupChat] 연쇄 대화 흐름이 완료되었습니다.");
    if (MemorySystemController.Instance != null)
    {
        StartCoroutine(MemorySystemController.Instance.agent.ProcessCurrentContext(groupId, true));
    }
}

// FindNextResponder 수정된 시그니처 예시
private CharacterPreset FindNextResponder(List<CharacterPreset> members, HashSet<string> participated)
{
    foreach (var m in members.OrderBy(_ => Random.value))
    {
        // 이미 말한 멤버는 30% 확률, 신규 멤버는 70% 확률로 발언
        float chance = participated.Contains(m.presetID) ? 0.3f : 0.7f;
        if (Random.value < chance)
            return m;
    }
    return null;
}

    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker, bool isFinalTurn = false)
    {
        try
        {
            string reply;
            List<ChatDatabase.ChatMessage> conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            
            if (conversationHistory.Count == 0) return null;
            
            // 1️⃣ 타겟 메시지 선정 로직 --------------------------
            ChatDatabase.ChatMessage lastMessage = conversationHistory.First();            // 최신
            ChatDatabase.ChatMessage originalUserMsg = conversationHistory
                .LastOrDefault(m => m.SenderID == "user");                               // 대화 시작부 근처 사용자 질문

            ChatDatabase.ChatMessage targetMsg = lastMessage;
            
            if (!isFinalTurn) // 중간 턴에서만 변주
            {
                float r = Random.value;
                if (r < 0.4f && originalUserMsg != null && originalUserMsg != lastMessage)
                {
                    // 40% 확률 → 사용자 원 질문에 추가 답변
                    targetMsg = originalUserMsg;
                }
                else if (r < 0.6f && conversationHistory.Count > 2)
                {
                    // 20% 확률 → 직전이 아닌 예전 캐릭터 발언에 반응
                    int randomIndex = Random.Range(1, conversationHistory.Count);
                    targetMsg = conversationHistory[randomIndex];
                }
                // 나머지 40% → 직전 발언에 답변 (default)
            }
            
            // 2️⃣ 타겟 메시지 정보 추출 --------------------------
            string targetSpeakerName;
            if (targetMsg.SenderID == "user")
            {
                targetSpeakerName = "사용자";
            }
            else
            {
                var spPreset = CharacterPresetManager.Instance.GetPreset(targetMsg.SenderID);
                targetSpeakerName = spPreset?.characterName ?? "다른 멤버";
            }
            var targetData = JsonUtility.FromJson<MessageData>(targetMsg.Message);
            string targetMessageContent = targetData?.textContent ?? "(내용 없음)";

            // 3️⃣ 임무 프롬프트 -------------------------------
            string taskPrompt;
            if (isFinalTurn)
            {
                taskPrompt =
                    $"\n\n--- 현재 임무 ---\n" +
                    $"'{targetSpeakerName}'이(가) 방금 \"{targetMessageContent}\" 라고 말했다. 이 발언을 참고하여 대화의 흐름을 자연스럽게 마무리하는 발언을 해라.";
            }
            else
            {
                taskPrompt =
                    $"\n\n--- 현재 임무 ---\n" +
                    $"'{targetSpeakerName}'이(가) 방금 \"{targetMessageContent}\" 라고 말했다. 이 발언에 대해 너의 역할과 성격에 맞게 자연스럽게 응답하라.";
            }
            
            //LLM 호출
            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                var messages = new List<OllamaMessage>();

                // Ollama 방식은 시스템 프롬프트에 taskPrompt를 포함하는 것이 아니라,
                // 전체 대화 흐름을 보고 스스로 판단하게 하는 것이 더 나을 수 있습니다.
                // 하지만 Gemini와 통일성을 위해 여기서는 시스템 프롬프트에 taskPrompt를 붙이는 현재 로직을 유지합니다.
                string systemPrompt = PromptHelper.BuildBasePrompt(speaker) + taskPrompt;
                messages.Add(new OllamaMessage { role = "system", content = systemPrompt });

                foreach (var msg in conversationHistory)
                {
                    string role = (msg.SenderID == speaker.presetID) ? "assistant" : "user";
                    var messageData = JsonUtility.FromJson<MessageData>(msg.Message);
                    if (messageData != null)
                    {
                        messages.Add(new OllamaMessage { role = role, content = messageData.textContent });
                    }
                }
                
                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else // 기존 Gemini API 방식
            {
                string finalPrompt = PromptHelper.BuildFullChatContextPrompt(speaker, conversationHistory) + taskPrompt;
                reply = await ChatService.AskAsync(finalPrompt, null, null, cancellationToken);
            }
            
            string parsedReply = ParseResponse(reply, speaker.presetID);
            var replyData = new MessageData { type = "text", textContent = parsedReply };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speaker.presetID, JsonUtility.ToJson(replyData));

            return parsedReply;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GroupChat] 그룹 응답 생성 중 오류 ({speaker.characterName}): {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    #endregion

    #region 공용 헬퍼 메서드 (변경 없음)
    // ... 이하 코드는 모두 동일합니다 ...
    private string ParseResponse(string responseText, string presetIdForContext)
    {
        string parsedText = responseText;
        if (string.IsNullOrEmpty(parsedText) || parsedText.Contains("실패") || parsedText.Contains("차단"))
        {
            return parsedText;
        }
        
        var preset = CharacterPresetManager.Instance.GetPreset(presetIdForContext);
        if (preset == null) return parsedText;

        // 1. [FAREWELL] 태그 처리
        if (parsedText.Contains("[FAREWELL]"))
        {
            preset.hasSaidFarewell = true;
            preset.isWaitingForReply = false;
            preset.ignoreCount = 0;
            parsedText = parsedText.Replace("[FAREWELL]", "");
        }

        // 2. [INTIMACY_CHANGE=값] 태그 처리
        string changeTag = "[INTIMACY_CHANGE=";
        int tagIndex = parsedText.IndexOf(changeTag, StringComparison.OrdinalIgnoreCase);
        if (tagIndex != -1)
        {
            int endIndex = parsedText.IndexOf(']', tagIndex);
            if (endIndex != -1)
            {
                string valueStr = parsedText.Substring(tagIndex + changeTag.Length, endIndex - (tagIndex + changeTag.Length));
                if (float.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float delta))
                {
                    preset.ApplyIntimacyChange(delta);
                }
                // 태그 부분을 문자열에서 제거
                parsedText = parsedText.Remove(tagIndex, endIndex - tagIndex + 1);
            }
        }
        
        // 3. [ME] 식별자 제거 (추가된 로직)
        // AI가 실수로 자신의 발언에 "[ME]"를 붙여서 응답하는 경우를 방지합니다.
        parsedText = parsedText.Replace("[ME]", "");
        
        // 4. 앞/뒤 공백 최종 제거
        return parsedText.Trim();
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
    #endregion
}