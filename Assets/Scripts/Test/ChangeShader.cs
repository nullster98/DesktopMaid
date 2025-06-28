using UnityEngine;

public class ChangeShader : MonoBehaviour
{
    [ContextMenu("Replace All Shaders to lilToon (lts)")]
    public void ReplaceShadersToLilToon()
    {
        Shader lilToonShader = Shader.Find("lilToon");  // 또는 "Hidden/lts_o" 등

        if (lilToonShader == null)
        {
            Debug.LogError("Hidden/lts 셰이더를 찾을 수 없습니다. Shader가 빌드에 포함되어 있는지 확인하세요.");
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

        Debug.Log("✅ 모든 셰이더를 Hidden/lts로 변경 완료");
    }
}