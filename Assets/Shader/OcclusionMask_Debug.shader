// 파일 이름: OcclusionMask_Debug.shader
Shader "Unlit/OcclusionMask_Debug"
{
    Properties
    {
        _Color ("Debug Color", Color) = (1, 0, 0, 0.3) // 반투명한 빨간색
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite On                 // Z-Depth는 계속 기록 (벽의 역할)
            Blend SrcAlpha OneMinusSrcAlpha // 반투명 설정
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { 
                float4 vertex : POSITION; 
            }; // 중괄호를 여기에 맞게 수정

            struct v2f { 
                float4 vertex : SV_POSITION; 
            }; // 중괄호를 여기에 맞게 수정

            fixed4 _Color;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                return _Color; // 인스펙터에서 설정한 색상을 그대로 출력
            }
            ENDCG
        }
    }
}