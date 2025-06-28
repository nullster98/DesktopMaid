// --- START OF FILE StartupManager.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        
        if (WindowAutoStart.WasStartedByAutoRun)
        {
            if (UserData.Instance == null || UserData.Instance.CurrentUserMode == UserMode.Off)
            {
                yield break;
            }
            
            // [수정] CharacterPresetManager.Instance 확인 추가
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
                
                // [수정] PromptHelper를 통해 시간에 맞는 인사말 주제를 가져옴
                string timeBasedMessage = PromptHelper.GetTimeBasedGreetingMessage();
                
                var observer = FindObjectOfType<AIScreenObserver>();
                if (observer != null)
                {
                    observer.TriggerGreetingMessage(greeter, timeBasedMessage);
                }
            }
        }
    }
}