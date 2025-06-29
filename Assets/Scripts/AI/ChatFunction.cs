// --- START OF FILE ChatFunction.cs ---

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

// 이 스크립트는 ChatUI와 함께 작동하며, 사용자의 입력을 받아 AI와 통신하는 핵심 역할을 합니다.
// 모든 AI 호출은 ChatService를 통해 비동기(async/await)로 처리됩니다.

public class ChatFunction : MonoBehaviour
{
    #region 변수 및 초기화

    [Header("필수 연결")]
    [Tooltip("이 ChatFunction과 연결된 ChatUI 컴포넌트")]
    public ChatUI chatUI;

    [Header("설정")]
    [Tooltip("AI에게 단기 기억으로 제공할 최근 대화의 수")]
    private const int SHORT_TERM_MEMORY_COUNT = 20;

    // AI 모델 설정 (Gemini API / 로컬 Gemma)을 담고 있는 설정 파일
    private AIConfig cfg;

    private void Awake()
    {
        // 스크립트가 활성화될 때 AI 설정을 안전하게 불러옵니다.
        cfg = Resources.Load<AIConfig>("AIConfig");
        if (cfg == null)
        {
            Debug.LogError("[ChatFunction] AIConfig 파일을 Resources 폴더에서 찾을 수 없습니다!");
        }
    }

    #endregion

    #region 1:1 채팅 로직

    /// <summary>
    /// 1:1 채팅 메시지 전송의 공식 진입점입니다. (UI에서 호출)
    /// </summary>
    public void SendMessageToGemini(string userInput, string fileContent = null, string fileType = null, string fileName = null, long fileSize = 0)
    {
        string presetId = chatUI.presetID;
        CharacterSession.SetPreset(presetId);

        // 자율 행동 타이머 리셋을 위해 Observer에 알림
        FindObjectOfType<AIScreenObserver>()?.OnUserSentMessageTo(presetId);

        // 비동기 요청 시작 (결과를 기다리지 않고 바로 UI 반응성을 유지)
        SendRequestAsync(userInput, fileContent, fileType, fileName, fileSize).Forget();
    }

    /// <summary>
    /// 1:1 채팅 AI에게 실제 요청을 보내고 응답을 처리하는 비동기 메서드.
    /// </summary>
    private async UniTaskVoid SendRequestAsync(string inputText, string fileContent, string fileType, string fileName, long fileSize)
    {
        string presetId = chatUI.presetID;
        var myself = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == presetId);
        if (myself == null)
        {
            Debug.LogError($"SendRequestAsync 실패: 프리셋 ID '{presetId}'를 찾을 수 없습니다.");
            return;
        }
        
        // 1. PromptHelper를 사용해 AI의 모든 기억과 설정을 포함한 기본 프롬프트를 생성합니다.
        List<ChatDatabase.ChatMessage> shortTermMemory = ChatDatabaseManager.Instance.GetRecentMessages(presetId, SHORT_TERM_MEMORY_COUNT);
        string contextPrompt = PromptHelper.BuildFullChatContextPrompt(myself, shortTermMemory);

        // 2. 최종 프롬프트에 사용자의 현재 발언과 임무를 덧붙입니다.
        string finalPrompt = contextPrompt +
            "\n\n--- 현재 임무 ---\n" +
            "지금까지의 모든 대화와 설정을 바탕으로, 아래의 사용자 발언에 대해 자연스럽게 대답해라.\n" +
            $"사용자 발언: \"{inputText}\"";

        string imageBase64 = null;

        
        if (cfg.modelMode == ModelMode.GeminiApi || cfg.modelMode == ModelMode.OllamaHttp)
        {
            if (fileType == "text" && !string.IsNullOrEmpty(fileContent))
            {
                finalPrompt += $"\n\n--- 첨부된 파일 '{fileName}'의 내용 ---\n{fileContent}";
            }
            else if (fileType == "image")
            {
                if (File.Exists(fileContent))
                    fileContent = Convert.ToBase64String(File.ReadAllBytes(fileContent));

                // 순수 base64 상태에서 로그
                Debug.Log($"[ImgDebug] len={fileContent.Length} head={fileContent.Substring(0,40)}...");

                imageBase64 = fileContent.Trim();  // ★ 접두사 없이!
            }
        }

        // 4. ChatService를 통해 AI 응답을 비동기적으로 요청합니다.
        try
        {
            string reply = await ChatService.AskAsync(finalPrompt, imageBase64);

            // 5. 응답을 파싱하고 DB에 저장 후, 캐릭터 상태를 업데이트합니다.
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

    /// <summary>
    /// 사용자가 그룹에 메시지를 보냈을 때의 공식 진입점입니다. (UI에서 호출)
    /// </summary>
    public void OnUserSentMessage(string groupId, string userInput, string fileContent, string fileType, string fileName, long fileSize)
    {
        // 로컬 모델 파일 첨부 제한
        if (cfg.modelMode == ModelMode.GemmaLocal && (fileContent != null || fileSize > 0))
        {
            var errorData = new MessageData { type = "system", textContent = "(현재 로컬 AI 모델에서는 파일 및 이미지 첨부를 지원하지 않습니다.)" };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, "system", JsonUtility.ToJson(errorData));
            if (string.IsNullOrWhiteSpace(userInput)) return;
        }

        // 사용자 발언을 시작으로 연쇄 대화 흐름을 시작합니다.
        GroupConversationFlowAsync(groupId, "user").Forget();
    }

    /// <summary>
    /// 시스템(자율 행동)이 그룹 대화를 시작했을 때의 공식 진입점입니다.
    /// </summary>
    public void OnSystemInitiatedConversation(string groupId, string firstMessage, string speakerId)
    {
        // 시스템이 생성한 첫 메시지를 DB에 저장합니다.
        var messageData = new MessageData { type = "text", textContent = firstMessage };
        ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speakerId, JsonUtility.ToJson(messageData));

        // 해당 메시지를 시작으로 연쇄 대화 흐름을 시작합니다.
        GroupConversationFlowAsync(groupId, speakerId).Forget();
    }

    /// <summary>
    /// 그룹 채팅의 연쇄 반응 흐름을 총괄하는 마스터 비동기 메서드.
    /// </summary>
    private async UniTask GroupConversationFlowAsync(string groupId, string initialSpeakerId)
    {
        var group = CharacterGroupManager.Instance.GetGroup(groupId);
        var allMembers = CharacterGroupManager.Instance.GetGroupMembers(groupId);
        if (group == null || allMembers.Count == 0) return;

        // 이번 연쇄 반응에 이미 참여한 멤버를 기록하여 중복 발언을 방지합니다.
        var participatedMembers = new HashSet<string> { initialSpeakerId };

        const int MAX_ADDITIONAL_TURNS = 3; // 최초 발언 이후 최대 3명까지 추가로 반응합니다.
        for (int i = 0; i < MAX_ADDITIONAL_TURNS; i++)
        {
            // 1. 아직 말하지 않은 멤버들 중에서 다음 발언자를 찾습니다.
            var potentialResponders = allMembers.Where(p => !participatedMembers.Contains(p.presetID)).ToList();
            if (potentialResponders.Count == 0) break; // 더 이상 반응할 사람이 없으면 종료

            CharacterPreset nextSpeaker = FindNextResponder(potentialResponders);
            if (nextSpeaker == null) break; // 확률적으로 반응할 사람이 없으면 종료

            // 2. 선택된 발언자가 응답을 생성합니다. (내부적으로 최신 DB를 참조)
            Debug.Log($"[GroupChat] 다음 발언자 결정: {nextSpeaker.characterName}");
            string generatedMessage = await GenerateSingleGroupResponseAsync(groupId, nextSpeaker);

            // 3. 응답이 성공적으로 생성되었다면, 참여 목록에 추가하고 다음 턴을 위해 잠시 대기합니다.
            if (!string.IsNullOrEmpty(generatedMessage))
            {
                participatedMembers.Add(nextSpeaker.presetID);
                await UniTask.Delay(Random.Range(1000, 2500)); // 다음 캐릭터가 생각하는 시간
            }
            else
            {
                break; // 응답 생성 실패 시 연쇄 반응 중단
            }
        }

        Debug.Log("[GroupChat] 연쇄 대화 흐름이 완료되었습니다.");
    }

    /// <summary>
    /// 그룹 멤버 중에서 다음으로 말할 사람 한 명을 확률적으로 결정합니다.
    /// </summary>
    private CharacterPreset FindNextResponder(List<CharacterPreset> potentialResponders)
    {
        // 후보 목록을 무작위로 섞어 순서를 공평하게 만듭니다.
        foreach (var member in potentialResponders.OrderBy(a => Random.value))
        {
            // 70%의 기본 확률로 대화에 참여합니다.
            if (Random.value < 0.7f)
            {
                return member;
            }
        }
        return null; // 아무도 반응하지 않을 수도 있습니다.
    }

    /// <summary>
    /// 그룹 채팅에서 한 명의 캐릭터가 자신의 응답을 생성하고 DB에 저장합니다.
    /// </summary>
    private async UniTask<string> GenerateSingleGroupResponseAsync(string groupId, CharacterPreset speaker)
    {
        // 1. AI가 응답하기 직전의 최신 대화 기록을 DB에서 가져옵니다.
        List<ChatDatabase.ChatMessage> conversationHistory = ChatDatabaseManager.Instance.GetRecentGroupMessages(groupId, SHORT_TERM_MEMORY_COUNT);

        // 2. AI가 최신 대화 내용을 포함한 전체 맥락을 보고 스스로 다음 말을 판단하도록 프롬프트를 구성합니다.
        string finalPrompt = PromptHelper.BuildFullChatContextPrompt(speaker, conversationHistory) +
            "\n\n--- 현재 임무 ---\n" +
            "너는 지금 다른 사람들과 그룹 채팅을 하고 있다. 지금까지의 대화 흐름을 보고, 너의 역할과 성격에 맞게 자연스럽게 대화를 이어나가라.";

        try
        {
            string reply = await ChatService.AskAsync(finalPrompt);
            string parsedReply = ParseResponse(reply, speaker.presetID);

            // 3. 생성된 응답을 그룹 채팅 DB에 저장합니다.
            var replyData = new MessageData { type = "text", textContent = parsedReply };
            ChatDatabaseManager.Instance.InsertGroupMessage(groupId, speaker.presetID, JsonUtility.ToJson(replyData));

            return parsedReply; // 성공 시 생성된 메시지 반환
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatFunction] 그룹 응답 생성 중 오류 ({speaker.characterName}): {ex.Message}\n{ex.StackTrace}");
            return null; // 실패 시 null 반환
        }
    }

    #endregion

    #region 공용 헬퍼 메서드

    /// <summary>
    /// AI의 응답에서 [INTIMACY_CHANGE] 같은 특수 태그를 파싱하고 처리합니다.
    /// </summary>
    private string ParseResponse(string responseText, string presetIdForContext)
    {
        string originalText = responseText;

        // 에러 메시지는 그대로 반환
        if (string.IsNullOrEmpty(originalText) || originalText.Contains("실패") || originalText.Contains("차단"))
        {
            return originalText;
        }

        var preset = CharacterPresetManager.Instance.presets.Find(p => p.presetID == presetIdForContext);
        if (preset == null) return originalText;

        // [FAREWELL] 태그 처리
        if (originalText.Contains("[FAREWELL]"))
        {
            preset.hasSaidFarewell = true;
            preset.isWaitingForReply = false;
            preset.ignoreCount = 0;
            originalText = originalText.Replace("[FAREWELL]", "").Trim();
        }

        // [INTIMACY_CHANGE=값] 태그 처리
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

    /// <summary>
    /// 현재 활성화된 채팅창의 캐릭터 ID를 관리하는 정적 클래스.
    /// </summary>
    public static class CharacterSession
    {
        public static string CurrentPresetId { get; private set; }
        public static void SetPreset(string presetId)
        {
            CurrentPresetId = presetId;
            // DB 연결을 미리 활성화
            ChatDatabaseManager.Instance.GetDatabase(presetId);
        }
    }

    #endregion
}
// --- END OF FILE ChatFunction.cs ---