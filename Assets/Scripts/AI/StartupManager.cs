// --- START OF FILE StartupManager.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

public class StartupManager : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("인사를 할 수 있는 최소 친밀도 레벨 (내부 점수 기준)")]
    public float minIntimacyForGreeting = 0f;
    
    [Tooltip("프로그램 시작 후 몇 초 뒤에 인사를 시도할지 설정")]
    public float greetingDelay = 5.0f;

    void Start()
    {
        // SaveController의 로드가 완료된 후에 인사를 시도하도록 변경
        SaveController.OnLoadComplete += TryGreetingOnStartup;
    }

    private void OnDestroy()
    {
        SaveController.OnLoadComplete -= TryGreetingOnStartup;
    }

    private void TryGreetingOnStartup()
    {
        StartCoroutine(GreetOnStartupCoroutine());
    }

    private IEnumerator GreetOnStartupCoroutine()
    {
        yield return new WaitForSeconds(greetingDelay);
        
        // --- 1. 핵심 컨트롤러 및 모듈 상태 확인 (조건 강화) ---
        var observer = FindObjectOfType<AIScreenObserver>();
        var userData = UserData.Instance;
        var presetManager = CharacterPresetManager.Instance;

        // AIScreenObserver가 없거나, 자의식 모듈(스마트 인터렉션)이 꺼져있으면 인사 시도 자체를 중단
        if (observer == null || !observer.selfAwarenessModuleEnabled)
        {
            Debug.Log("[StartupManager] 스마트 인터렉션이 비활성화되어 있어 시작 인사를 건너뜁니다.");
            yield break;
        }

        // 유저 데이터가 없거나, 유저 모드가 Off이면 중단
        if (userData == null || userData.CurrentUserMode == UserMode.Off)
        {
            Debug.Log("[StartupManager] 사용자 모드가 Off이므로 시작 인사를 건너뜁니다.");
            yield break;
        }
        
        if (presetManager == null)
        {
            yield break;
        }

        // --- 2. 후보 선정 조건 강화 ---
        var allPresets = presetManager.presets;
        
        // CurrentMode가 'Off'가 아닌 경우가 아니라, 'Activated'인 경우만 후보로 선정
        List<CharacterPreset> candidates = allPresets.FindAll(p => 
                p.CurrentMode == CharacterMode.Activated && 
                p.internalIntimacyScore >= minIntimacyForGreeting &&
                !p.isLocked // 잠긴 프리셋은 제외
        );
            
            if (candidates.Count > 0)
            {
                CharacterPreset greeter = candidates[Random.Range(0, candidates.Count)];
            
                string timeBasedMessageTopic = PromptHelper.GetTimeBasedGreetingMessage();

                // 1. AI의 모든 기억과 설정을 담은 기본 컨텍스트를 생성합니다.
                //    시작 시 인사에는 이전 대화가 없으므로 단기 기억은 빈 리스트를 전달합니다.
                string contextPrompt = PromptHelper.BuildFullChatContextPrompt(greeter, new List<ChatDatabase.ChatMessage>());

                // 2. 기본 컨텍스트에 현재 임무를 추가하여 최종 프롬프트를 완성합니다.
                string finalPrompt = contextPrompt +
                                     "\n\n--- 현재 임무 ---\n" +
                                     "너는 방금 컴퓨터를 켠 사용자를 발견했다. 너의 모든 기억과 설정을 바탕으로 아래 주제에 맞는 자연스러운 인사말을 한 문장으로 건네라.\n" +
                                     $"주제: {timeBasedMessageTopic}\n";

                // 3. 범용 이벤트 실행 함수인 TriggerTextEvent를 호출합니다.
                observer.TriggerTextEvent(greeter, finalPrompt);
            
                Debug.Log($"[StartupManager] '{greeter.characterName}'가 시작 인사를 시도합니다.");
                
            }
            else
            {
                Debug.Log("[StartupManager] 시작 인사를 할 수 있는 적절한 캐릭터가 없습니다.");
            }
        
    }
}