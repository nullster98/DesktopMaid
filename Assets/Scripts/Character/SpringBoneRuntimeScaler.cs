/*
 * SpringBoneRuntimeScaler.cs (v7 - 물리 수식 및 초기화 로직 수정)
 * ------------------------------------------------------------
 * • [수정] Initialize 시점에, VRM에 설정된 "원본 파라미터 값"을 그대로 저장합니다. (계산 없음)
 * • [수정] LateUpdate의 UpdateParameters에서 "원본 값"과 "현재 스케일"만으로
 *   물리적으로 올바른 최종 파라미터를 계산하여 적용합니다.
 * • [수정] Stiffness, Radius는 스케일에 반비례, Gravity는 스케일에 정비례하도록 수식 변경.
 * • [수정] 누적 오차와 복잡성을 제거하여 안정성을 극대화했습니다.
 * ------------------------------------------------------------
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRM;

// DragController가 없어도 독립적으로 작동할 수 있도록 RequireComponent 제거
public class SpringBoneRuntimeScaler : MonoBehaviour
{
    private class SpringBoneRecord
    {
        // 이 값들은 모두 VRM에 설정된 "원본(Original)" 값입니다.
        public VRMSpringBone BoneComponent;
        public float OriginalStiffness;
        public float OriginalGravity;
        public float OriginalDrag;
        public float OriginalHitRadius;
    }

    private readonly List<SpringBoneRecord> _records = new();
    private float _lastScale = 1.0f;
    private bool _isInitialized = false;

    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    void Start()
    {
        // LoadNewVRM에서 Initialize를 먼저 호출했을 수 있으므로 확인
        if (!_isInitialized)
        {
            Initialize();
        }
    }
    
    // LoadNewVRM에서 즉시 호출할 수 있도록 public으로 유지
    public void Initialize()
    {
        if (_isInitialized) return;

        _records.Clear();
        
        var springBones = GetComponentsInChildren<VRMSpringBone>(true);
        if (springBones.Length == 0)
        {
            // 스프링 본이 없는 모델일 수 있으므로 경고 대신 로그 출력 후 종료
            Debug.Log($"[{gameObject.name}] 추적할 SpringBone이 없어 Scaler 초기화를 건너뜁니다.");
            _isInitialized = true; // 더 이상 시도하지 않도록 초기화 완료 처리
            return;
        }

        foreach (var sb in springBones)
        {
            var record = new SpringBoneRecord { BoneComponent = sb };

            // [수정] 계산 없이 원본 파라미터 값을 그대로 저장합니다.
            record.OriginalStiffness = GetFloatValue(sb, "StiffnessForce", "m_stiffnessForce");
            record.OriginalGravity = GetFloatValue(sb, "GravityPower", "m_gravityPower");
            record.OriginalDrag = GetFloatValue(sb, "DragForce", "m_dragForce");
            record.OriginalHitRadius = GetFloatValue(sb, "HitRadius", "m_hitRadius");
            
            _records.Add(record);
        }
        
        _lastScale = transform.localScale.x;
        // 저장된 원본 값을 기반으로 현재 스케일에 맞게 즉시 파라미터 업데이트
        UpdateParameters(_lastScale);
        _isInitialized = true;
        
        Debug.Log($"✅ [{gameObject.name}] SpringBoneRuntimeScaler(v7) 초기화 완료. {_records.Count}개의 SpringBone을 추적합니다.");
    }

    void LateUpdate()
    {
        if (!_isInitialized || _records.Count == 0) return;
        
        float currentScale = transform.localScale.x;
        
        // 스케일이 0에 가깝거나, 이전 프레임과 거의 같다면 업데이트 생략 (성능 최적화)
        if (Mathf.Approximately(currentScale, 0f) || Mathf.Approximately(currentScale, _lastScale))
        {
            return;
        }

        UpdateParameters(currentScale);
        _lastScale = currentScale;
    }

    private void UpdateParameters(float currentScale)
    {
        foreach (var r in _records)
        {
            // [수정] 물리적으로 올바른 수식을 사용하여 최종 값을 계산
            // Stiffness: 스케일이 커지면 본 사이의 거리가 늘어나므로, 탄성 계수는 줄여야 안정적이다. (반비례)
            float finalStiffness = r.OriginalStiffness / currentScale;

            // Gravity: 스케일이 커지면 질량도 커진다고 가정하여 중력의 '힘'을 키운다. (정비례)
            float finalGravity = r.OriginalGravity * currentScale;
            
            // Drag: 속도가 스케일에 비례하여 빨라지므로, 저항도 동일하게 유지하거나 약간 줄여 안정화. 원본 값 유지가 가장 안정적.
            float finalDrag = r.OriginalDrag;

            // Radius: localScale에 의해 이미 스케일링되므로, 파라미터 값은 반비례로 줄여 최종 월드 크기를 유지해야 한다.
            float finalHitRadius = r.OriginalHitRadius / currentScale;

            // 계산된 최종 값을 컴포넌트에 적용
            SetFloatValue(r.BoneComponent, "StiffnessForce", "m_stiffnessForce", finalStiffness);
            SetFloatValue(r.BoneComponent, "GravityPower", "m_gravityPower", finalGravity);
            SetFloatValue(r.BoneComponent, "DragForce", "m_dragForce", finalDrag);
            SetFloatValue(r.BoneComponent, "HitRadius", "m_hitRadius", finalHitRadius);
        }
    }

    #region Reflection Helpers (이 부분은 수정 없음)
    private static MemberInfo GetMemberInfo(object obj, string memberName) => obj.GetType().GetMember(memberName, FLAGS | BindingFlags.IgnoreCase).FirstOrDefault();
    private static object GetMemberValue(object obj, string memberName) => GetMemberValue(GetMemberInfo(obj, memberName), obj);
    private static object GetMemberValue(MemberInfo m, object obj)
    {
        if (m == null) return null;
        return m switch { FieldInfo f => f.GetValue(obj), PropertyInfo p => p.GetValue(obj), _ => null };
    }
    private static void SetMemberValue(MemberInfo m, object obj, object val)
    {
        if (m == null) return;
        switch (m) { case FieldInfo f: f.SetValue(obj, val); break; case PropertyInfo p when p.CanWrite: p.SetValue(obj, val); break; }
    }
    private static float GetFloatValue(object obj, string propName, string fieldName)
    {
        var member = GetMemberInfo(obj, propName) ?? GetMemberInfo(obj, fieldName);
        var value = GetMemberValue(member, obj);
        return value == null ? 0f : Convert.ToSingle(value);
    }
    private static void SetFloatValue(object obj, string propName, string fieldName, float value)
    {
        var member = GetMemberInfo(obj, propName) ?? GetMemberInfo(obj, fieldName);
        if (member != null) SetMemberValue(member, obj, value);
    }
    #endregion
}