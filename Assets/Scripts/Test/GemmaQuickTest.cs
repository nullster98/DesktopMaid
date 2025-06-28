using UnityEngine;
using GemmaCpp;
using Cysharp.Threading.Tasks;   // UniTask를 쓰려면 필요

// GemmaManager보다 한 프레임 늦게 Start 되도록 우선순위 조정(선택)
// [DefaultExecutionOrder(100)]
public class GemmaQuickTest : MonoBehaviour
{
    [SerializeField] private GemmaManager gemma;   // Inspector에서 GemmaRuntime drag

    async void Start()
    {
        // GemmaManager.Initialized 가 true 될 때까지 대기
        await UniTask.WaitUntil(() => gemma != null && gemma.Initialized);

        string finalText = await gemma.GenerateResponseAsync(
            "안녕! Unity에서 로컬 Gemma 4B 모델을 실행 중이야. 자기소개해 줘.",
            token =>
            {
                Debug.Log(token);   // 토큰 단위 실시간 출력
                return true;        // false 반환 시 스트리밍 중단
            });

        Debug.Log("=== 최종 응답 ===");
        Debug.Log(finalText);
    }
}