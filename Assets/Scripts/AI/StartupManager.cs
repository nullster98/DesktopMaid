using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 프로그램 시작 시 AI가 사용자에게 인사를 건네는 로직을 관리합니다.
/// </summary>
public class StartupManager : MonoBehaviour
{
    [Header("인사 설정")]
    [Tooltip("프로그램 시작 후 인사를 시도하기까지의 대기 시간 (초)")]
    [SerializeField] private float greetingDelay = 5.0f;
    
    [Tooltip("AI가 시작 인사를 건넬 수 있는 최소 친밀도 점수")]
    [SerializeField] private float minIntimacyForGreeting = 0f;

    private void Start()
    {
        // 프로그램 시작 시 인사 로직을 담은 코루틴 실행
        StartCoroutine(GreetOnStartupCoroutine());
    }

    /// <summary>
    /// 지정된 시간만큼 대기한 후, 조건에 맞는 AI가 사용자에게 인사를 하도록 처리하는 코루틴.
    /// </summary>
    private IEnumerator GreetOnStartupCoroutine()
    {
        yield return new WaitForSeconds(greetingDelay);

        // 자율 행동이 가능한 상태인지 먼저 확인
        var observer = FindObjectOfType<AIScreenObserver>();
        if (observer == null || !observer.selfAwarenessModuleEnabled || 
            (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off))
        {
            yield break; // 조건 미충족 시 조용히 종료
        }
        
        if (CharacterPresetManager.Instance == null)
        {
            yield break;
        }

        // 1. 인사할 수 있는 후보 캐릭터 필터링
        List<CharacterPreset> candidates = CharacterPresetManager.Instance.presets.FindAll(p => 
            p.CurrentMode != CharacterMode.Off && 
            !p.hasSaidFarewell && // 이미 작별인사를 한 상태가 아니고
            p.internalIntimacyScore >= minIntimacyForGreeting // 최소 친밀도 조건을 만족하는 캐릭터
        );
        
        if (candidates.Count > 0)
        {
            // 2. 후보 중에서 랜덤으로 한 명 선택
            CharacterPreset greeter = candidates[Random.Range(0, candidates.Count)];
            
            // 3. 현재 시간에 맞는 인사 주제 가져오기
            string timeBasedMessageTopic = PromptHelper.GetTimeBasedGreetingMessage();
            
            // 4. AI의 모든 정보를 담은 완전한 프롬프트 생성
            // 시작 인사에는 이전 대화 기록이 없으므로 빈 리스트를 전달
            string contextPrompt = PromptHelper.BuildFullChatContextPrompt(greeter, new List<ChatDatabase.ChatMessage>());

            // 5. 최종 임무를 부여하여 프롬프트 완성
            string finalPrompt = contextPrompt +
                "\n\n--- 현재 임무 ---\n" +
                "너는 방금 컴퓨터를 켠 사용자를 발견했다. 너의 모든 기억과 설정을 바탕으로 아래 주제에 맞는 자연스러운 인사말을 한 문장으로 건네라.\n" +
                $"주제: {timeBasedMessageTopic}\n";

            // 6. AIScreenObserver에 텍스트 이벤트 실행 요청
            Debug.Log($"[StartupManager] '{greeter.characterName}'가 시작 인사를 시도합니다.");
            observer.TriggerTextEvent(greeter, finalPrompt);
        }
    }
}