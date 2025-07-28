// --- START OF FILE ChatFunction.cs ---

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

public class ChatFunction : MonoBehaviour
{
    #region 변수 및 초기화

    [Header("필수 연결")]
    public ChatUI chatUI;

    private const int SHORT_TERM_MEMORY_COUNT = 20;
    private AIConfig cfg;
    
    [Header("그룹 대화 설정")]
    [Tooltip("AI 대화가 끝나지 않을 경우를 대비한 최대 루프 횟수 (안전장치)")]
    public int maxLoopTurns = 10;
    
    [Header("타이핑 효과 설정")]
    [Tooltip("초당 타이핑 속도 (글자 수)")]
    public float typingSpeed = 15f;
    [Tooltip("최소 타이핑 지연 시간 (초)")]
    public float minTypingDelay = 0.5f;
    [Tooltip("최대 타이핑 지연 시간 (초)")]
    public float maxTypingDelay = 4.0f;
    
    [Header("Localization")]
    [SerializeField] private LocalizedString errorMessageKey;

    private void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");
        if (cfg == null)
        {
            Debug.LogError("[ChatFunction] AIConfig 파일을 Resources 폴더에서 찾을 수 없습니다!");
        }
    }

    #endregion

    #region 1:1 채팅 로직

    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);
        FindObjectOfType<AIScreenObserver>()?.OnUserSentMessageTo(presetId);
        CharacterPresetManager.Instance?.MovePresetToTop(presetId);
        SendRequestAsync(userInput, fileContent, fileType, fileName, fileSize).Forget();
    }

    private async UniTaskVoid SendRequestAsync(string inputText, string fileContent, string fileType, string fileName, long fileSize)
    {
        string presetId = chatUI.presetID;
        var myself = CharacterPresetManager.Instance.GetPreset(presetId);
        if (myself == null)
        {
             Debug.LogError($"SendRequestAsync 실패: 프리셋 '{presetId}'을(를) 찾을 수 없습니다.");
             return;
        }

        chatUI.ShowTypingIndicator(myself);
        var cancellationToken = this.GetCancellationTokenOnDestroy();

        try
        {
            // --- 1. 개인 단기 기억 불러오기 ---
            var personalMemory = ChatDatabaseManager
                .Instance
                .GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);

            // --- 2. 그룹 단기 기억도 불러와 합치기 (있을 때만) ---
            var combinedMemory = new List<ChatDatabase.ChatMessage>(personalMemory);
            if (!string.IsNullOrEmpty(myself.groupID))
            {
                var groupMemory = ChatDatabaseManager
                    .Instance
                    .GetRecentGroupMessages(myself.groupID, SHORT_TERM_MEMORY_COUNT);
                combinedMemory.AddRange(groupMemory);
            }
            
            combinedMemory = combinedMemory
                .OrderBy(m => m.Timestamp)
                .ToList();

            // --- 3. LLM 호출 전 로그 (디버깅용) ---
            Debug.Log($"[1:1Chat] personalMemory={personalMemory.Count}, groupMemory={(combinedMemory.Count - personalMemory.Count)}");

            // --- 4. 모델 분기 & 메시지 구성 ---
            string reply;
            

            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                var messages = new List<OllamaMessage>();
                // 시스템 프롬프트에 통합 컨텍스트
                string sys = PromptHelper.BuildBasePrompt(myself);
                messages.Add(new OllamaMessage { role = "system", content = sys });

                // 단기 기억(개인+그룹)
                foreach (var msg in combinedMemory)
                {
                    string role = msg.SenderID == "user" ? "user" : "assistant";
                    var data = JsonUtility.FromJson<MessageData>(msg.Message);
                    // [안정성 강화] data가 null이 아닐 때만 메시지를 추가합니다.
                    if (data != null)
                    {
                        messages.Add(new OllamaMessage { role = role, content = data.textContent });
                    }
                }

                // 사용자 메시지
                var userMsg = new OllamaMessage { role = "user", content = inputText };
                if (fileType == "image" && !string.IsNullOrEmpty(fileContent))
                    userMsg.images = new List<string> { fileContent };
                messages.Add(userMsg);

                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else
            {
                // Full context 프롬프트에 combinedMemory 전달
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

            // --- 5. 응답 지연 및 후처리 ---
            string parsed = myself.ParseAndApplyResponse(reply);
            
            // 응답 길이에 따른 타이핑 지연 계산
            float typingDelay = Mathf.Clamp((float)parsed.Length / typingSpeed, minTypingDelay, maxTypingDelay);
            await UniTask.Delay(TimeSpan.FromSeconds(typingDelay), cancellationToken: cancellationToken);
            
            // 딜레이가 끝난 후, 타이핑 UI를 숨기고 DB에 저장
            chatUI.HideTypingIndicator();
            var replyData = new MessageData { type = "text", textContent = parsed };
            ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));

            // (선택) 바로 “현재 상황”도 갱신
            var memCtrl = MemorySystemController.Instance;
            if (memCtrl?.agent != null)
            {
                // 개인 채팅 플래그(false)
                memCtrl.agent.ProcessCurrentContextAsync(presetId, false).Forget();
            }
            
            if (!parsed.Contains("차단") && !myself.hasSaidFarewell)
                myself.StartWaitingForReply();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatFunction] 1:1 채팅 에러: {ex}");
            var handle = errorMessageKey.GetLocalizedStringAsync();
            await handle;

            string errorMessage = "An error occurred..."; // 기본 fallback 메시지

            // AsyncOperationHandle의 상태는 Status 프로퍼티와 AsyncOperationStatus enum으로 확인합니다.
            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                errorMessage = handle.Result;
            }
            else
            {
                Debug.LogError($"[ChatFunction] 현지화된 오류 메시지('{errorMessageKey.TableReference}/{errorMessageKey.TableEntryReference}')를 불러오는 데 실패했습니다.");
            }

            // DB에 저장하는 대신, ChatUI에 직접 시스템 메시지를 표시하도록 요청합니다.
            // (isUser: false, playSound: false, speaker: null)
            chatUI.AddChatBubble(errorMessage, false, false, null);
        }
        finally
        {
            // 에러 발생 시에도 타이핑 UI는 반드시 숨깁니다.
            chatUI.HideTypingIndicator();
        }
    }

    #endregion

    #region 그룹 채팅 로직

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
        // 시스템이 시작한 대화는 이미 DB에 저장되어 있으므로 여기서는 저장하지 않고, 바로 연쇄 반응을 시작합니다.
        GroupConversationFlowAsync(groupId, speakerId).Forget();
    }

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

        while (turn < maxLoopTurns)
        {
            var conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, 15);
            var candidates = allMembers.Where(p => p.presetID != lastSpeakerId && !p.hasSaidFarewell).ToList();
            
            if(candidates.Count == 0) 
            {
                Debug.Log("[GroupChat] 더 이상 대화할 상대가 없어 AI 연쇄 반응을 종료합니다.");
                break;
            }
            
            string coordinatorPrompt = PromptHelper.GetAdvancedCoordinatorPrompt(group, candidates, conversationHistory);
            string rawDecision = "";
            try
            {
                 rawDecision = await ChatService.AskAsync(coordinatorPrompt, null, null, cancellationToken);
            }
            catch(Exception ex)
            {
                Debug.LogError($"[GroupChat] 조율사 AI 호출 중 에러: {ex}");
                var handle = errorMessageKey.GetLocalizedStringAsync();
                await handle;
                string errorMessage = "An error occurred...";
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    errorMessage = handle.Result;
                }
                groupChatUI.AddChatBubble(errorMessage, false, false, null);
                break; 
            }
            
            string decision = "";
            string reason = "";
            
            var decisionMatch = Regex.Match(rawDecision, @"결정:\s*(.+)");
            if (decisionMatch.Success)
            {
                decision = decisionMatch.Groups[1].Value.Trim();
            }

            var reasonMatch = Regex.Match(rawDecision, @"이유:\s*(.+)");
            if (reasonMatch.Success)
            {
                reason = reasonMatch.Groups[1].Value.Trim();
            }

            Debug.Log($"[CoordinatorAI] 결정: {decision} | 이유: {reason}");

            if (string.IsNullOrEmpty(decision) || decision.ToUpper().Contains("NONE"))
            {
                Debug.Log("[CoordinatorAI] 대화 종료를 결정하여 흐름을 마칩니다.");
                break;
            }

            CharacterPreset nextSpeaker = allMembers.FirstOrDefault(p => p.presetID == decision);
            
            if (nextSpeaker == null)
            {
                nextSpeaker = candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p.characterName) && decision.Contains(p.characterName));
                
                if (nextSpeaker != null)
                {
                    Debug.LogWarning($"[GroupChat Fallback] 조율사 AI가 ID 대신 이름으로 응답했을 가능성이 있어, '{nextSpeaker.characterName}'을 다음 발언자로 선택합니다.");
                }
            }
            
            if (nextSpeaker == null)
            {
                Debug.LogWarning($"[CoordinatorAI] 존재하지 않는 ID({decision})를 반환했습니다. 대화를 종료합니다.");
                break;
            }
            
            Debug.Log($"[GroupChat] AI가 선택한 다음 화자: {nextSpeaker.characterName}");

            groupChatUI.ShowTypingIndicator(nextSpeaker);
            string generatedMessage;
            try
            {
                generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GroupChat] 그룹 응답 생성 중 에러 ({nextSpeaker.characterName}): {ex}");
                var handle = errorMessageKey.GetLocalizedStringAsync();
                await handle;
                string errorMessage = "An error occurred...";
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    errorMessage = handle.Result;
                }
                groupChatUI.HideTypingIndicator();
                groupChatUI.AddChatBubble(errorMessage, false, false, null);
                break; 
            }

            if (string.IsNullOrEmpty(generatedMessage))
            {
                groupChatUI.HideTypingIndicator();
                break;
            }
            
            float typingDelay = Mathf.Clamp((float)generatedMessage.Length / typingSpeed, minTypingDelay, maxTypingDelay);
            await UniTask.Delay(TimeSpan.FromSeconds(typingDelay), cancellationToken: cancellationToken);
            
            groupChatUI.HideTypingIndicator();
            var replyData = new MessageData { type = "text", textContent = generatedMessage };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, nextSpeaker.presetID, JsonUtility.ToJson(replyData));

            lastSpeakerId = nextSpeaker.presetID;
            turn++;

            await UniTask.Delay(Random.Range(800, 2000), cancellationToken: cancellationToken);
        }

        Debug.Log("[GroupChat] AI 조율 대화 흐름이 완료되었습니다.");
        var memCtrl = MemorySystemController.Instance;
        if (memCtrl != null && memCtrl.agent != null)
        {
            memCtrl.agent.CheckAndProcessGroupMemoryAsync(group).Forget();
        }
    }

    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker)
    {
        List<ChatDatabase.ChatMessage> conversationHistory =
            ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
        List<ChatDatabase.ChatMessage> personalHistory =
            ChatDatabaseManager.Instance.GetRecentMessages(speaker.presetID, SHORT_TERM_MEMORY_COUNT);
        
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
                if (data != null)
                {
                    messages.Add(new OllamaMessage { role = role, content = data.textContent });
                }
            }
            reply = await ChatService.AskAsync(messages, cancellationToken);
        }
        else
        {
            string basePrompt = PromptHelper.BuildFullChatContextPrompt(speaker, combinedHistory);
            string finalPrompt = basePrompt + taskPrompt;
            reply = await ChatService.AskAsync(finalPrompt, null, null, cancellationToken);
        }
        
        string parsed = speaker.ParseAndApplyResponse(reply);
        return parsed;
    }

    #endregion

    #region 공용 헬퍼 메서드

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
// --- END OF FILE ChatFunction.cs ---