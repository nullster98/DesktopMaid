using UnityEngine;
using UnityEditor;
using TMPro;

public class EditFontChanger : EditorWindow
{
    private TMP_FontAsset newFont;

    // 메뉴에 "Tools/TMP Font Changer" 항목을 추가합니다.
    [MenuItem("Tools/TMP Font Changer")]
    public static void ShowWindow()
    {
        // 기존에 열린 창이 있다면 가져오고, 없다면 새로 만듭니다.
        GetWindow<EditFontChanger>("TMP Font Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("모든 TMP 컴포넌트의 폰트를 변경합니다.", EditorStyles.boldLabel);
        
        // 사용자가 새 폰트 에셋을 지정할 수 있는 필드를 만듭니다.
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("새 폰트 에셋", newFont, typeof(TMP_FontAsset), false);

        if (newFont == null)
        {
            EditorGUILayout.HelpBox("변경할 새 폰트 에셋을 지정해주세요.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("1. 현재 열린 씬의 모든 폰트 변경"))
        {
            ChangeFontsInCurrentScene();
        }

        if (GUILayout.Button("2. 프로젝트 내 모든 프리팹의 폰트 변경"))
        {
            ChangeFontsInAllPrefabs();
        }
    }

    private void ChangeFontsInCurrentScene()
    {
        int changedCount = 0;

        // TMP_Text 컴포넌트를 찾아서 변경합니다. (비활성화된 오브젝트 포함)
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in allTexts)
        {
            // 씬에 있는 컴포넌트인지 확인 (프리팹 에셋 자체를 건드리지 않기 위해)
            if (EditorUtility.IsPersistent(text)) continue;

            text.font = newFont;
            changedCount++;
            EditorUtility.SetDirty(text); // 변경사항을 저장하도록 표시
        }
        
        // TMP_InputField 컴포넌트를 찾아서 변경합니다.
        TMP_InputField[] allInputFields = Resources.FindObjectsOfTypeAll<TMP_InputField>();
        foreach (TMP_InputField inputField in allInputFields)
        {
            if (EditorUtility.IsPersistent(inputField)) continue;

            // InputField의 메인 텍스트 컴포넌트 폰트 변경
            if (inputField.textComponent != null)
            {
                inputField.textComponent.font = newFont;
                EditorUtility.SetDirty(inputField.textComponent);
            }
            // InputField의 Placeholder 텍스트 폰트 변경
            if (inputField.placeholder is TMP_Text placeholderText)
            {
                placeholderText.font = newFont;
                EditorUtility.SetDirty(placeholderText);
            }
            changedCount++;
        }
        
        Debug.Log($"현재 씬에서 {changedCount}개의 TMP 컴포넌트 폰트를 '{newFont.name}' (으)로 변경했습니다.");
    }

    private void ChangeFontsInAllPrefabs()
    {
        // 프로젝트의 모든 프리팹 경로를 가져옵니다.
        string[] allPrefabPaths = AssetDatabase.FindAssets("t:Prefab");
        int changedCount = 0;

        foreach (string prefabGUID in allPrefabPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGUID);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                bool changed = false;

                // 프리팹과 그 자식들에서 TMP_Text를 찾아서 변경
                foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = newFont;
                    changed = true;
                }

                // 프리팹과 그 자식들에서 TMP_InputField를 찾아서 변경
                foreach (TMP_InputField inputField in prefab.GetComponentsInChildren<TMP_InputField>(true))
                {
                    if (inputField.textComponent != null)
                    {
                        inputField.textComponent.font = newFont;
                    }
                    if (inputField.placeholder is TMP_Text placeholderText)
                    {
                        placeholderText.font = newFont;
                    }
                    changed = true;
                }

                if (changed)
                {
                    changedCount++;
                    EditorUtility.SetDirty(prefab); // 프리팹의 변경사항을 저장하도록 표시
                }
            }
        }

        AssetDatabase.SaveAssets(); // 변경된 모든 에셋을 디스크에 저장
        AssetDatabase.Refresh();    // 에셋 데이터베이스 새로고침

        Debug.Log($"프로젝트 내 {changedCount}개의 프리팹에 포함된 폰트를 '{newFont.name}' (으)로 변경했습니다.");
    }
}