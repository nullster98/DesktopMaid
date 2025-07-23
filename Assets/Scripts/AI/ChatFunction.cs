// --- START OF FILE ChatFunction.cs ---

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

    #region 1:1 채팅 로직

    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);
        FindObjectOfType<AIScreenObserver>()?.OnUserSentMessageTo(presetId);
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
            var cancellationToken = this.GetCancellationTokenOnDestroy();

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

            // --- 5. 응답 저장 & 후처리 ---
            // [수정] CharacterPreset의 중앙화된 파싱 함수를 호출
            string parsed = myself.ParseAndApplyResponse(reply);
            
            var replyData = new MessageData { type = "text", textContent = parsed };
            ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));

            // (선택) 바로 “현재 상황”도 갱신
            var memCtrl = MemorySystemController.Instance;
            if (memCtrl?.agent != null)
            {
                // 개인 채팅 플래그(false)
                memCtrl.agent.ProcessCurrentContextAsync(presetId, false).Forget();
            }
            else
            {
                Debug.LogWarning("[SendRequestAsync] MemorySystemController 또는 agent가 NULL입니다. 메모리 처리 건너뜁니다.");
            }

            // [수정] 작별인사를 했다면 더 이상 응답을 기다리지 않음
            if (!parsed.Contains("차단") && !myself.hasSaidFarewell)
                myself.StartWaitingForReply();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatFunction] 1:1 채팅 에러: {ex}");
            var err = new MessageData { type = "system", textContent = "오류가 발생했어요…" };
            ChatDatabaseManager.Instance.InsertMessage(presetId, "system", JsonUtility.ToJson(err));
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
            .Where(p =>
                !p.isLocked &&
                p.CurrentMode == CharacterMode.Activated)
            .ToList();
        if (group == null || allMembers.Count == 0) return;

    // 첫 응답 전 랜덤 지연
    await UniTask.Delay(Random.Range(800, 1800), cancellationToken: this.GetCancellationTokenOnDestroy());

    int turn = 0;
    string lastSpeakerId = initialSpeakerId;

    while (turn < maxLoopTurns)
    {
        var conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, 10);
        // [수정] 직전 발언자 + 작별한 멤버 제외
        var candidates = allMembers.Where(p => p.presetID != lastSpeakerId && !p.hasSaidFarewell).ToList();
        if(candidates.Count == 0) break;

        string coordinatorPrompt = PromptHelper.GetCoordinatorPrompt(group, candidates, conversationHistory);
        string decision = await ChatService.AskAsync(coordinatorPrompt, null, null, this.GetCancellationTokenOnDestroy());
        decision = decision.Replace("결정:", "").Trim();
        
        Debug.Log($"[CoordinatorAI] 결정: {decision}");

        if (string.IsNullOrEmpty(decision) || decision.ToUpper() == "NONE")
        {
            Debug.Log("[CoordinatorAI] 대화 종료를 결정했습니다.");
            break;
        }

        CharacterPreset nextSpeaker = allMembers.FirstOrDefault(p => p.presetID == decision);
        if (nextSpeaker == null)
        {
            Debug.LogWarning($"[CoordinatorAI] 존재하지 않는 ID({decision})를 반환했습니다. 대화를 종료합니다.");
            break;
        }
        
        Debug.Log($"[GroupChat] AI가 선택한 다음 화자: {nextSpeaker.characterName}");

        string generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker, false);
        if (string.IsNullOrEmpty(generatedMessage)) break;
        
        lastSpeakerId = nextSpeaker.presetID;
        turn++;

        await UniTask.Delay(Random.Range(800, 2000), cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    Debug.Log("[GroupChat] AI 조율 대화 흐름이 완료되었습니다.");
        var memCtrl = MemorySystemController.Instance;
        if (memCtrl != null && memCtrl.agent != null)
        {
            memCtrl.agent.CheckAndProcessGroupMemoryAsync(group).Forget();
        }
        else
        {
            Debug.LogWarning("[ChatFunction] MemorySystemController 또는 agent가 초기화되지 않았습니다.");
        }
    }

    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker, bool isFinalTurn = false)
    {
        try
        {
            List<ChatDatabase.ChatMessage> conversationHistory =
                ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
            List<ChatDatabase.ChatMessage> personalHistory =
                ChatDatabaseManager.Instance.GetRecentMessages(speaker.presetID, SHORT_TERM_MEMORY_COUNT);
            
            var combinedHistory = new List<ChatDatabase.ChatMessage>();
            combinedHistory.AddRange(conversationHistory);
            combinedHistory.AddRange(personalHistory);
            
            combinedHistory = combinedHistory.OrderBy(m => m.Timestamp).ToList();
            
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
            
            // [수정] CharacterPreset의 중앙화된 파싱 함수를 호출
            string parsed = speaker.ParseAndApplyResponse(reply);

            var replyData = new MessageData { type = "text", textContent = parsed };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speaker.presetID, JsonUtility.ToJson(replyData));

            return parsed;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GroupChat] 그룹 응답 생성 중 오류 ({speaker.characterName}): {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    #endregion

    #region 공용 헬퍼 메서드

    // [삭제] 이 클래스에 있던 ParseResponse 함수는 CharacterPreset.cs로 이전되어 삭제되었습니다.

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