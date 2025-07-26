// --- START OF FILE StartupManager.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings; // 언어 설정을 위해 추가

public class StartupManager : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("인사를 할 수 있는 최소 친밀도 레벨 (내부 점수 기준)")]
    public float minIntimacyForGreeting = 0f;
    
    [Tooltip("프로그램 시작 후 몇 초 뒤에 인사를 시도할지 설정")]
    public float greetingDelay = 5.0f;

    void Start()
    {
        StartCoroutine(GreetOnStartupCoroutine());
    }

    private IEnumerator GreetOnStartupCoroutine()
    {
        yield return new WaitForSeconds(greetingDelay);
        
        // 자동 시작으로 실행되었을 때만 인사하도록 하는 로직 (디버깅 시에는 비활성화 가능)
        // if (WindowAutoStart.WasStartedByAutoRun) 
        {
            if (UserData.Instance == null || UserData.Instance.CurrentUserMode == UserMode.Off)
            {
                yield break;
            }
            
            if (CharacterPresetManager.Instance == null)
            {
                yield break;
            }

            var allPresets = CharacterPresetManager.Instance.presets;
            List<CharacterPreset> candidates = allPresets.FindAll(p => 
                p.CurrentMode != CharacterMode.Off && 
                p.internalIntimacyScore >= minIntimacyForGreeting
            );
            
            if (candidates.Count > 0)
            {
                CharacterPreset greeter = candidates[Random.Range(0, candidates.Count)];
                
                string timeBasedMessageTopic = PromptHelper.GetTimeBasedGreetingMessage();
                
                var observer = FindObjectOfType<AIScreenObserver>();
                if (observer != null)
                {
                    // --- 로직 수정 부분 ---
                    // 1. AI의 모든 기억과 설정을 담은 기본 컨텍스트를 생성합니다.
                    //    시작 시 인사에는 이전 대화가 없으므로 단기 기억은 빈 리스트를 전달합니다.
                    string contextPrompt = PromptHelper.BuildFullChatContextPrompt(greeter, new List<ChatDatabase.ChatMessage>());
                    
                    // 2. 현재 언어 설정을 가져옵니다.
                    var currentLocale = LocalizationSettings.SelectedLocale;
                    string languageName = currentLocale != null ? currentLocale.LocaleName : "한국어";

                    // 3. 기본 컨텍스트에 현재 임무를 추가하여 최종 프롬프트를 완성합니다.
                    string finalPrompt = contextPrompt +
                        "\n\n--- 현재 임무 ---\n" +
                        "너는 방금 컴퓨터를 켠 사용자를 발견했다. 너의 모든 기억과 설정을 바탕으로 아래 주제에 맞는 자연스러운 인사말을 한 문장으로 건네라.\n" +
                        $"주제: {timeBasedMessageTopic}\n" +
                        $"너의 답변은 반드시 '{languageName}'(으)로 작성해야 한다.";

                    // 4. 범용 이벤트 실행 함수인 TriggerTextEvent를 호출합니다.
                    observer.TriggerTextEvent(greeter, finalPrompt);
                }
            }
        }
    }
}
// --- END OF FILE StartupManager.cs ---