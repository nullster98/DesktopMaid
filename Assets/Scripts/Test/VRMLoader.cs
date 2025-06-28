// using System.IO;
// using System.Threading.Tasks;
// using UnityEngine;
// using UnityEngine.UI;
// using VRM;
// using UniGLTF;
// using SFB;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using UniVRM10;
// using System;
//
// #if UNITY_EDITOR
// using UnityEditor;
// #endif
//
// public class VRMLoader : MonoBehaviour
// {
//     public Button loadVRMButton;
//     public GameObject mainModel;
//     public GameObject customModelOutput;
//     public RuntimeAnimatorController animatorController;
//     public GameObject componentTemplatePrefab;
//
//     private GameObject currentModel;
//     private bool isLoading = false;
//     private string modelPathKey = "SavedPathModel";
//
//     void Start()
//     {
//         if (PlayerPrefs.HasKey(modelPathKey))
//         {
//             string savedPath = PlayerPrefs.GetString(modelPathKey);
//             if (!string.IsNullOrEmpty(savedPath))
//                 LoadVRM(savedPath);
//         }
//     }
//
//     public void OpenFileDialogAndLoadVRM()
//     {
//         if (isLoading) return;
//
//         isLoading = true;
//         var extensions = new[] { new ExtensionFilter("Model Files", "vrm", "me", "prefab") };
//         string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Model File", "", extensions, false);
//         if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
//             LoadVRM(paths[0]);
//
//         isLoading = false;
//     }
//
//     public async void LoadVRM(string path)
//     {
//         if (path.EndsWith(".me", StringComparison.OrdinalIgnoreCase))
//         {
//             LoadAssetBundleModel(path);
//             PlayerPrefs.SetString(modelPathKey, path);
//             PlayerPrefs.Save();
//             return;
//         }
//         if (IsDLCReference(path))
//         {
//             GameObject prefab = FindDLCByName(path);
//             if (prefab != null)
//             {
//                 GameObject instance = Instantiate(prefab);
//                 FinalizeLoadedModel(instance, path);
//                 PlayerPrefs.SetString(modelPathKey, path);
//                 PlayerPrefs.Save();
//             }
//             else
//             {
//                 Debug.LogError("[VRMLoader] DLC Prefab nicht gefunden: " + path);
//             }
//             return;
//         }
//
//
//         if (!File.Exists(path)) return;
//
//         try
//         {
//             if (path.EndsWith(".me", StringComparison.OrdinalIgnoreCase))
//             {
//                 LoadAssetBundleModel(path);
//                 return;
//             }
//
//             byte[] fileData = await Task.Run(() => File.ReadAllBytes(path));
//             if (fileData == null || fileData.Length == 0) return;
//
//             GameObject loadedModel = null;
//             try
//             {
//                 var glbData = new GlbFileParser(path).Parse();
//                 var vrm10Data = Vrm10Data.Parse(glbData);
//
//                 if (vrm10Data != null)
//                 {
//                     using var importer10 = new Vrm10Importer(vrm10Data);
//                     var instance10 = await importer10.LoadAsync(new ImmediateCaller());
//                     if (instance10.Root != null)
//                         loadedModel = instance10.Root;
//                 }
//             }
//             catch { }
//
//             if (loadedModel == null)
//             {
//                 try
//                 {
//                     using var gltfData = new GlbBinaryParser(fileData, path).Parse();
//                     var importer = new VRMImporterContext(new VRMData(gltfData));
//                     var instance = await importer.LoadAsync(new ImmediateCaller());
//                     if (instance.Root != null)
//                         loadedModel = instance.Root;
//                 }
//                 catch { return; }
//             }
//
//             if (loadedModel == null) return;
//
//             FinalizeLoadedModel(loadedModel, path);
//             PlayerPrefs.SetString(modelPathKey, path);
//             PlayerPrefs.Save();
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError("[VRMLoader] Failed to load model: " + ex.Message);
//         }
//     }
//
//     private void LoadAssetBundleModel(string path)
//     {
//         var bundle = AssetBundle.LoadFromFile(path);
//         if (bundle == null)
//         {
//             Debug.LogError("[VRMLoader] Failed to load AssetBundle at: " + path);
//             return;
//         }
//
//         var prefab = bundle.LoadAllAssets<GameObject>().FirstOrDefault();
//         if (prefab == null)
//         {
//             Debug.LogError("[VRMLoader] No prefab found in AssetBundle.");
//             bundle.Unload(false);
//             return;
//         }
//
//         var instance = Instantiate(prefab);
//         bundle.Unload(false);
//         FinalizeLoadedModel(instance, path);
//     }
//
//     private void FinalizeLoadedModel(GameObject loadedModel, string path)
//     {
//         DisableMainModel();
//         ClearPreviousCustomModel();
//
//         loadedModel.transform.SetParent(customModelOutput.transform, false);
//         loadedModel.transform.localPosition = Vector3.zero;
//         loadedModel.transform.localRotation = Quaternion.identity;
//         loadedModel.transform.localScale = Vector3.one;
//         currentModel = loadedModel;
//
//         EnableSkinnedMeshRenderers(currentModel);
//         AssignAnimatorController(currentModel);
//         InjectComponentsFromPrefab(componentTemplatePrefab, currentModel);
//
//         /*
//         var avatarSettingsMenu = FindFirstObjectByType<AvatarSettingsMenu>();
//         if (avatarSettingsMenu != null)
//         {
//             avatarSettingsMenu.LoadSettings();
//             avatarSettingsMenu.ApplySettings();
//         }
//         */
//
//         string displayName = Path.GetFileNameWithoutExtension(path);
//         string author = "Unknown";
//         string version = "Unknown";
//         string fileType = "Unknown";
//         Texture2D thumbnail = null;
//         bool isME = path.EndsWith(".me", StringComparison.OrdinalIgnoreCase);
//
//         var vrm10Instance = loadedModel.GetComponent<UniVRM10.Vrm10Instance>();
//         if (vrm10Instance != null && vrm10Instance.Vrm != null && vrm10Instance.Vrm.Meta != null)
//         {
//             displayName = vrm10Instance.Vrm.Meta.Name ?? displayName;
//             author = (vrm10Instance.Vrm.Meta.Authors != null && vrm10Instance.Vrm.Meta.Authors.Count > 0) ? vrm10Instance.Vrm.Meta.Authors[0] : "Unknown";
//             version = vrm10Instance.Vrm.Meta.Version ?? "Unknown";
//             fileType = isME ? ".ME (VRM1.X)" : "VRM1.X";
//             thumbnail = vrm10Instance.Vrm.Meta.Thumbnail;
//         }
//         else
//         {
//             var vrmMeta = loadedModel.GetComponent<VRM.VRMMeta>();
//             if (vrmMeta != null && vrmMeta.Meta != null)
//             {
//                 var meta = vrmMeta.Meta;
//                 displayName = !string.IsNullOrEmpty(meta.Title) ? meta.Title : displayName;
//                 author = !string.IsNullOrEmpty(meta.Author) ? meta.Author : "Unknown";
//                 version = !string.IsNullOrEmpty(meta.Version) ? meta.Version : "Unknown";
//                 fileType = isME ? ".ME (VRM0.X)" : "VRM0.X";
//                 thumbnail = meta.Thumbnail;
//             }
//
//         }
//
//         Texture2D safeThumbnail = MakeReadableCopy(thumbnail);
//         int polyCount = GetTotalPolygons(loadedModel);
//
//         // VRMs/ME-Files zur Liste hinzufügen, aber NIE DLC/Prefabs!
//         if (!IsDLCReference(path))
//         {
//             AvatarLibraryMenu.AddAvatarToLibrary(displayName, author, version, fileType, path, safeThumbnail, polyCount);
//         }
//
//         if (safeThumbnail != null) Destroy(safeThumbnail);
//
//         var libraryMenu = FindFirstObjectByType<AvatarLibraryMenu>();
//         if (libraryMenu != null)
//         {
//             libraryMenu.ReloadAvatars();
//         }
//
//         StartCoroutine(DelayedRefreshStats());
//
//         if (MEModLoader.Instance != null)
//             MEModLoader.Instance.AssignHandlersForCurrentAvatar(loadedModel);
//
//         ReleaseRamAndUnloadAssets();
//         SettingsHandlerUtility.ReloadAllSettingsHandlers(); // Should load the settings from all modules and load it to new vrm files
//     }
//
//
//     public Texture2D MakeReadableCopy(Texture texture)
//     {
//         if (texture == null) return null;
//         RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
//         Graphics.Blit(texture, rt);
//         RenderTexture previous = RenderTexture.active;
//         RenderTexture.active = rt;
//         Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
//         readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
//         readable.Apply();
//         RenderTexture.active = previous;
//         RenderTexture.ReleaseTemporary(rt);
//         return readable;
//     }
//
//     public void ResetModel()
//     {
//         string vrmFolder = Path.Combine(Application.persistentDataPath, "VRM");
//         if (Directory.Exists(vrmFolder))
//             Directory.Delete(vrmFolder, true);
//
//         ClearPreviousCustomModel(skipRawImageCleanup: true);
//         EnableMainModel();
//         PlayerPrefs.DeleteKey(modelPathKey);
//         PlayerPrefs.Save();
//
//         if (MEModLoader.Instance != null && mainModel != null)
//             MEModLoader.Instance.AssignHandlersForCurrentAvatar(mainModel);
//         ReleaseRamAndUnloadAssets();
//     }
//
//     private void DisableMainModel()
//     {
//         if (mainModel != null)
//             mainModel.SetActive(false);
//     }
//
//     private void EnableMainModel()
//     {
//         if (mainModel != null)
//             mainModel.SetActive(true);
//     }
//     private void ClearPreviousCustomModel(bool skipRawImageCleanup = false)
//     {
//         if (customModelOutput != null)
//         {
//             foreach (Transform child in customModelOutput.transform)
//             {
//                 if (child.gameObject == mainModel)
//                     continue;
//                 CleanupMaterialsAndTextures(child.gameObject);
//                 CleanupRawImages(child.gameObject);
//                 Destroy(child.gameObject);
//             }
//         }
//
//         if (!skipRawImageCleanup)
//             CleanupAllRawImagesInScene();
//     }
//
//     private void EnableSkinnedMeshRenderers(GameObject model)
//     {
//         foreach (var skinnedMesh in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
//             skinnedMesh.enabled = true;
//     }
//
//     private void AssignAnimatorController(GameObject model)
//     {
//         var animator = model.GetComponentInChildren<Animator>();
//         if (animator != null && animatorController != null)
//             animator.runtimeAnimatorController = animatorController;
//     }
//
//     private void InjectComponentsFromPrefab(GameObject prefabTemplate, GameObject targetModel)
//     {
//         if (prefabTemplate == null || targetModel == null) return;
//
//         var templateObj = Instantiate(prefabTemplate);
//         var animator = targetModel.GetComponentInChildren<Animator>();
//
//         foreach (var templateComp in templateObj.GetComponents<MonoBehaviour>())
//         {
//             var type = templateComp.GetType();
//             if (targetModel.GetComponent(type) != null)
//                 continue;
//             var newComp = targetModel.AddComponent(type);
//             CopyComponentValues(templateComp, newComp);
//
//             if (animator != null)
//             {
//                 var setAnimMethod = type.GetMethod("SetAnimator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//                 if (setAnimMethod != null)
//                     setAnimMethod.Invoke(newComp, new object[] { animator });
//
//                 var animatorField = type.GetField("animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//                 if (animatorField != null && animatorField.FieldType == typeof(Animator))
//                     animatorField.SetValue(newComp, animator);
//             }
//         }
//         Destroy(templateObj);
//     }
//
//     private void CopyComponentValues(Component source, Component destination)
//     {
//         var type = source.GetType();
//         var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//         foreach (var field in fields)
//         {
//             if (field.IsDefined(typeof(SerializeField), true) || field.IsPublic)
//                 field.SetValue(destination, field.GetValue(source));
//         }
//         var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
//                         .Where(p => p.CanWrite && p.GetSetMethod(true) != null);
//         foreach (var prop in props)
//         {
//             try { prop.SetValue(destination, prop.GetValue(source)); }
//             catch { }
//         }
//     }
//
//     private System.Collections.IEnumerator DelayedRefreshStats()
//     {
//         yield return null;
//         var stats = FindFirstObjectByType<RuntimeModelStats>();
//         if (stats != null)
//             stats.RefreshNow();
//     }
//
//     public int GetTotalPolygons(GameObject model)
//     {
//         int total = 0;
//         foreach (var meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
//         {
//             var mesh = meshFilter.sharedMesh;
//             if (mesh != null)
//                 total += mesh.triangles.Length / 3;
//         }
//         foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
//         {
//             var mesh = skinned.sharedMesh;
//             if (mesh != null)
//                 total += mesh.triangles.Length / 3;
//         }
//         return total;
//     }
//
//     public void ActivateDefaultModel()
//     {
//         ClearPreviousCustomModel(skipRawImageCleanup: true);
//         EnableMainModel();
//         PlayerPrefs.DeleteKey(modelPathKey);
//         PlayerPrefs.Save();
//
//         if (MEModLoader.Instance != null && mainModel != null)
//             MEModLoader.Instance.AssignHandlersForCurrentAvatar(mainModel);
//         /*
//         var avatarSettingsMenu = FindFirstObjectByType<AvatarSettingsMenu>();
//         if (avatarSettingsMenu != null)
//         {
//             avatarSettingsMenu.LoadSettings();
//             avatarSettingsMenu.ApplySettings();
//         }
//         */
//         ReleaseRamAndUnloadAssets();
//         SettingsHandlerUtility.ReloadAllSettingsHandlers(); // Should load all settings to new VRM Files
//
//     }
//
//     private void ReleaseRamAndUnloadAssets()
//     {
//         Resources.UnloadUnusedAssets();
//         System.GC.Collect();
//     }
//
//     private void CleanupMaterialsAndTextures(GameObject obj)
//     {
//         if (obj == null) return;
//         foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
//         {
//             if (renderer.materials != null)
//             {
//                 foreach (var mat in renderer.materials)
//                 {
//                     if (mat == null) continue;
//                     if (mat.mainTexture != null)
//                         mat.mainTexture = null;
//                 }
//             }
//         }
//     }
//
//     private void CleanupRawImages(GameObject obj)
//     {
//         if (obj == null) return;
//         var rawImages = obj.GetComponentsInChildren<RawImage>(true);
//         foreach (var rawImage in rawImages)
//         {
//             rawImage.texture = null;
//         }
//     }
//
//     private void CleanupAllRawImagesInScene()
//     {
//         var rawImages = GameObject.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//         foreach (var rawImage in rawImages)
//         {
//             rawImage.texture = null;
//         }
//     }
//
//     private bool IsDLCReference(string path)
//     {
// #if UNITY_EDITOR
//         if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
//             return true;
// #endif
//         if (!File.Exists(path) && !path.EndsWith(".vrm") && !path.EndsWith(".me"))
//             return true;
//         return false;
//     }
//
//     private GameObject FindDLCByName(string name)
//     {
//         var library = FindFirstObjectByType<AvatarLibraryMenu>();
//         if (library == null) return null;
//         foreach (var dlc in library.dlcAvatars)
//         {
// #if UNITY_EDITOR
//             string assetPath = AssetDatabase.GetAssetPath(dlc.prefab);
//             if (assetPath == name) return dlc.prefab;
// #endif
//             if (dlc.prefab != null && dlc.prefab.name == name) return dlc.prefab;
//         }
//         return null;
//     }
// }