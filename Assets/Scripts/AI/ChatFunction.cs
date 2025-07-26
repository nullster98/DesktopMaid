using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;
using AI;
using UnityEngine.Localization;
using System.Text.RegularExpressions;

/// <summary>
/// 개별 채팅창의 핵심 로직을 담당합니다.
/// 사용자 입력을 처리하고, AI의 기억과 설정을 바탕으로 프롬프트를 구성하며,
/// 그룹 채팅에서는 '조정자 AI'를 통해 AI간의 연쇄 대화를 조율합니다.
/// </summary>
public class ChatFunction : MonoBehaviour
{
    #region Variables and Initialization

    [Header("필수 연결")]
    public ChatUI chatUI;

    [Header("대화 설정")]
    [Tooltip("1:1 및 그룹 채팅에서 단기 기억으로 가져올 최근 메시지 개수")]
    private const int SHORT_TERM_MEMORY_COUNT = 20;
    [Tooltip("AI 그룹 대화가 무한 루프에 빠지는 것을 방지하기 위한 최대 대화 턴 수")]
    public int maxLoopTurns = 10;
    
    [Header("타이핑 효과 설정")]
    [Tooltip("초당 타이핑 속도 (글자 수)")]
    public float typingSpeed = 15f;
    [Tooltip("최소 타이핑 지연 시간 (초)")]
    public float minTypingDelay = 0.5f;
    [Tooltip("최대 타이핑 지연 시간 (초)")]
    public float maxTypingDelay = 4.0f;
    
    [Header("현지화(Localization)")]
    [SerializeField] private LocalizedString errorMessageKey;

    private AIConfig cfg;

    private void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");
        if (cfg == null)
        {
            Debug.LogError("[ChatFunction] AIConfig 파일을 Resources 폴더에서 찾을 수 없습니다! AI 기능이 작동하지 않습니다.");
        }
    }

    #endregion

    #region 1:1 Chat Logic

    /// <summary>
    /// 1:1 채팅 UI에서 사용자가 메시지 전송 시 호출되는 시작점입니다.
    /// </summary>
    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);
        
        // AI의 자율 행동 타이머를 리셋하기 위해 Observer에 알림
        FindObjectOfType<AIScreenObserver>()?.OnUserSentMessageTo(presetId);
        
        // 비동기 요청 시작
        SendRequestAsync(userInput, fileContent, fileType, fileName, fileSize).Forget();
    }

    /// <summary>
    /// 1:1 채팅의 프롬프트를 구성하고 AI 응답을 요청하는 비동기 코어 함수.
    /// </summary>
    private async UniTaskVoid SendRequestAsync(string inputText, string fileContent, string fileType, string fileName, long fileSize)
    {
        string presetId = chatUI.presetID;
        CharacterPreset myself = CharacterPresetManager.Instance.GetPreset(presetId);
        if (myself == null)
        {
             Debug.LogError($"[ChatFunction] SendRequestAsync 실패: 프리셋 '{presetId}'을(를) 찾을 수 없습니다.");
             return;
        }

        chatUI.ShowTypingIndicator(myself);
        var cancellationToken = this.GetCancellationTokenOnDestroy();

        try
        {
            // 1. 단기 기억 구성: 개인 기억과 소속 그룹의 기억을 모두 가져와 합친다.
            List<ChatDatabase.ChatMessage> personalMemory = ChatDatabaseManager.Instance.GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);
            var combinedMemory = new List<ChatDatabase.ChatMessage>(personalMemory);
            if (!string.IsNullOrEmpty(myself.groupID))
            {
                List<ChatDatabase.ChatMessage> groupMemory = ChatDatabaseManager.Instance.GetRecentGroupMessages(myself.groupID, SHORT_TERM_MEMORY_COUNT);
                combinedMemory.AddRange(groupMemory);
            }
            combinedMemory = combinedMemory.OrderBy(m => m.Timestamp).ToList();

            // 2. AI 모델에 따른 분기 처리
            string reply;
            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                // Ollama: 역할(role)이 분리된 메시지 리스트 구성
                var messages = new List<OllamaMessage>();
                messages.Add(new OllamaMessage { role = "system", content = PromptHelper.BuildBasePrompt(myself) });

                foreach (var msg in combinedMemory)
                {
                    string role = msg.SenderID == "user" ? "user" : "assistant";
                    var data = JsonUtility.FromJson<MessageData>(msg.Message);
                    if (data != null) messages.Add(new OllamaMessage { role = role, content = data.textContent });
                }

                var userMsg = new OllamaMessage { role = "user", content = inputText };
                if (fileType == "image" && !string.IsNullOrEmpty(fileContent))
                {
                    userMsg.images = new List<string> { fileContent };
                }
                messages.Add(userMsg);

                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else // Gemini 등
            {
                // Gemini: 모든 정보를 하나의 거대한 텍스트 프롬프트로 구성
                string context = PromptHelper.BuildFullChatContextPrompt(myself, combinedMemory);
                string finalPrompt = context + "\n\n--- 현재 임무 ---\n" +
                                     $"위 모든 정보를 참고하여 아래 사용자 발언에 자연스럽게 답하라.\n" +
                                     $"사용자: \"{inputText}\"";

                string imageBase64 = null;
                if (fileType == "text" && !string.IsNullOrEmpty(fileContent))
                    finalPrompt += $"\n\n--- 첨부 파일 '{fileName}' 내용 ---\n{fileContent}";
                else if (fileType == "image")
                    imageBase64 = fileContent;

                reply = await ChatService.AskAsync(finalPrompt, imageBase64, null, cancellationToken);
            }

            // 3. 응답 후처리
            string parsed = myself.ParseAndApplyResponse(reply);
            
            // 응답 길이에 비례한 자연스러운 타이핑 지연
            float typingDelay = Mathf.Clamp((float)parsed.Length / typingSpeed, minTypingDelay, maxTypingDelay);
            await UniTask.Delay(TimeSpan.FromSeconds(typingDelay), cancellationToken: cancellationToken);
            
            chatUI.HideTypingIndicator();
            var replyData = new MessageData { type = "text", textContent = parsed };
            ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));

            // 대화가 끝난 후, 이 대화를 '현재 상황'으로 요약하도록 요청
            MemorySystemController.Instance?.agent.ProcessCurrentContextAsync(presetId, isGroup: false).Forget();
            
            if (!parsed.Contains("차단") && !myself.hasSaidFarewell)
            {
                myself.StartWaitingForReply(); // AI가 사용자의 다음 답장을 기다리는 상태로 전환
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatFunction] 1:1 채팅 중 예외 발생: {ex.Message}\n{ex.StackTrace}");
            var handle = errorMessageKey.GetLocalizedStringAsync();
            await handle;
            string errorMessage = (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) 
                ? handle.Result 
                : "An error occurred...";
            chatUI.AddChatBubble(errorMessage, isUser: false, speaker: null); // 시스템 오류 메시지 표시
        }
        finally
        {
            chatUI.HideTypingIndicator(); // 성공/실패 여부와 관계없이 타이핑 UI 숨김
        }
    }

    #endregion

    #region Group Chat Logic

    /// <summary>
    /// 그룹 채팅 UI에서 사용자가 메시지 전송 시 호출되는 시작점입니다.
    /// </summary>
    public void OnUserSentMessage(string groupId, string userInput, string fileContent, string fileType, string fileName, long fileSize)
    {
        // 로컬 모델 파일 첨부 제한 (필요 시)
        if (cfg.modelMode == ModelMode.GemmaLocal && (fileContent != null || fileSize > 0))
        {
            var errorData = new MessageData { type = "system", textContent = "(현재 로컬 AI 모델에서는 파일 및 이미지 첨부를 지원하지 않습니다.)" };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, "system", JsonUtility.ToJson(errorData));
            if (string.IsNullOrWhiteSpace(userInput)) return;
        }
        // AI 연쇄 반응 시작
        GroupConversationFlowAsync(groupId, initialSpeakerId: "user").Forget();
    }

    /// <summary>
    /// 자율 행동 시스템(AIScreenObserver)이 그룹 대화를 시작시킬 때 호출되는 시작점입니다.
    /// </summary>
    public void OnSystemInitiatedConversation(string groupId, string firstMessage, string speakerId)
    {
        // 첫 메시지는 이미 DB에 저장되어 있으므로, 바로 AI 연쇄 반응을 시작
        GroupConversationFlowAsync(groupId, initialSpeakerId: speakerId).Forget();
    }

    /// <summary>
    /// 조정자 AI를 통해 그룹 멤버들의 연쇄적인 대화를 제어하는 비동기 코어 함수.
    /// </summary>
    private async UniTask GroupConversationFlowAsync(string groupId, string initialSpeakerId)
    {
        var group = CharacterGroupManager.Instance.GetGroup(groupId);
        var allMembers = CharacterGroupManager.Instance.GetGroupMembers(groupId)
            .Where(p => !p.isLocked && p.CurrentMode == CharacterMode.Activated).ToList();
        if (group == null || allMembers.Count == 0) return;

        var groupChatUI = FindObjectsOfType<ChatUI>(true).FirstOrDefault(ui => ui.OwnerID == groupId && ui.gameObject.activeInHierarchy);
        if (groupChatUI == null) return;
        
        var cancellationToken = this.GetCancellationTokenOnDestroy();

        await UniTask.Delay(Random.Range(800, 1800), cancellationToken: cancellationToken);

        int turn = 0;
        string lastSpeakerId = initialSpeakerId;

        // 조정자 AI 루프 시작
        while (turn < maxLoopTurns)
        {
            var conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, 15);
            var candidates = allMembers.Where(p => p.presetID != lastSpeakerId && !p.hasSaidFarewell).ToList();
            
            if(candidates.Count == 0) 
            {
                Debug.Log("[ChatFunction] 그룹 내 대화할 상대가 없어 AI 연쇄 반응을 종료합니다.");
                break;
            }
            
            // 1. 조정자 AI에게 "누가 말할 차례인가?" 질문
            string coordinatorPrompt = PromptHelper.GetAdvancedCoordinatorPrompt(group, candidates, conversationHistory);
            string rawDecision = "";
            try
            {
                 rawDecision = await ChatService.AskAsync(coordinatorPrompt, null, null, cancellationToken);
            }
            catch(Exception ex)
            {
                Debug.LogError($"[ChatFunction] 조정자 AI 호출 중 예외 발생: {ex}");
                // 에러 처리...
                break; 
            }
            
            // 2. 조정자 AI의 답변 파싱
            var decisionMatch = Regex.Match(rawDecision, @"결정:\s*(.+)");
            string decision = decisionMatch.Success ? decisionMatch.Groups[1].Value.Trim() : "";
            
            Debug.Log($"[ChatFunction] CoordinatorAI 결정: {decision}");

            if (string.IsNullOrEmpty(decision) || decision.ToUpper().Contains("NONE"))
            {
                Debug.Log("[ChatFunction] CoordinatorAI가 대화 종료를 결정하여 흐름을 마칩니다.");
                break;
            }

            // 3. 다음 발언자 선정
            CharacterPreset nextSpeaker = allMembers.FirstOrDefault(p => p.presetID == decision) 
                ?? candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p.characterName) && decision.Contains(p.characterName)); // ID가 아닌 이름으로 답했을 경우 대비

            if (nextSpeaker == null)
            {
                Debug.LogWarning($"[ChatFunction] CoordinatorAI가 유효하지 않은 ID({decision})를 반환하여 대화를 종료합니다.");
                break;
            }
            
            Debug.Log($"[ChatFunction] AI가 선택한 다음 발언자: {nextSpeaker.characterName}");
            groupChatUI.ShowTypingIndicator(nextSpeaker);

            // 4. 선택된 발언자의 응답 생성
            string generatedMessage;
            try
            {
                generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatFunction] 그룹 응답 생성 중 예외 발생 ({nextSpeaker.characterName}): {ex}");
                // 에러 처리...
                groupChatUI.HideTypingIndicator();
                break; 
            }

            if (string.IsNullOrEmpty(generatedMessage))
            {
                groupChatUI.HideTypingIndicator();
                break;
            }
            
            // 5. 생성된 응답을 UI에 표시하고 DB에 저장
            float typingDelay = Mathf.Clamp((float)generatedMessage.Length / typingSpeed, minTypingDelay, maxTypingDelay);
            await UniTask.Delay(TimeSpan.FromSeconds(typingDelay), cancellationToken: cancellationToken);
            
            groupChatUI.HideTypingIndicator();
            var replyData = new MessageData { type = "text", textContent = generatedMessage };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, nextSpeaker.presetID, JsonUtility.ToJson(replyData));

            lastSpeakerId = nextSpeaker.presetID;
            turn++;
            await UniTask.Delay(Random.Range(800, 2000), cancellationToken: cancellationToken);
        } // End of while loop

        Debug.Log("[ChatFunction] 그룹 대화 조율 흐름이 완료되었습니다.");
        // 그룹 대화가 모두 끝난 후, 이 대화를 '현재 상황'으로 요약하도록 요청
        MemorySystemController.Instance?.agent.CheckAndProcessGroupMemoryAsync(group).Forget();
    }

    /// <summary>
    /// 그룹 채팅에서 특정 AI 멤버 한 명의 응답을 생성하는 헬퍼 함수.
    /// </summary>
    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker)
    {
        // 그룹 대화 기록과 개인 대화 기록을 모두 참고하여 프롬프트 생성
        List<ChatDatabase.ChatMessage> conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
        List<ChatDatabase.ChatMessage> personalHistory = ChatDatabaseManager.Instance.GetRecentMessages(speaker.presetID, SHORT_TERM_MEMORY_COUNT);
        var combinedHistory = conversationHistory.Concat(personalHistory).OrderBy(m => m.Timestamp).ToList();
        
        ChatDatabase.ChatMessage lastMessage = combinedHistory.LastOrDefault();
        if (lastMessage == null) return null;

        string targetSpeakerName = (lastMessage.SenderID == "user")
            ? "사용자"
            : CharacterPresetManager.Instance.GetPreset(lastMessage.SenderID)?.characterName ?? "다른 멤버";

        var targetData = JsonUtility.FromJson<MessageData>(lastMessage.Message);
        string targetMessageContent = targetData?.textContent ?? "(내용 없음)";
        
        string taskPrompt = $"\n\n--- 현재 임무 ---\n'{targetSpeakerName}'이(가) 방금 \"{targetMessageContent}\" 라고 말했다. 이 발언에 대해 너의 역할과 성격에 맞게 자연스럽게 응답하라.";
        
        string reply;
        var cancellationToken = this.GetCancellationTokenOnDestroy();

        if (cfg.modelMode == ModelMode.OllamaHttp)
        {
            var messages = new List<OllamaMessage>();
            string systemPrompt = PromptHelper.BuildBasePrompt(speaker) + taskPrompt;
            messages.Add(new OllamaMessage { role = "system", content = systemPrompt });

            foreach (var msg in combinedHistory)
            {
                string role = (msg.SenderID == speaker.presetID) ? "assistant" : "user";
                var data = JsonUtility.FromJson<MessageData>(msg.Message);
                if (data != null) messages.Add(new OllamaMessage { role = role, content = data.textContent });
            }
            reply = await ChatService.AskAsync(messages, cancellationToken);
        }
        else
        {
            string basePrompt = PromptHelper.BuildFullChatContextPrompt(speaker, combinedHistory);
            string finalPrompt = basePrompt + taskPrompt;
            reply = await ChatService.AskAsync(finalPrompt, null, null, cancellationToken);
        }
        
        return speaker.ParseAndApplyResponse(reply);
    }

    #endregion

    #region Utility
    
    // 이 static 클래스는 특정 채팅 세션의 소유자(presetId)를 임시로 저장하는 역할을 합니다.
    // 더 나은 방법은 Context나 Session 관리 객체를 사용하는 것이지만, 현재 구조를 유지합니다.
    public static class CharacterSession
    {
        public static string CurrentPresetId { get; private set; }
        public static void SetPreset(string presetId)
        {
            CurrentPresetId = presetId;
            ChatDatabaseManager.Instance.GetDatabase(presetId); // DB 연결 미리 활성화
        }
    }
    
    #endregion
}