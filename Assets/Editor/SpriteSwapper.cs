using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class SpriteSwapper : EditorWindow
{
    private Sprite oldSprite;
    private Sprite newSprite;

    [MenuItem("Tools/Sprite Swapper")]
    public static void ShowWindow()
    {
        GetWindow<SpriteSwapper>("Sprite Swapper");
    }

    private void OnGUI()
    {
        GUILayout.Label("스프라이트 교체 도구", EditorStyles.boldLabel);
        
        oldSprite = (Sprite)EditorGUILayout.ObjectField("Old Sprite (교체될 스프라이트)", oldSprite, typeof(Sprite), false);
        newSprite = (Sprite)EditorGUILayout.ObjectField("New Sprite (새 스프라이트)", newSprite, typeof(Sprite), false);

        if (GUILayout.Button("Swap Sprites in Project (프로젝트 전체에서 교체)"))
        {
            if (oldSprite == null || newSprite == null)
            {
                EditorUtility.DisplayDialog("오류", "두 스프라이트를 모두 지정해주세요.", "확인");
                return;
            }
            
            if (oldSprite == newSprite)
            {
                EditorUtility.DisplayDialog("오류", "같은 스프라이트로는 교체할 수 없습니다.", "확인");
                return;
            }

            SwapSprites();
        }
    }

    private void SwapSprites()
    {
        int swappedCount = 0;

        // 1. 프로젝트 내 모든 프리팹 검색 및 교체
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                // SpriteRenderer 교체
                SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer renderer in renderers)
                {
                    if (renderer.sprite == oldSprite)
                    {
                        renderer.sprite = newSprite;
                        EditorUtility.SetDirty(prefab); // 변경사항 저장
                        swappedCount++;
                    }
                }

                // UI Image 교체
                Image[] images = prefab.GetComponentsInChildren<Image>(true);
                foreach (Image image in images)
                {
                    if (image.sprite == oldSprite)
                    {
                        image.sprite = newSprite;
                        EditorUtility.SetDirty(prefab); // 변경사항 저장
                        swappedCount++;
                    }
                }
            }
        }
        
        // 변경된 프리팹 저장
        AssetDatabase.SaveAssets();

        // 2. 현재 열려있는 씬들 검색 및 교체
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    // SpriteRenderer 교체
                    SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (SpriteRenderer renderer in renderers)
                    {
                        if (renderer.sprite == oldSprite)
                        {
                            Undo.RecordObject(renderer, "Swap Sprite"); // Undo 기능 추가
                            renderer.sprite = newSprite;
                            swappedCount++;
                        }
                    }

                    // UI Image 교체
                    Image[] images = root.GetComponentsInChildren<Image>(true);
                    foreach (Image image in images)
                    {
                        if (image.sprite == oldSprite)
                        {
                            Undo.RecordObject(image, "Swap UI Sprite"); // Undo 기능 추가
                            image.sprite = newSprite;
                            swappedCount++;
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); // 씬이 변경되었음을 표시
            }
        }
        
        Debug.Log($"총 {swappedCount}개의 스프라이트를 '{oldSprite.name}'에서 '{newSprite.name}'으로 교체했습니다.");
        EditorUtility.DisplayDialog("완료", $"총 {swappedCount}개의 스프라이트를 교체했습니다.", "확인");
    }
}