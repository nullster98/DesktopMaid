using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;
using UniVRM10;

public class ExpressionController : MonoBehaviour
{
    private VRMBlendShapeProxy proxy;
    private Vrm10Instance vrm1Instance;
    private Vrm10RuntimeExpression expression;
    private Animator animator;

    private void EnsureDependencies()
    {
        if (proxy == null)
            proxy = GetComponentInChildren<VRMBlendShapeProxy>(true);

        if (vrm1Instance == null)
            vrm1Instance = GetComponentInChildren<Vrm10Instance>(true);
        if (vrm1Instance != null && expression == null)
            expression = vrm1Instance.Runtime.Expression;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    void Awake()  { EnsureDependencies(); }   // 씬-프리팹 대비
    void OnEnable(){ EnsureDependencies(); }   // SetActive(true) 대비

    public void Happy()
    {
        SetExpression(BlendShapePreset.Joy);
    }

    public void Angry()
    {
        SetExpression(BlendShapePreset.Angry);
    }

    public void ResetExpression()
    {
        FullResetExpression();
        SetExpression(BlendShapePreset.Neutral);
    }

    public void FullResetExpression()
    {
        // VRM0.x: 모든 BlendShape 클립 값을 0으로 리셋
        if (proxy != null && proxy.BlendShapeAvatar != null)
        {
            foreach (var clip in proxy.BlendShapeAvatar.Clips)
            {
                if (clip != null && clip.Preset != BlendShapePreset.Unknown)
                {
                    proxy.ImmediatelySetValue(clip.Preset, 0f);
                }
            }
        }
        // VRM1.x: 모든 Expression 값을 0으로 리셋
        if (expression != null)
        {
            foreach (ExpressionPreset preset in Enum.GetValues(typeof(ExpressionPreset)))
            {
                if (preset == ExpressionPreset.custom) continue;
                expression.SetWeight(ExpressionKey.CreateFromPreset(preset), 0f);
            }
        }
        // Animator 트리거를 통한 추가 리셋 동작
        if (animator != null)
        {
            animator.SetTrigger("Reset");
            StartCoroutine(ClearTriggerNextFrame("Reset"));
        }
    }

    private IEnumerator ClearTriggerNextFrame(string triggerName)
    {
        yield return null; // 한 프레임 대기
        if (animator != null) animator.ResetTrigger(triggerName);
    }

    public void PlayDance()
    {
        if (animator != null)
        {
            Debug.Log("✅ PlayDance() 호출됨, 트리거 실행");
            animator.SetTrigger("Dance");
        }
        else
        {
            Debug.LogError("❌ PlayDance: animator가 null입니다.");
        }
    }

    public void SetExpression(BlendShapePreset preset)
    {
        EnsureDependencies();
        // VRM0.x: 다른 표정을 초기화한 후 지정된 표정 적용
        if (proxy != null)
        {
            proxy.ImmediatelySetValue(BlendShapePreset.Neutral, 1.0f); // 먼저 중립으로 초기화
            proxy.ImmediatelySetValue(preset, 1.0f);                    // 새 표정 적용
        }
        // VRM1.x: 현재 표정을 모두 초기화한 후 지정된 표정 적용
        if (expression != null)
        {
            // Neutral 처리: 모든 Expression 0으로 설정 후 종료
            if (preset == BlendShapePreset.Neutral)
            {
                foreach (ExpressionPreset ep in Enum.GetValues(typeof(ExpressionPreset)))
                {
                    if (ep == ExpressionPreset.custom) continue;
                    expression.SetWeight(ExpressionKey.CreateFromPreset(ep), 0f);
                }
                return;
            }

            // VRM0 프리셋 -> VRM1 ExpressionPreset 매핑
            ExpressionPreset expPreset;
            switch (preset)
            {
                case BlendShapePreset.Joy:    expPreset = ExpressionPreset.happy; break;
                case BlendShapePreset.Angry:  expPreset = ExpressionPreset.angry; break;
                case BlendShapePreset.Sorrow: expPreset = ExpressionPreset.sad; break;
                case BlendShapePreset.Fun:    expPreset = ExpressionPreset.relaxed; break;
                case BlendShapePreset.Blink:  expPreset = ExpressionPreset.blink; break;
                case BlendShapePreset.Blink_L:expPreset = ExpressionPreset.blinkLeft; break;
                case BlendShapePreset.Blink_R:expPreset = ExpressionPreset.blinkRight; break;
                case BlendShapePreset.A:      expPreset = ExpressionPreset.aa; break;
                case BlendShapePreset.I:      expPreset = ExpressionPreset.ih; break;
                case BlendShapePreset.U:      expPreset = ExpressionPreset.ou; break;
                case BlendShapePreset.E:      expPreset = ExpressionPreset.ee; break;
                case BlendShapePreset.O:      expPreset = ExpressionPreset.oh; break;
                default:
                    Debug.LogWarning($"⚠️ SetExpression: '{preset}' VRM1 매핑 없음 (무시됨)");
                    return;
            }
            // 모든 표정 초기화 후 새로운 표정 적용
            foreach (ExpressionPreset ep in Enum.GetValues(typeof(ExpressionPreset)))
            {
                if (ep == ExpressionPreset.custom) continue;
                expression.SetWeight(ExpressionKey.CreateFromPreset(ep), 0f);
            }
            expression.SetWeight(ExpressionKey.CreateFromPreset(expPreset), 1.0f);
        }
    }

    public void SetBlendShapeProxy(VRMBlendShapeProxy newProxy)
    {
        proxy = newProxy;
        // Animator도 새로 연결 시도
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log("✅ Animator 연결 완료 (SetBlendShapeProxy 내부)");
            }
        }
    }

    public void SetVrm10Instance(Vrm10Instance newInstance)
    {
        vrm1Instance = newInstance;
        if (vrm1Instance != null)
        {
            expression = vrm1Instance.Runtime.Expression;
            Debug.Log("✅ Vrm10Instance 연결 완료 (SetVrm10Instance 내부)");
        }
        // Animator 연결 확인
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log("✅ Animator 연결 완료 (SetVrm10Instance 내부)");
            }
        }
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        Debug.Log("✅ Animator 수동 연결 완료 (SetAnimator)");
    }
}
