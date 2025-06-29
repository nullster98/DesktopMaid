// --- START OF FILE ChatFunction.cs (최종 수정본) ---

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
            Debug.LogError($"SendRequestAsync 실패: 프리셋 ID '{presetId}'를 찾을 수 없습니다.");
            return;
        }

        try
        {
            string reply;
            List<ChatDatabase.ChatMessage> shortTermMemory = ChatDatabaseManager.Instance.GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                var messages = new List<OllamaMessage>();

                // 1. 시스템 프롬프트: PromptHelper.BuildBasePrompt 사용 (대화 기록 없는 순수 캐릭터 설정)
                string systemPrompt = PromptHelper.BuildBasePrompt(myself);
                messages.Add(new OllamaMessage { role = "system", content = systemPrompt });

                // 2. 대화 기록: DB에서 가져온 기록을 user/assistant 역할로 변환
                foreach (var msg in shortTermMemory)
                {
                    string role = (msg.SenderID == "user") ? "user" : "assistant";
                    var messageData = JsonUtility.FromJson<MessageData>(msg.Message);
                    if (messageData != null)
                    {
                        messages.Add(new OllamaMessage { role = role, content = messageData.textContent });
                    }
                }

                // 3. 현재 사용자 입력 추가
                var userMessage = new OllamaMessage { role = "user", content = inputText };
                if (fileType == "image" && !string.IsNullOrEmpty(fileContent))
                {
                    if (File.Exists(fileContent))
                        fileContent = Convert.ToBase64String(File.ReadAllBytes(fileContent));
                    userMessage.images = new List<string> { fileContent.Trim() };
                }
                messages.Add(userMessage);

                // 구조화된 메시지 리스트를 받는 AskAsync 오버로드 호출
                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else // Gemini API 등 기존 방식 (프롬프트 폭발이 일어나는 방식)
            {
                string contextPrompt = PromptHelper.BuildFullChatContextPrompt(myself, shortTermMemory);
                string finalPrompt = contextPrompt +
                    "\n\n--- 현재 임무 ---\n" +
                    "지금까지의 모든 대화와 설정을 바탕으로, 아래의 사용자 발언에 대해 자연스럽게 대답해라.\n" +
                    $"사용자 발언: \"{inputText}\"";

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
            }

            string parsedReply = ParseResponse(reply, myself.presetID);
            var replyData = new MessageData { type = "text", textContent = parsedReply };
            ChatDatabaseManager.Instance.InsertMessage(presetId, presetId, JsonUtility.ToJson(replyData));

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
        var messageData = new MessageData { type = "text", textContent = firstMessage };
        ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speakerId, JsonUtility.ToJson(messageData));
        GroupConversationFlowAsync(groupId, speakerId).Forget();
    }

    private async UniTask GroupConversationFlowAsync(string groupId, string initialSpeakerId)
    {
        var group = CharacterGroupManager.Instance.GetGroup(groupId);
        var allMembers = CharacterGroupManager.Instance.GetGroupMembers(groupId);
        if (group == null || allMembers.Count == 0) return;

        var participatedMembers = new HashSet<string> { initialSpeakerId };
        const int MAX_ADDITIONAL_TURNS = 3;

        for (int i = 0; i < MAX_ADDITIONAL_TURNS; i++)
        {
            var potentialResponders = allMembers.Where(p => !participatedMembers.Contains(p.presetID)).ToList();
            if (potentialResponders.Count == 0) break;

            CharacterPreset nextSpeaker = FindNextResponder(potentialResponders);
            if (nextSpeaker == null) break;

            Debug.Log($"[GroupChat] 다음 발언자 결정: {nextSpeaker.characterName}");
            string generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker);

            if (!string.IsNullOrEmpty(generatedMessage))
            {
                participatedMembers.Add(nextSpeaker.presetID);
                await UniTask.Delay(Random.Range(1000, 2500), cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            else
            {
                break;
            }
        }
        Debug.Log("[GroupChat] 연쇄 대화 흐름이 완료되었습니다.");
    }

    private CharacterPreset FindNextResponder(List<CharacterPreset> potentialResponders)
    {
        foreach (var member in potentialResponders.OrderBy(a => Random.value))
        {
            if (Random.value < 0.7f) return member;
        }
        return null;
    }

    // [핵심 수정] 그룹 채팅도 프롬프트 폭발을 막도록 수정
    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker)
    {
        try
        {
            string reply;
            List<ChatDatabase.ChatMessage> conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            if (cfg.modelMode == ModelMode.OllamaHttp)
            {
                var messages = new List<OllamaMessage>();

                string systemPrompt = PromptHelper.BuildBasePrompt(speaker) +
                    "\n\n--- 현재 임무 ---\n" +
                    "너는 지금 다른 사람들과 그룹 채팅을 하고 있다. 지금까지의 대화 흐름을 보고, 너의 역할과 성격에 맞게 자연스럽게 대화를 이어나가라. 다른 사람의 발언은 [이름: 내용] 형태로 전달될 것이다.";
                messages.Add(new OllamaMessage { role = "system", content = systemPrompt });

                foreach (var msg in conversationHistory)
                {
                    string role = (msg.SenderID == speaker.presetID) ? "assistant" : "user";
                    var messageData = JsonUtility.FromJson<MessageData>(msg.Message);
                    if (messageData != null)
                    {
                        string senderName = (msg.SenderID == "user") ? "사용자" : (CharacterPresetManager.Instance.GetPreset(msg.SenderID)?.characterName ?? msg.SenderID);
                        string content = $"[{senderName}]: {messageData.textContent}";
                        messages.Add(new OllamaMessage { role = role, content = content });
                    }
                }
                
                reply = await ChatService.AskAsync(messages, cancellationToken);
            }
            else
            {
                string finalPrompt = PromptHelper.BuildFullChatContextPrompt(speaker, conversationHistory) +
                    "\n\n--- 현재 임무 ---\n" +
                    "너는 지금 다른 사람들과 그룹 채팅을 하고 있다. 지금까지의 대화 흐름을 보고, 너의 역할과 성격에 맞게 자연스럽게 대화를 이어나가라.";
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

    #region 공용 헬퍼 메서드

    private string ParseResponse(string responseText, string presetIdForContext)
    {
        string originalText = responseText;
        if (string.IsNullOrEmpty(originalText) || originalText.Contains("실패") || originalText.Contains("차단"))
        {
            return originalText;
        }
        var preset = CharacterPresetManager.Instance.presets.Find(p => p.presetID == presetIdForContext);
        if (preset == null) return originalText;
        if (originalText.Contains("[FAREWELL]"))
        {
            preset.hasSaidFarewell = true;
            preset.isWaitingForReply = false;
            preset.ignoreCount = 0;
            originalText = originalText.Replace("[FAREWELL]", "").Trim();
        }
        string changeTag = "[INTIMACY_CHANGE=";
        int tagIndex = originalText.IndexOf(changeTag, StringComparison.OrdinalIgnoreCase);
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

    #region 캐릭터 세션 관리

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