using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UnusedSpriteFinder : EditorWindow
{
    private List<string> unusedSpritePaths = new List<string>();
    private Dictionary<string, bool> selectionStatus = new Dictionary<string, bool>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Unused Sprite Finder")]
    public static void ShowWindow()
    {
        GetWindow<UnusedSpriteFinder>("Unused Sprite Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("사용하지 않는 스프라이트 찾기 및 삭제", EditorStyles.boldLabel);
        
        // 경고 메시지
        EditorGUILayout.HelpBox("이 스크립트는 프리팹, 씬, 애니메이션, 스크립터블 오브젝트 등에서 직접 참조되지 않는 스프라이트를 찾습니다. " +
                                "하지만 Resources.Load, AssetBundle 또는 코드 내 문자열 경로로 로드되는 스프라이트는 '사용 중'임에도 '미사용'으로 감지될 수 있습니다. " +
                                "삭제하기 전에 목록을 반드시 주의 깊게 확인하세요.", MessageType.Warning);

        if (GUILayout.Button("1. 사용하지 않는 스프라이트 검색"))
        {
            FindAndListUnusedSprites();
        }

        EditorGUILayout.Space(10);

        if (unusedSpritePaths.Count > 0)
        {
            // 전체 선택/해제 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택")) SelectAll(true);
            if (GUILayout.Button("전체 해제")) SelectAll(false);
            EditorGUILayout.EndHorizontal();

            // 검색된 스프라이트 목록 표시
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            foreach (string path in unusedSpritePaths)
            {
                EditorGUILayout.BeginHorizontal();
                selectionStatus[path] = EditorGUILayout.Toggle(selectionStatus[path], GUILayout.Width(20));
                
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorGUILayout.ObjectField(sprite, typeof(Sprite), false);

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            
            // 선택된 항목 삭제 버튼
            int selectedCount = selectionStatus.Count(x => x.Value);
            GUI.backgroundColor = Color.red; // 위험한 작업임을 알리기 위해 버튼 색 변경
            if (GUILayout.Button($"2. 선택된 스프라이트 {selectedCount}개 삭제"))
            {
                if (EditorUtility.DisplayDialog("삭제 확인",
                    $"선택된 {selectedCount}개의 스프라이트를 프로젝트에서 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                    "삭제 실행", "취소"))
                {
                    DeleteSelectedSprites();
                }
            }
            GUI.backgroundColor = Color.white; // 버튼 색상 원래대로
        }
    }

    private void SelectAll(bool select)
    {
        var keys = new List<string>(selectionStatus.Keys);
        foreach (var key in keys)
        {
            selectionStatus[key] = select;
        }
    }

    private void FindAndListUnusedSprites()
    {
        unusedSpritePaths.Clear();
        selectionStatus.Clear();

        // 1. 프로젝트 내 모든 스프라이트의 경로를 가져옵니다.
        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite");
        List<string> allSpritePaths = spriteGuids.Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();

        // 2. 참조를 확인할 에셋들(프리팹, 씬, 스크립터블 오브젝트 등)의 경로를 가져옵니다.
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        string[] dependencyCheckTargetPaths = allAssetPaths.Where(path =>
            path.EndsWith(".prefab") ||
            path.EndsWith(".unity") ||
            path.EndsWith(".asset") ||
            path.EndsWith(".anim")
        ).ToArray();

        // 3. 모든 에셋들의 종속성 목록을 한 번에 가져옵니다. (성능 향상)
        string[] allDependencies = AssetDatabase.GetDependencies(dependencyCheckTargetPaths, true);
        HashSet<string> dependencySet = new HashSet<string>(allDependencies);

        // 4. 각 스프라이트가 종속성 목록에 있는지 확인합니다.
        for (int i = 0; i < allSpritePaths.Count; i++)
        {
            string spritePath = allSpritePaths[i];
            EditorUtility.DisplayProgressBar("스프라이트 검사 중", $"{i + 1}/{allSpritePaths.Count}", (float)i / allSpritePaths.Count);
            
            if (!dependencySet.Contains(spritePath))
            {
                unusedSpritePaths.Add(spritePath);
                selectionStatus[spritePath] = false; // 기본적으로 선택 해제 상태로 추가
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"총 {unusedSpritePaths.Count}개의 사용하지 않는 스프라이트를 찾았습니다.");
    }

    private void DeleteSelectedSprites()
    {
        List<string> pathsToDelete = selectionStatus.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
        
        if (pathsToDelete.Count == 0)
        {
            Debug.LogWarning("삭제할 스프라이트가 선택되지 않았습니다.");
            return;
        }

        foreach (string path in pathsToDelete)
        {
            AssetDatabase.DeleteAsset(path);
        }

        Debug.Log($"{pathsToDelete.Count}개의 스프라이트를 성공적으로 삭제했습니다.");
        
        // 삭제 후 목록 새로고침
        FindAndListUnusedSprites();
    }
}