// 파일 이름: OcclusionMask.shader
Shader "Unlit/OcclusionMask"
{
    SubShader
    {
        // 렌더링 순서를 일반 오브젝트보다 약간 앞으로 당깁니다.
        Tags { "Queue"="Geometry-1" }
        LOD 100

        Pass
        {
            // Z-Depth 버퍼에는 값을 씁니다. (벽의 역할을 함)
            ZWrite On
            
            // 하지만 화면의 색상 버퍼에는 아무것도 쓰지 않습니다. (투명하게 보임)
            ColorMask 0
        }
    }
}