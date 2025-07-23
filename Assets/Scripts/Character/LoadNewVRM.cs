// --- START OF FILE LoadNewVRM.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
using VRM;
using SFB;
using System.IO;
using System.Threading.Tasks;
using UniGLTF.Extensions;
using UniVRM10;

public class LoadNewVRM : MonoBehaviour
{
    [Header("LoadModel 전용 (외형 교체)")]
    public GameObject baseCharacter;
    public Transform bodyContainer;

    [Header("PlusModel 전용 (새 모델 생성)")]
    public GameObject characterBasePrefab;
    public RuntimeAnimatorController defaultController;

    private GameObject currentModel;


    #region LoadModel

    public void OpenFileAndLoadVRM()
    {
        var extensions = new[] { new ExtensionFilter("VRM Files", "vrm"), };
        var paths = StandaloneFileBrowser.OpenFilePanel("VRM 모델 선택", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            LoadVRM(paths[0]);
        }
    }

    public async void LoadVRM(string path, System.Action<GameObject> onLoaded = null)
    {
        Debug.Log($"📁 LoadVRM 시작: {path}");

        if (!File.Exists(path))
        {
            Debug.LogError($"❌ VRM 파일이 존재하지 않음: {path}");
            return;
        }

        LoadingUIController.instance.Show("VRM 모델을 불러오는 중입니다....");

        try
        {
            var parser = new GlbFileParser(path);
            var gltfData = parser.Parse();

            GameObject vrmModel = null;
            bool isVrm1 = false;

            UniVRM10.Vrm10Data vrm10Data = null;
            try
            {
                vrm10Data = UniVRM10.Vrm10Data.Parse(gltfData);
            }
            catch (Exception) { vrm10Data = null; }

            if (vrm10Data != null)
            {
                try
                {
                    using (var importer = new UniVRM10.Vrm10Importer(vrm10Data))
                    {
                        var instance = await importer.LoadAsync(new ImmediateCaller());
                        if (instance != null && instance.Root != null)
                        {
                            var rootGO = instance.Root;
                            rootGO.SetActive(true);
                            foreach (var r in rootGO.GetComponentsInChildren<Renderer>(true))
                                r.enabled = true;
                            
                            
                            vrmModel = instance.Root;
                            isVrm1 = true;
                            Debug.Log("✅ VRM 1.x 모델 감지 – Vrm10Importer로 로드 완료");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ VRM1.x 모델 로드 실패: {ex.Message} – VRM0.x 방식으로 재시도");
                    vrmModel = null;
                    isVrm1 = false;
                }
            }

            if (vrmModel == null)
            {
                if (isVrm1)
                {
                    gltfData = new GlbFileParser(path).Parse();
                }
                var vrm0Data = new VRMData(gltfData);
                var context = new VRMImporterContext(vrm0Data);

                try
                {
                    var meta = context.ReadMeta(true);
                    Debug.Log($"⚠️ VRM 메타 정보: {meta?.Title ?? "[없음]"} (계속 진행)");
                }
                catch { /* 메타 읽기 실패는 무시 */ }

                var instance0 = await context.LoadAsync(new RuntimeOnlyAwaitCaller());
                if (instance0 == null)
                {
                    Debug.LogError("❌ VRM 0.x 모델 LoadAsync 실패");
                    LoadingUIController.instance.Hide();
                    return;
                }
                instance0.ShowMeshes();
                vrmModel = instance0.Root;
                Debug.Log("✅ VRM 0.x 모델 로드 완료");
            }

            var presetManager = FindObjectOfType<CharacterPresetManager>();
            var preset = presetManager?.GetCurrentPreset();
            if (preset == null)
            {
                Debug.LogError("⚠️ 현재 프리셋이 없습니다.");
                LoadingUIController.instance.Hide();
                return;
            }

            if (preset.vrmModel != null)
            {
                var oldRoot = preset.vrmModel.transform.root.gameObject;
                Destroy(oldRoot);
                preset.vrmModel = null;
                Debug.Log("⚠️ 기존 VRM 모델 및 루트 오브젝트 제거 완료");
            }

            GameObject root = Instantiate(characterBasePrefab);
            root.name = "VRM_" + preset.characterName;
            root.transform.position = new Vector3(0f, -1.54f, -6.73f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Transform container = root.transform.Find("BodyContainer");
            if (container == null)
            {
                Debug.LogError("❌ characterBasePrefab 안에 BodyContainer가 없습니다.");
                LoadingUIController.instance.Hide();
                return;
            }
            vrmModel.transform.SetParent(container, false);
            vrmModel.transform.localPosition = Vector3.zero;
            vrmModel.transform.localRotation = Quaternion.identity;
            vrmModel.transform.localScale = Vector3.one;
            vrmModel.SetActive(false);

            AdjustRendererBounds(vrmModel);
            SetupColliderAndDrag(vrmModel);

            Animator vrmAnimator = vrmModel.GetComponentInChildren<Animator>();
            Animator anim = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            anim.runtimeAnimatorController = defaultController;
            if (vrmAnimator != null && vrmAnimator.avatar != null)
            {
                anim.avatar = vrmAnimator.avatar;
                anim.Rebind();
                Debug.Log("✅ Animator 연결 완료");
            }
            else
            {
                Debug.LogWarning("⚠️ Animator 또는 Avatar가 null입니다.");
            }

            ExpressionController expr = root.GetComponent<ExpressionController>();
            if (expr != null)
            {
                expr.SetAnimator(anim);
                if (isVrm1)
                {
                    var v1Instance = vrmModel.GetComponent<Vrm10Instance>();
                    if (v1Instance != null) expr.SetVrm10Instance(v1Instance);
                }
                else
                {
                    var proxy = vrmModel.GetComponent<VRMBlendShapeProxy>();
                    if (proxy != null) expr.SetBlendShapeProxy(proxy);
                }
            }

            preset.vrmModel = vrmModel;
            preset.vrmFilePath = path;

            // [핵심 추가] CharacterPreset에 생성된 VRM 루트 오브젝트와 그 컴포넌트들을 등록합니다.
            preset.SetupVRMComponents(root);

            SnapAwareVRM snapAware = root.GetComponent<SnapAwareVRM>();
            if (snapAware != null && vrmAnimator != null)
            {
                Transform hips = vrmAnimator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    snapAware.targetTransform = hips;
                    Debug.Log("✅ SnapAwareVRM의 targetTransform을 Hips로 설정했습니다.");
                }
            }
            
            var autoActivate = root.GetComponent<VRMAutoActivate>();
            if (autoActivate != null)
            {
                autoActivate.SetPreset(preset);
            }

            onLoaded?.Invoke(vrmModel);
            Debug.Log($"✅ 최종 preset.vrmModel 적용 완료: {vrmModel.name}");

            LoadingUIController.instance.Hide();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ LoadVRM 예외 발생: {ex.Message}\n{ex.StackTrace}");
            LoadingUIController.instance.Hide();
        }
    }
    
    #endregion
    
    private void AdjustRendererBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("모델에서 Renderer를 찾을 수 없어 Bounds를 조정할 수 없습니다.", model);
            return;
        }

        var totalWorldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            totalWorldBounds.Encapsulate(renderers[i].bounds);
        }

        var localCenter = model.transform.InverseTransformPoint(totalWorldBounds.center);
        var localSize = totalWorldBounds.size;
        localSize *= 1.2f;

        var newLocalBounds = new Bounds(localCenter, localSize);
        
        var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skinnedRenderer in skinnedRenderers)
        {
            skinnedRenderer.localBounds = newLocalBounds;
        }

        Debug.Log($"✅ SkinnedMeshRenderer의 localBounds를 모델 전체 크기로 재설정했습니다. Center: {newLocalBounds.center}, Size: {newLocalBounds.size}", model);
    }
    
    private void SetupColliderAndDrag(GameObject model)
    {
        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Animator가 없어서 콜라이더 설정 실패");
            return;
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        if (head == null || footL == null || footR == null)
        {
            Debug.LogWarning("필수 뼈대(Head 또는 Feet)를 찾을 수 없습니다.");
            return;
        }
        
        Vector3 footPos = ((footL.position + footR.position) * 0.5f) + Vector3.down * 0.15f;
        Vector3 headPos = head.position + Vector3.up * 0.28f;
        
        float height = Vector3.Distance(headPos, footPos);
        Vector3 centerWorld = (headPos + footPos) * 0.5f;

        CapsuleCollider capsule = model.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = model.AddComponent<CapsuleCollider>();
        }

        capsule.direction = 1;
        capsule.height = height;
        capsule.radius = height * 0.1f;
        capsule.center = model.transform.InverseTransformPoint(centerWorld);

        Debug.Log("✅ 콜라이더(모자 포함, 발끝 포함) 생성 완료");

        if (model.GetComponent<DragController>() == null)
        {
            model.AddComponent<DragController>();
            Debug.Log("✅ DragController 부착 완료");
        }
    }
    
    public GameObject InstantiateFromPreset(CharacterPreset preset)
    {
        if (preset == null || preset.vrmModel == null)
        {
            Debug.LogWarning("❌ 프리셋 또는 vrmModel이 null입니다.");
            return null;
        }

        GameObject root = Instantiate(characterBasePrefab);
        root.name = "VRM_" + preset.characterName;
        root.transform.position = new Vector3(0f, -1.54f, -6.73f);
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Transform container = root.transform.Find("BodyContainer");
        if (container == null)
        {
            Debug.LogError("❌ characterBasePrefab 안에 BodyContainer가 없습니다.");
            Destroy(root);
            return null;
        }

        GameObject model = Instantiate(preset.vrmModel, container);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        model.SetActive(false); 

        AdjustRendererBounds(model);
        SetupColliderAndDrag(model);

        Animator vrmAnimator = model.GetComponentInChildren<Animator>();
        Animator anim = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
        anim.runtimeAnimatorController = defaultController;

        if (vrmAnimator != null && vrmAnimator.avatar != null)
        {
            anim.avatar = vrmAnimator.avatar;
            anim.Rebind();
            Debug.Log("✅ Animator 및 Avatar 연결 완료");
        }
        else
        {
            Debug.LogWarning("⚠️ VRM 모델 내 Animator 또는 Avatar가 존재하지 않습니다.");
        }

        var proxy = model.GetComponent<VRMBlendShapeProxy>();
        var expr = root.GetComponent<ExpressionController>();
        if (proxy != null && expr != null)
        {
            expr.SetBlendShapeProxy(proxy);
            expr.SetAnimator(anim);
        }

        preset.vrmModel = model;
        
        // [핵심 추가] CharacterPreset에 생성된 VRM 루트 오브젝트와 그 컴포넌트들을 등록합니다.
        preset.SetupVRMComponents(root);
        
        SnapAwareVRM snapAware = root.GetComponent<SnapAwareVRM>();
        if (snapAware != null && vrmAnimator != null)
        {
            Transform hips = vrmAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                snapAware.targetTransform = hips;
                Debug.Log($"✅ [Preset] SnapAwareVRM의 targetTransform을 Hips로 설정했습니다.");
            }
            else
            {
                Debug.LogWarning("⚠️ [Preset] Hips 본을 찾지 못해 SnapAwareVRM targetTransform 설정에 실패했습니다.");
            }
        }

        Debug.Log($"✅ 프리셋에서 VRM 오브젝트 인스턴스화 완료: {model.name}");
        
        var autoActivate = root.GetComponent<VRMAutoActivate>();
        if (autoActivate != null)
        {
            autoActivate.SetPreset(preset);
        }
        
        return model;
    }
}