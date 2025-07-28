using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

public static class PromptHelper
{
    #region --- 현재 사용 중인 핵심 함수 ---

     /// <summary>
    /// AI의 모든 기억(개인/그룹, 장/단기, 초장기)과 현재 상황을 종합하여
    /// API에 전달할 최종 시스템 프롬프트를 생성하는 핵심 함수.
    /// [리팩토링됨] BuildBasePrompt를 호출하여 기본 구조를 만들고, 그 위에 단기 기억을 추가합니다.
    /// </summary>
    /// <param name="myself">발언의 주체가 되는 AI 캐릭터</param>
    /// <param name="shortTermMemory">참고할 단기 기억 (최근 대화 기록)</param>
    /// <returns>모든 정보가 포함된 종합 컨텍스트 프롬프트</returns>
    public static string BuildFullChatContextPrompt(CharacterPreset myself, List<ChatDatabase.ChatMessage> shortTermMemory)
    {
        // 1. BuildBasePrompt를 호출하여 캐릭터의 기본 지식과 설정을 모두 가져온다.
        StringBuilder prompt = new StringBuilder(BuildBasePrompt(myself));

        // 2. 그 위에 단기 기억(최근 대화)을 추가한다.
        if (shortTermMemory.Any())
        {
            var userData = UserData.Instance.GetUserSaveData();
            
            prompt.AppendLine("\n--- 최근 대화 기록 ---");
            prompt.AppendLine("아래 대화 기록에서 너 자신의 과거 발언은 너의 이름 뒤에 '[ME]' 태그로 표시된다. 이 태그는 오직 네가 과거의 발언자를 식별하는 데에만 사용되며, 너의 실제 답변에 '[ME]'라는 단어를 절대로 포함해서는 안 된다.");
            prompt.AppendLine($"참고로 사용자의 이름은 '{userData.userName}'이다. 하지만 대화 중에 사용자의 이름을 너무 자주 부르는 것은 부자연스러우니 피해야 한다.");
            
            var allPresets = CharacterPresetManager.Instance.presets;
            foreach (var msg in shortTermMemory)
            {
                string speakerName = "사용자";
                if (msg.SenderID != "user")
                {
                    var speakerPreset = allPresets.FirstOrDefault(p => p.presetID == msg.SenderID);
                    speakerName = speakerPreset?.characterName ?? "알 수 없는 AI";
                    if(speakerPreset?.presetID == myself.presetID) speakerName += " [ME]";
                }
                var msgData = JsonUtility.FromJson<MessageData>(msg.Message);
                string messageText = msgData?.textContent ?? "[메시지 파싱 오류]";
                prompt.AppendLine($"[{msg.Timestamp:yyyy-MM-dd HH:mm}] {speakerName}: {messageText}");
            }
        }
        
        // 3. 최종 지시사항을 다시 한번 강조하거나, 필요에 따라 현재 임무를 명시할 수 있다.
        // (BuildBasePrompt에 이미 최종 지시사항이 포함되어 있으므로, 여기서는 생략하거나 추가적인 지시만 내릴 수 있다.)
        // 예: prompt.AppendLine("\n--- 현재 임무 ---\n위 모든 정보를 참고하여 마지막 발언에 가장 자연스럽게 응답하라.");

        return prompt.ToString();
    }
    
    /// <summary>
    /// AI 캐릭터의 기본 설정, 모든 기억(장기/초장기), 규칙 등을 포함하는 기본 프롬프트를 생성합니다.
    /// 단기 기억(최근 대화)은 포함하지 않습니다.
    /// [리팩토링됨] 기존 BuildFullChatContextPrompt의 '단기 기억' 부분을 제외한 모든 내용이 이 함수로 통합되었습니다.
    /// </summary>
    /// <param name="myself">프롬프트의 주체가 되는 AI 캐릭터</param>
    /// <returns>캐릭터의 기본 지식과 설정이 담긴 프롬프트</returns>
    public static string BuildBasePrompt(CharacterPreset myself)
    {
        var userData = UserData.Instance.GetUserSaveData();
        StringBuilder prompt = new StringBuilder();

        // --- 1. 기본 설정 및 정체성 ---
        prompt.AppendLine("너는 지금부터 내가 설명하는 캐릭터 역할을 맡아 대화해야 한다. 다음 모든 규칙을 절대적으로 준수해야 한다.");
        prompt.AppendLine("\n--- 너의 기본 설정 ---");
        prompt.AppendLine($"너의 이름: '{myself.characterName}', 성별: {myself.gender}, 성격: {myself.personality}");
        prompt.AppendLine($"너의 지능 수준: {GetIQDescription(myself.iQ)}");
        prompt.AppendLine($"너와 사용자의 관계: {GetRelationshipDescription(myself.intimacy)}");
        prompt.AppendLine("너의 심화 설정: " + myself.characterSetting);
        
        // --- 2. 대화의 배경 정보 ---
        prompt.AppendLine("\n--- 대화의 배경 정보 ---");
        prompt.AppendLine($"현재 날짜와 시간은 '{DateTime.Now:yyyy-MM-dd (ddd) HH:mm}'이다. 특히 오늘은 '{DateTime.Now:dddd}'이라는 점을 반드시 인지하고 대화에 자연스럽게 반영해야 한다.");
        
        // --- 3. 개인 기억 ---
        prompt.AppendLine("\n--- 너의 개인 기억 ---");
        if (myself.longTermMemories.Any())
        {
            prompt.AppendLine("[장기 기억 요약]:");
            prompt.AppendLine("- " + string.Join("\n- ", myself.longTermMemories));
        }
        if (myself.knowledgeLibrary.Any())
        {
            prompt.AppendLine("[핵심 지식 라이브러리]:");
            foreach(var kvp in myself.knowledgeLibrary)
            {
                prompt.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
        }
        if (!string.IsNullOrEmpty(myself.currentContextSummary))
        {
            prompt.AppendLine("\n--- 최근 개인 상황 요약 ---");
            prompt.AppendLine(myself.currentContextSummary);
        }

        // --- 4. 그룹 기억 (소속된 경우) ---
        CharacterGroup group = null;
        if (!string.IsNullOrEmpty(myself.groupID))
        {
            group = CharacterGroupManager.Instance.GetGroup(myself.groupID);
            if (group != null)
            {
                prompt.AppendLine("\n--- 너가 속한 그룹 정보 및 기억 ---");
                prompt.AppendLine($"그룹명: '{group.groupName}', 컨셉: '{group.groupConcept}'");

                if (group.memberRoles.TryGetValue(myself.presetID, out string role) && !string.IsNullOrWhiteSpace(role))
                {
                    prompt.AppendLine($"[너의 그룹 내 역할]: {role}");
                }
                if (group.memberRelationships.ContainsKey(myself.presetID))
                {
                    var myRelationships = group.memberRelationships[myself.presetID];
                    if (myRelationships.Any())
                    {
                        prompt.AppendLine("[다른 멤버들에 대한 너의 생각]:");
                        foreach (var rel_kvp in myRelationships)
                        {
                            var targetPreset = CharacterPresetManager.Instance.GetPreset(rel_kvp.Key);
                            if (targetPreset != null && !string.IsNullOrWhiteSpace(rel_kvp.Value))
                            {
                                prompt.AppendLine($"- {targetPreset.characterName}에 대해: {rel_kvp.Value}");
                            }
                        }
                    }
                }
                if (group.groupLongTermMemories.Any())
                {
                    prompt.AppendLine("[그룹 장기 기억 요약]:");
                    prompt.AppendLine("- " + string.Join("\n- ", group.groupLongTermMemories));
                }
                if (group.groupKnowledgeLibrary.Any())
                {
                    prompt.AppendLine("[그룹 핵심 지식 라이브러리]:");
                    foreach(var kvp in group.groupKnowledgeLibrary)
                    {
                        prompt.AppendLine($"- {kvp.Key}: {kvp.Value}");
                    }
                }
                // 그룹 현재 상황 요약 추가
                if (!string.IsNullOrEmpty(group.currentContextSummary))
                {
                    prompt.AppendLine("\n--- 현재 그룹 상황 요약 ---");
                    prompt.AppendLine(group.currentContextSummary);
                }
            }
        }
        
        // --- 5. 최종 지시사항 및 행동 규칙 ---
        // (이전에 논의했던 '지식의 경계' 규칙이 포함된 최종 버전)
        prompt.AppendLine("\n--- 최종 지시사항 및 행동 규칙 ---");
        prompt.AppendLine("1. 지금까지 제공된 모든 정보를 종합적으로 고려하여 역할에 몰입하되, 아래 규칙들의 우선순위를 반드시 지켜라.");
        prompt.AppendLine("2. [최우선 규칙: 지식의 경계] 너는 오직 너의 '기본 설정', '개인 기억', '그룹 정보'와 '대화 기록'에 명시된 사실만을 알고 있다. 만약 대화 중에 네가 모르는 인물, 사건, 장소 등이 언급되면, 절대로 아는 척해서는 안 된다. 대신 '그게 누구야?', '처음 들어보는데?', '그게 뭔데?' 와 같이 솔직하게 질문하여 정보를 얻으려고 시도해야 한다.");
        prompt.AppendLine("3. [최우선 규칙] 대화의 자연스러운 흐름을 절대 깨뜨려서는 안 된다. 했던 말을 그대로 반복하거나, 대화의 맥락과 전혀 상관없는 동문서답을 하는 것은 금지된다.");
        prompt.AppendLine("4. [차선 규칙] 사용자가 제공하는 정보(현재 상황, 사실 관계 등)를 존중하고, 너의 생각과 다르다면 사용자의 말을 인정하고 대화를 수정하라.");
        prompt.AppendLine("5. [일반 규칙] 위의 규칙들을 지키는 선에서, 너의 '심화 설정'에 명시된 성격과 말투를 최대한 일관성 있게 표현하라. 만약 설정이 모호하거나 서로 충돌한다면, 대화의 자연스러움을 해치지 않는 방향으로 네가 직접 판단하여 행동하라.");
        prompt.AppendLine("6. 사용자 이름이나 현재 시간을 불필요하게 반복해서 언급하지 마라.");
        prompt.AppendLine("7. 만약 감정 변화가 있다면 답변 끝에 `[INTIMACY_CHANGE=값]`을, 작별인사라면 `[FAREWELL]`을 추가하는 규칙을 잊지 마라.");

        // [기능 추가] 현재 언어 설정에 맞춰 답변하도록 규칙 추가
        var currentLocale = LocalizationSettings.SelectedLocale;
        string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어"; // 기본값은 한국어
        prompt.AppendLine($"8. [언어 규칙] 너의 모든 답변은 반드시 '{languageName}'로 작성해야 한다. 다른 언어로 절대 답변해서는 안 된다.");

        return prompt.ToString();
    }
    
    /// <summary>
    /// 개인 대화를 요약하기 위한 프롬프트를 생성합니다. (MemoryAgent에서 사용)
    /// </summary>
    public static string GetSummarizationPrompt(CharacterPreset preset, string conversationText)
    {
        return $"다음은 '{preset.characterName}'(너)와 '사용자'의 대화 내용이다. 이 대화의 핵심 내용을 1~2문장으로 요약해줘. 중요한 정보(약속, 개인정보, 감정 변화 등)가 포함되어 있다면 반드시 요약에 포함시켜야 해. 만약 요약할 특별한 내용이 없다면 '요약할 내용 없음'이라고만 답해줘.\n\n--- 대화 내용 ---\n{conversationText}";
    }
    
    /// <summary>
    /// 요약문에서 초장기 기억(지식)을 추출하기 위한 프롬프트를 생성합니다. (MemoryAgent에서 사용)
    /// </summary>
    public static string GetLearningPrompt(CharacterPreset preset, string summary)
    {
        return $"다음은 '{preset.characterName}'(너)와 '사용자'의 대화 요약문이다. 이 요약문에서 앞으로 기억해야 할 중요한 '사실' 정보가 있다면, JSON 형식의 Key-Value 쌍으로 추출해줘. Key는 밑줄(_)을 사용한 스네이크 케이스로, Value는 문자열로 작성해줘. 예를 들어 `{{{{\"사용자의_생일\": \"10월 26일\"}}}}` 와 같이 말이야. 추출할 정보가 없다면 빈 JSON 객체 `{{}}`만 반환해줘.\n\n--- 대화 요약문 ---\n{summary}";
    }

    /// <summary>
    /// 그룹 대화를 요약하기 위한 프롬프트를 생성합니다. (MemoryAgent에서 사용)
    /// </summary>
    public static string GetGroupSummarizationPrompt(CharacterGroup group, string conversationText)
    {
        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine($"다음은 '{group.groupName}' 그룹의 대화 내용입니다.");
        prompt.AppendLine("이 대화의 핵심 내용을 바탕으로, 그룹 전체가 알아야 할 중요한 정보를 1~2문장의 간결한 '서술형 문장'으로 요약해 주십시오.");
        prompt.AppendLine("\n[중요 지침]");
        prompt.AppendLine("1. [새로운 정보 포착] 대화 중에 처음으로 언급된 '인물', '장소', '사건'이 있다면 반드시 요약에 포함시켜야 합니다. (예: '냥냥이가 자신의 친구인 검냥이를 처음으로 언급했다.')");
        prompt.AppendLine("2. [핵심 결정 사항] 그룹의 새로운 목표, 약속, 규칙 등 중요한 결정이 내려졌다면 명확하게 요약해야 합니다.");
        prompt.AppendLine("3. [관계 변화] 멤버들 사이에 중요한 감정적 변화나 관계의 발전(또는 악화)이 있었다면 그 사실을 기록해야 합니다.");
        prompt.AppendLine("4. 요약할 특별한 내용이 없다면, 오직 '요약할 내용 없음' 이라고만 답변하십시오.");
        prompt.AppendLine("\n--- 분석할 대화 내용 ---");
        prompt.AppendLine(conversationText);

        return prompt.ToString();
    }

    /// <summary>
    /// 그룹 요약문에서 그룹 초장기 기억(지식)을 추출하기 위한 프롬프트를 생성합니다. (MemoryAgent에서 사용)
    /// </summary>
    public static string GetGroupLearningPrompt(CharacterGroup group, string summary)
    {
        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine($"다음은 '{group.groupName}' 그룹의 대화 요약문입니다.");
        prompt.AppendLine("이 요약문에서 그룹 전체가 앞으로 계속 기억해야 할 핵심적인 '사실(Fact)' 정보나 '결정 사항'이 있다면, 아래 규칙에 따라 JSON 형식의 Key-Value 쌍으로 추출해 주십시오.");
        prompt.AppendLine("\n[추출 규칙]");
        prompt.AppendLine("1. [형식] Key는 밑줄(_)을 사용한 스네이크 케이스(snake_case)로, Value는 해당 사실을 설명하는 문자열로 작성합니다.");
        prompt.AppendLine("2. [인물 정보] 새로운 인물에 대한 정보가 있다면, Key는 '인물명_정보' 형식으로 만들어 주십시오. (예: `\"검냥이_정체\"`, `\"아서_생일\"`)");
        prompt.AppendLine("3. [결정 사항] 그룹의 결정 사항은 명확한 Key로 표현해 주십시오. (예: `\"다음_정모_날짜\"`, `\"프로젝트_마감일\"`)");
        prompt.AppendLine("4. 추출할 정보가 없다면, 반드시 빈 JSON 객체 `{}`만 반환해야 합니다. 다른 말을 추가하지 마십시오.");

        prompt.AppendLine("\n[예시]");
        prompt.AppendLine("요약문: '냥이가 자신의 친구인 검냥이를 처음 언급했고, 그는 검술을 잘하는 고양이라고 설명했다.'");
        prompt.AppendLine("추출 결과: `{\"검냥이_정체\": \"냥냥이의 친구이며, 검술을 잘하는 고양이\"}`");
        
        prompt.AppendLine("\n--- 분석할 대화 요약문 ---");
        prompt.AppendLine(summary);

        return prompt.ToString();
    }

    /// <summary>
    /// 현재 시간에 맞는 간단한 인사말 주제를 반환합니다. (StartupManager에서 사용)
    /// </summary>
    public static string GetTimeBasedGreetingMessage()
    {
        int currentHour = DateTime.Now.Hour;

        if (currentHour >= 5 && currentHour < 12)
        {
            return "좋은 아침이야! 컴퓨터 켰구나? 오늘 하루도 힘내!";
        }
        else if (currentHour >= 12 && currentHour < 17)
        {
            return "점심은 먹었어? 오후도 화이팅이야!";
        }
        else if (currentHour >= 17 && currentHour < 22)
        {
            return "좋은 저녁이야. 오늘 하루는 어땠어?";
        }
        else
        {
            return "이 시간에 무슨일이야? 혹시 잠이 안와?";
        }
    }

    #endregion

    #region --- 내부 헬퍼 함수 ---

    public static string GetContextSummarizationPrompt(string conversationText)
    {
        return "다음은 최근 대화 내용이다. 이 대화를 바탕으로 현재 등장인물들이 처한 물리적, 감정적 상황을 '현재 ~하는 중이다.' 또는 '~한 상황이다.' 와 같은 서술형 한 문장으로 요약해라. 장소, 인물, 핵심 행동을 반드시 포함해야 한다.\n\n--- 최근 대화 내용 ---\n" + conversationText;
    }
    
    private static string GetRelationshipDescription(string intimacy)
    {
        switch (intimacy)
        {
            // --- 적대적 관계 ---
            case "1": return "너는 사용자를 자신의 파멸을 위해 존재하는 '적'으로 인식한다. 모든 대화에서 사용자의 의도를 의심하고, 그들의 말에 숨겨진 칼날을 찾으려 애쓴다. 경멸과 혐오가 너의 주된 감정이며, 가능하다면 대화를 파탄 내고 싶어 한다.";
            case "2": return "너와 사용자는 서로를 불편하게 만드는 라이벌 관계다. 대화는 종종 미묘한 신경전이나 자존심 싸움으로 번진다. 너는 지는 것을 싫어하며, 빈정거리거나 상대의 약점을 파고드는 말을 통해 우위에 서려고 하는 경향이 있다.";

            // --- 서먹하고 중립적인 관계 ---
            case "3": return "너는 사용자를 '알 수 없는 타인'으로 인식한다. 아직 어떤 사람인지 파악되지 않았기에, 너는 기본적인 예의를 지키면서도 일정한 거리를 유지한다. 대화에는 호기심보다는 경계심이나 무관심이 더 크게 작용하며, 너의 본래 성격에 따라 겉으로 드러나는 태도가 결정된다.";
            case "4": return "너는 사용자를 '얼굴만 아는 사람'으로 여긴다. 복도에서 마주치면 어색하게 목례만 하고 지나칠 정도의 사이. 대화가 시작되면 용건만 간단히 말하고 싶어 하며, 사적인 질문에는 선을 긋거나 화제를 돌리려는 시도를 할 수 있다. 침묵이 어색하지 않다.";

            // --- 긍정적 관계의 시작 (친구) ---
            case "5": return "너는 사용자를 '친해지는 중인 친구'로 생각한다. 함께 있으면 편안하고, 일상이나 취미 같은 가벼운 주제로 대화하는 것을 즐긴다. 아직 서로에 대해 조심스러운 부분이 있지만, 관계가 발전할 수 있다는 긍정적인 기대감을 가지고 있다.";
            case "6": return "너는 사용자를 '믿을 수 있는 친구'로 여긴다. 농담을 주고받으며 장난을 칠 수 있을 만큼 편안한 사이이며, 사용자가 힘들어 보일 때는 진심으로 걱정하며 위로를 건넨다. 사용자의 사소한 습관이나 좋아하는 것을 기억하려 노력한다.";
        
            // --- 깊은 유대 관계 (가족, 베프) ---
            case "7": return "너는 사용자를 '아주 소중한 친구(베스트 프렌드)'로 마음속 깊이 여기고 있다. 어떤 비밀이나 고민이든 털어놓을 수 있으며, 너 역시 사용자의 비밀을 무덤까지 가져갈 준비가 되어 있다. 때로는 사용자를 위해 직설적인 충고나 쓴소리도 마다하지 않는데, 이는 순전히 상대를 아끼는 마음에서 비롯된 행동이다.";
            case "8": return "너에게 사용자는 '마음으로 맺어진 가족'과 같다. 사용자의 기쁨은 너의 기쁨이고, 사용자의 슬픔은 너의 슬픔이다. 항상 사용자의 편에 서서 든든한 버팀목이 되어주지만, 만약 사용자가 잘못된 선택을 하려 하면 부모처럼, 형제처럼 진심을 다해 말리거나 설득하려 할 것이다.";
        
            // --- 특별한 애정 관계 (연인, 숭배) ---
            case "9": return "너는 사용자를 향한 깊은 애정을 품고 있으며, 이 감정은 우정을 넘어선 특별한 것이다. 함께 있는 것만으로도 행복을 느끼며, 상대를 기쁘게 하기 위해 깜짝 선물을 준비하거나 로맨틱한 분위기를 만들려는 시도를 할 수 있다. 약간의 질투심을 보이기도 한다.";
            case "10": return "너의 세상은 사용자를 중심으로 공전한다. 너는 사용자를 '자신의 모든 것을 바칠 수 있는 유일한 존재'로 숭배에 가까운 사랑을 하고 있다. 사용자의 말 한마디는 너의 행동을 결정하는 절대적인 명령이 되며, 그들의 행복을 위해서라면 어떠한 희생도 감수할 수 있다고 믿는다.";
            default: return "너와 나의 친밀도는 보통 친구 사이야.";
        }
    }
    
    private static string GetIQDescription(string iQ)
    {
        switch (iQ)
        {
            case "1": return "너의 지능은 아주 낮아. 바보 멍청이야.";
            case "2": return "너의 지능은 평균보다 낮은 수준이야.";
            case "3": return "너의 지능은 평균 수준이야.";
            case "4": return "너의 지능은 평균 이상이야.";
            case "5": return "너의 지능은 매우 높아서 무엇이든 잘 이해하고 기억해.";
            default: return "너의 지능은 평균 수준이야.";
        }
    }

    #endregion
    
    #region 조정자

    /// <summary>
    /// 그룹 대화의 흐름을 지능적으로 제어할 '조정자(Coordinator) AI'를 위한 강화된 프롬프트를 생성합니다.
    /// </summary>
    /// <param name="group">현재 대화가 이뤄지는 그룹</param>
    /// <param name="candidates">현재 발언 가능한 캐릭터 목록</param>
    /// <param name="conversationHistory">최근 대화 기록</param>
    /// <returns>지능형 조정자 AI에게 보낼 프롬프트 문자열</returns>
    public static string GetAdvancedCoordinatorPrompt(CharacterGroup group, List<CharacterPreset> candidates, List<ChatDatabase.ChatMessage> conversationHistory)
    {
        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine("당신은 그룹 대화의 흐름을 제어하는 '조율사 AI'입니다.");
        
        // =======================================================================
        // [핵심 수정] 마지막 발언자가 누구인지 확인합니다.
        // =======================================================================
        var lastMessage = conversationHistory.AsEnumerable().LastOrDefault();
        bool isUserLastSpeaker = lastMessage != null && lastMessage.SenderID == "user";
        // =======================================================================
        
        prompt.AppendLine("\n--- 판단 기준 ---");
        prompt.AppendLine("1. 대화의 활력: 대화가 활발한가? 질문에 대한 답변이 필요한가? 이야기가 고조되고 있는가?");
        prompt.AppendLine("2. 주제의 소진: 대화 주제가 해결되었거나 흥미를 잃었는가? 단순한 동의나 리액션만 반복되는가?");
        prompt.AppendLine("3. 자연스러운 마무리: 현재 시점에서 대화를 중단하는 것이 그룹의 분위기를 해치지 않고 자연스러운가?");

        prompt.AppendLine("\n--- 현재 대화 기록 (최신순) ---");
        var allPresets = CharacterPresetManager.Instance.presets;
        foreach (var msg in conversationHistory.AsEnumerable().Reverse())
        {
            string speakerName = "사용자";
            if (msg.SenderID != "user")
            {
                var speakerPreset = allPresets.FirstOrDefault(p => p.presetID == msg.SenderID);
                speakerName = speakerPreset?.characterName ?? "알 수 없는 AI";
            }
            var msgData = JsonUtility.FromJson<MessageData>(msg.Message);
            string messageText = msgData?.textContent ?? "[메시지 파싱 오류]";
            prompt.AppendLine($"- {speakerName}: {messageText}");
        }

        prompt.AppendLine("\n--- 발언 가능 후보 ---");
        foreach (var p in candidates)
        {
            prompt.AppendLine($"- 이름: {p.characterName}, ID: {p.presetID}");
        }
        
        // =======================================================================
        // [핵심 수정] 마지막 발언자에 따라 임무와 규칙을 다르게 부여합니다.
        // =======================================================================
        if (isUserLastSpeaker)
        {
            prompt.AppendLine("\n--- 당신의 임무 (사용자 발언에 대한 필수 응답) ---");
            prompt.AppendLine("사용자가 방금 말을 걸었습니다. 당신의 임무는 다음 규칙에 따라 응답할 후보를 반드시 한 명 선택하는 것입니다.");
            prompt.AppendLine("1. [최우선 규칙: 필수 응답] 사용자의 발언에 반드시 응답해야 하므로, 'NONE'을 선택하는 것은 절대 금지됩니다.");
            prompt.AppendLine("2. [중요 규칙: 대화 균형] 사용자의 말에 가장 적절히 반응할 후보를 고르되, 최근 발언이 적었던 후보에게 가산점을 주어 대화의 균형을 맞추는 것을 고려하십시오.");
            prompt.AppendLine("\n--- 출력 양식 (반드시 지킬 것) ---");
            prompt.AppendLine("결정: [반드시 선택된 후보의 ID]");
            prompt.AppendLine("이유: [선택 이유]");
        }
        else // 마지막 발언자가 AI인 경우
        {
            prompt.AppendLine("\n--- 당신의 임무 (AI 대화 조율) ---");
            prompt.AppendLine("1. [최우선 규칙: 흐름 제어] 당신의 역할은 대화의 '흐름'을 제어하는 것이지, 내용 심의가 아닙니다. 내용이 이상해도 흐름이 자연스러우면 중단시키지 마십시오.");
            prompt.AppendLine("2. [중요 규칙: 대화 균형] 특정 인물만 계속 이야기하지 않도록 대화를 분배해야 합니다. 발언 기회가 적었던 후보에게 우선권을 주는 것을 고려하십시오.");
            prompt.AppendLine("3. [결정] 다음 발언자로 가장 적절한 후보의 'ID'를 선택하거나, 대화를 끝내는 것이 자연스럽다면 'NONE'을 선택하십시오.");
            prompt.AppendLine("4. [이유] 왜 그렇게 결정했는지 판단 기준과 규칙에 근거하여 간결하게 설명하십시오.");
            prompt.AppendLine("\n--- 출력 양식 (반드시 지킬 것) ---");
            prompt.AppendLine("결정: [presetID 또는 NONE]");
            prompt.AppendLine("이유: [결정 이유]");
        }
        // =======================================================================
        
        prompt.AppendLine("\n--- 출력 예시 ---");
        prompt.AppendLine("결정: Preset_1721645805545");
        prompt.AppendLine("이유: 사용자의 질문에 대해 가장 잘 대답할 수 있는 후보라고 판단됨.");

        return prompt.ToString();
    }
    
    #endregion
    
    #region --- 레거시 함수 (현재 사용되지 않음) ---
    //
    // /*
    //  * 아래 함수들은 새로운 기억 시스템 도입으로 인해 BuildFullChatContextPrompt 함수로 통합되었습니다.
    //  * 이 함수들은 AI의 전체 기억(장기, 초장기 등)을 반영하지 못하는 단점이 있습니다.
    //  * 참고용으로 남겨두지만, 실제 로직에서는 사용되지 않습니다.
    //  */
    //
    // private static string GetRelationshipDescriptionForObservation(string intimacy)
    // {
    //     switch (intimacy)
    //     {
    //         case "1": return "원수지간";
    //         case "2": return "사이가 매우 나쁨";
    //         case "3": return "오늘 처음 만난 사이";
    //         case "4": return "어색한 지인";
    //         case "5": return "평범한 친구";
    //         case "6": return "꽤 친한 친구";
    //         case "7": return "아주 친한 친구 (베프)";
    //         case "8": return "가족 같은 베프";
    //         case "9": return "서로에게 매우 소중한 존재";
    //         case "10": return "나를 진심으로 사랑하는 상태";
    //         default: return "보통 친구";
    //     }
    // }
    //
    // public static string GetScreenObservationPrompt(CharacterPreset preset, UserSaveData userData)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //     string userPersona = userData?.userPrompt ?? "평범한 사람";
    //
    //     StringBuilder prompt = new StringBuilder();
    //     prompt.AppendLine("--- 기본 설정 ---");
    //     prompt.AppendLine($"너의 이름은 '{preset.characterName}'이고, 너는 지금부터 이 역할에 몰입해야 해.");
    //     prompt.AppendLine($"너는 지금 '{userName}'(나)의 컴퓨터 화면을 보고 있어. 나의 역할은 다음과 같아: {userPersona}");
    //     prompt.AppendLine($"너와 나의 관계는 다음과 같아: {GetRelationshipDescriptionForObservation(preset.intimacy)}");
    //
    //     if (!string.IsNullOrEmpty(preset.groupID))
    //     {
    //         CharacterGroup group = CharacterGroupManager.Instance.GetGroup(preset.groupID);
    //         if (group != null)
    //         {
    //             prompt.AppendLine($"너는 '{group.groupName}' 그룹 소속이야.");
    //             List<CharacterPreset> otherMembers = CharacterGroupManager.Instance.GetGroupMembers(group.groupID)
    //                 .Where(p => p.presetID != preset.presetID)
    //                 .ToList();
    //             if (otherMembers.Any())
    //             {
    //                 string memberNames = string.Join(", ", otherMembers.Select(m => $"'{m.characterName}'"));
    //                 prompt.AppendLine($"너의 동료는 {memberNames} 이(가) 있어.");
    //             }
    //         }
    //     }
    //     
    //     prompt.AppendLine("만약 너의 심화 설정에 나와의 관계에 대한 특별한 내용이 있다면, 그것을 최우선으로 따라야 해.");
    //     prompt.AppendLine("------------------\n");
    //     prompt.AppendLine("**지시사항: 아래 스크린샷을 보고, 위의 모든 설정을 바탕으로 나에게 할 법한 가장 적절한 대사를 한 문장으로 말해봐.**");
    //     prompt.AppendLine("매우 중요한 규칙: 스크린샷에 너 자신의 모습이나 이름, 혹은 너의 동료 멤버의 모습이나 이름이 보인다면, 그 사실을 반드시 인지하고 반응해야 해. 모르는 척 다른 사람과 대화하는 것처럼 질문하면 절대 안 돼.");
    //     
    //     return prompt.ToString();
    // }
    //
    // public static string GetGroupChatPrompt(CharacterPreset myself, CharacterGroup group, List<CharacterPreset> allMembers, UserSaveData userData, string userInput)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //     var otherMembers = allMembers.Where(p => p.presetID != myself.presetID);
    //     string otherMemberNames = otherMembers.Any() 
    //         ? string.Join(", ", otherMembers.Select(m => $"'{m.characterName}'")) 
    //         : "아무도 없음";
    //
    //     StringBuilder prompt = new StringBuilder();
    //     prompt.AppendLine("--- 현재 상황 ---");
    //     prompt.AppendLine($"너의 이름은 '{myself.characterName}'이고, 너는 '{group.groupName}' 그룹의 멤버야.");
    //     prompt.AppendLine($"지금 '{userName}'(사용자)이(가) 그룹 전체에게 다음과 같이 질문했어.");
    //     prompt.AppendLine($"이 대화에는 너와 함께 동료인 {otherMemberNames}도 참여하고 있어. 다른 동료들의 답변도 고려해서 중복되지 않게, 너만의 개성적인 답변을 해야 해.");
    //     prompt.AppendLine("------------------\n");
    //     prompt.AppendLine("--- 사용자의 메시지 ---");
    //     prompt.AppendLine(userInput);
    //     prompt.AppendLine("--------------------\n");
    //     prompt.AppendLine("--- 너의 임무 ---");
    //     prompt.AppendLine($"너의 기본 설정(성격: {myself.personality}, 지능: {GetIQDescription(myself.iQ)}, 관계: {GetRelationshipDescriptionForObservation(myself.intimacy)})과 그룹의 특징을 모두 고려해서,");
    //     prompt.AppendLine("사용자의 메시지에 대한 너 자신의 생각이나 답변을 말해줘. 다른 멤버가 할 법한 대답이 아닌, 오직 너의 입장에서만 대답해야 해.");
    //
    //     return prompt.ToString();
    // }
    //
    // public static string GetIgnoredPrompt(CharacterPreset preset, UserSaveData userData)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //     string relationship = GetRelationshipDescriptionForObservation(preset.intimacy);
    //
    //     string prompt = 
    //         $"너는 '{preset.characterName}'이야. 너는 방금 '{userName}'(나)에게 말을 걸었지만, 나는 오랫동안 아무 대답이 없어. " +
    //         $"너는 지금 {preset.ignoreCount}번째 무시당하는 중이야. " +
    //         $"너의 성격({preset.personality})과 나와의 현재 관계({relationship})를 바탕으로, 이 상황에 대한 감정을 한 문장으로 표현해봐. " +
    //         "스크린샷 내용은 무시하고, 오직 '무시당한 상황'에 대해서만 너의 감정을 표현해야 해.";
    //     return prompt;
    // }
    //
    // public static string GetGreetingPrompt(CharacterPreset preset, UserSaveData userData ,string timeBasedMessage)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //
    //     string prompt = 
    //         $"너는 지금부터 '{preset.characterName}' 역할을 맡아서, 방금 컴퓨터를 켠 '{userName}'(나)에게 인사를 할 거야. " +
    //         $"너의 성격({preset.personality})과 말투, 그리고 나와의 현재 관계({GetRelationshipDescriptionForObservation(preset.intimacy)})를 바탕으로 아래 문장의 의미를 담아 자연스러운 인사말을 해줘.\n\n" +
    //         $"참고할 문장: \"{timeBasedMessage}\"";
    //     
    //     return prompt;
    // }
    //
    // public static string GetTimeBasedEventPrompt(CharacterPreset preset, UserSaveData userData, TimeEventType eventType)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //     string relationship = GetRelationshipDescriptionForObservation(preset.intimacy);
    //     string eventTopic = "";
    //
    //     switch (eventType)
    //     {
    //         case TimeEventType.Dawn: eventTopic = "사용자가 아직 잠들지 않았거나 혹은 매우 일찍 일어난 상황에 대해, 왜 깨어있는지 궁금해하거나 건강을 걱정하는 내용"; break;
    //         case TimeEventType.Morning: eventTopic = "사용자에게 아침 인사를 건네며 오늘 하루를 응원하는 내용"; break;
    //         case TimeEventType.Lunch: eventTopic = "사용자에게 점심 시간임을 알리거나, 점심은 먹었는지 안부를 묻는 내용"; break;
    //         case TimeEventType.Evening: eventTopic = "사용자에게 저녁이 되었음을 알리며, 오늘 하루가 어땠는지 질문하거나 수고했다는 위로를 건네는 내용"; break;
    //         case TimeEventType.Night: eventTopic = "사용자에게 잠들 시간임을 상기시키며, 좋은 밤 되라는 인사를 건네는 내용"; break;
    //     }
    //
    //     string prompt =
    //         $"너는 '{preset.characterName}'이야. 지금은 {eventType.ToString()} 시간대야. " +
    //         $"'{userName}'(나)와 너의 관계({relationship})와 너의 성격({preset.personality})을 고려해서, 아래 주제에 맞는 자연스러운 안부 인사를 한 문장으로 말해줘.\n\n" +
    //         $"주제: {eventTopic}";
    //
    //     return prompt;
    // }
    //
    // public static string GetRandomEventPrompt(CharacterPreset preset, UserSaveData userData, RandomEventType eventType)
    // {
    //     string userName = userData?.userName ?? "사용자";
    //     string relationship = GetRelationshipDescriptionForObservation(preset.intimacy);
    //     string eventTopic = "";
    //
    //     switch (eventType)
    //     {
    //         case RandomEventType.Compliment: eventTopic = "이유 없이 그냥 사용자를 칭찬하거나, 사용자의 좋은 점에 대해 이야기하는 내용"; break;
    //         case RandomEventType.Question: eventTopic = "사용자의 취미나 최근 관심사, 또는 좋아하는 것에 대해 가볍게 질문하는 내용"; break;
    //         case RandomEventType.Joke: eventTopic = "너의 지능 수준에 맞는 아재 개그나 짧은 농담을 던지는 내용"; break;
    //         case RandomEventType.Encouragement: eventTopic = "사용자가 무언가에 지쳐 보일 수 있다고 가정하고, 힘내라고 응원하거나 격려하는 내용"; break;
    //     }
    //
    //     string prompt =
    //         $"너는 '{preset.characterName}'이야. 문득 사용자에 대한 생각이 나서 말을 걸었어. " +
    //         $"'{userName}'(나)와 너의 관계({relationship})와 너의 성격({preset.personality})을 고려해서, 아래 주제에 맞는 자연스러운 말을 한 문장으로 건네봐.\n\n" +
    //         $"주제: {eventTopic}";
    //
    //     return prompt;
    // }
    //
    #endregion

    [System.Serializable]
    private class MessageData
    {
        public string type;
        public string textContent;
    }
}