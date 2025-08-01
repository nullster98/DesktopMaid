using UnityEngine;
using VRM; // VRM 0.x의 BlendShapePreset을 사용하기 위해 필요

public class SetExpressionOnState : StateMachineBehaviour
{
    [Tooltip("애니메이션 시작 시 적용할 표정입니다.")]
    public BlendShapePreset expressionOnEnter = BlendShapePreset.Joy;

    [Tooltip("애니메이션 종료 시 표정을 리셋할지 여부입니다.")]
    public bool resetOnExit = true;

    private ExpressionController expressionController;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    // 애니메이션 상태에 진입할 때 호출됩니다.
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Animator가 붙어있는 게임 오브젝트에서 ExpressionController를 찾습니다.
        if (expressionController == null)
        {
            expressionController = animator.GetComponent<ExpressionController>();
        }

        if (expressionController != null)
        {
            // 설정된 표정을 적용합니다.
            // ExpressionController의 SetExpression 함수는 VRM 0.x와 1.x를 모두 처리해줍니다.
            expressionController.SetExpression(expressionOnEnter);
            Debug.Log($"[SetExpressionOnState] '{expressionOnEnter}' 표정 적용됨.");
        }
        else
        {
            Debug.LogError("[SetExpressionOnState] ExpressionController를 찾을 수 없습니다!");
        }
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    // 애니메이션 상태에서 빠져나갈 때 호출됩니다.
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (expressionController != null && resetOnExit)
        {
            // 표정을 중립(Neutral) 상태로 리셋합니다.
            expressionController.ResetExpression();
            Debug.Log("[SetExpressionOnState] 표정 리셋됨.");
        }
    }
}