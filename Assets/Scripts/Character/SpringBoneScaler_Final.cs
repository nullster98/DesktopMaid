using UnityEngine;
using VRM; // UniVRM 네임스페이스
using System.Collections.Generic;
using System.Reflection;

// 이 스크립트를 VRM 모델의 루트 오브젝트에 추가하세요.
public class SpringBoneScaler_Final : MonoBehaviour
{
    class Rec
    {
        public VRMSpringBone sb;
        public float k0, g0, d0, r0;
        public List<object> joints = new();
        public List<float> jointR0 = new();
    }

    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Rec> list = new();
    float prevScale = 1f;
    bool ready;

    void Start() { if (!ready) Init(); }

    public void Init()
    {
        list.Clear();
        foreach (var sb in GetComponentsInChildren<VRMSpringBone>(true))
        {
            var rec = new Rec
            {
                sb  = sb,
                k0  = sb.m_stiffnessForce,
                g0  = sb.m_gravityPower,
                d0  = sb.m_dragForce,
                r0  = sb.m_hitRadius
            };

            // m_joints(List<VRMSpringBoneJoint>) 가져오기
            var jointsField = typeof(VRMSpringBone).GetField("m_joints", F);
            if (jointsField != null)
            {
                var arr = jointsField.GetValue(sb) as IList<object>;
                if (arr != null)
                {
                    rec.joints.AddRange(arr);
                    foreach (var j in arr)
                    {
                        var radF = j.GetType().GetField("radius", F);
                        if (radF != null) rec.jointR0.Add((float)radF.GetValue(j));
                        else              rec.jointR0.Add(sb.m_hitRadius); // fallback
                    }
                }
            }
            list.Add(rec);
        }
        prevScale = transform.localScale.x;
        Apply(prevScale);
        ready = true;
        Debug.Log($"[FixedSpringBoneScaler] {list.Count}개 본 감시 시작");
    }

    void LateUpdate()
    {
        if (!ready) return;
        float s = transform.localScale.x;
        if (Mathf.Approximately(s, prevScale) || s <= 0f) return;
        Apply(s);
        prevScale = s;
    }

    void Apply(float s)
    {
        foreach (var r in list)
        {
            // 본 자체
            r.sb.m_stiffnessForce = r.k0 / s;
            r.sb.m_gravityPower   = r.g0 * s;
            r.sb.m_dragForce      = r.d0;
            r.sb.m_hitRadius      = r.r0 / s;

            // joint.radius 동기화
            for (int i = 0; i < r.joints.Count; ++i)
            {
                var radF = r.joints[i].GetType().GetField("radius", F);
                if (radF != null) radF.SetValue(r.joints[i], r.jointR0[i] / s);
            }
            
            var setup = typeof(VRMSpringBone).GetMethod("Setup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            setup?.Invoke(r.sb, null);
        }
    }
}