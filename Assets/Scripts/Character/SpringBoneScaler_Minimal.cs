using UnityEngine;
using VRM;
using System.Collections.Generic;

public class SpringBoneScaler_Minimal : MonoBehaviour
{
    private class BoneInfo
    {
        public VRMSpringBone SpringBone;
        public float OriginalStiffness;
        public float OriginalRadius;
    }

    private List<BoneInfo> boneInfos = new List<BoneInfo>();
    private float lastScale = 1.0f;
    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (isInitialized) return;

        boneInfos.Clear();
        foreach (var sb in GetComponentsInChildren<VRMSpringBone>(true))
        {
            boneInfos.Add(new BoneInfo
            {
                SpringBone = sb,
                OriginalStiffness = sb.m_stiffnessForce,
                OriginalRadius = sb.m_hitRadius
            });
        }
        
        lastScale = transform.localScale.x;
        UpdateParameters(lastScale);
        isInitialized = true;
        Debug.Log($"[Minimal Scaler] 초기화 완료. {boneInfos.Count}개의 스프링 본 추적.");
    }

    void LateUpdate()
    {
        if (!isInitialized) return;

        float currentScale = transform.localScale.x;
        if (Mathf.Approximately(currentScale, lastScale)) return;
        
        UpdateParameters(currentScale);
        lastScale = currentScale;
    }

    void UpdateParameters(float scale)
    {
        if (Mathf.Approximately(scale, 0f)) return;

        foreach (var info in boneInfos)
        {
            // 가장 확실한 두 가지만 스케일에 반비례하여 조정
            info.SpringBone.m_stiffnessForce = info.OriginalStiffness / scale;
            info.SpringBone.m_hitRadius = info.OriginalRadius / scale;

            // Gravity와 Drag는 일단 원본 값 그대로 둡니다.
            // info.SpringBone.m_gravityPower = ... (조정 안 함)
            // info.SpringBone.m_dragForce = ... (조정 안 함)
        }
    }
}