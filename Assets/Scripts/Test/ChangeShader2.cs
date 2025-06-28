using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeShader2 : MonoBehaviour
{
    // 이 GameObject 하위의 모든 머티리얼을 lilToon으로 변경
    [ContextMenu("Replace All Shaders to MToon")]
    public void ReplaceShadersToLilToon()
    {
        Shader lilToonShader = Shader.Find("VRM/MToon");

        if (lilToonShader == null)
        {
            Debug.LogError("lilToon/lilToon 셰이더를 찾을 수 없습니다. Shader가 프로젝트에 포함되어 있는지 확인하세요.");
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                mat.shader = lilToonShader;
                Debug.Log($"셰이더 변경됨: {renderer.gameObject.name} → {mat.name}");
            }
        }

        Debug.Log("💡 모든 셰이더를 lilToon/lilToon으로 변경 완료");
    }
}
