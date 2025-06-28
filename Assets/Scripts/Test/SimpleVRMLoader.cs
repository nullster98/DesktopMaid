using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UniGLTF;
using UniVRM10;
using SFB;

public class SimpleVRMLoader : MonoBehaviour
{
    [Header("애니메이터 컨트롤러")]
    public RuntimeAnimatorController animatorController;

    [Header("모델 위치 및 회전")]
    public Vector3 spawnPosition = Vector3.zero;
    public Vector3 spawnRotation = new Vector3(0, 180f, 0);

    private GameObject currentModel;

    void Start()
    {
        OpenFileDialog();
    }

    public void OpenFileDialog()
    {
        var extensions = new[] { new ExtensionFilter("VRM 파일", "vrm") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("VRM 1.0 모델 선택", "", extensions, false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            LoadVRM10Model(paths[0]);
        }
    }

    public async void LoadVRM10Model(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"❌ VRM 파일이 없습니다: {path}");
            return;
        }

        try
        {
            var parser = new GlbFileParser(path).Parse();
            var vrm10  = Vrm10Data.Parse(parser);

            using var importer = new Vrm10Importer(vrm10);
            RuntimeGltfInstance instance = await importer.LoadAsync(new RuntimeOnlyAwaitCaller());

            if (instance == null || instance.Root == null)
            {
                Debug.LogError("❌ 모델 로드 실패: Root가 null입니다.");
                return;
            }

            instance.ShowMeshes();
            currentModel = instance.Root;
            currentModel.name = $"VRM_1.0_{Path.GetFileNameWithoutExtension(path)}";
            currentModel.transform.position = spawnPosition;
            currentModel.transform.rotation = Quaternion.Euler(spawnRotation);

            // Animator 연결
            Animator anim = currentModel.GetComponent<Animator>();
            if (anim == null)
                anim = currentModel.AddComponent<Animator>();

            if (anim.runtimeAnimatorController == null && animatorController != null)
            {
                anim.runtimeAnimatorController = animatorController;
                Debug.Log($"✅ Animator 연결 완료 → {animatorController.name}");
            }

            Debug.Log($"✅ VRM 모델 로드 성공: {path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRMLoader] 예외 발생: {ex.Message}");
        }
    }
}