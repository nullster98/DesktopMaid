using UnityEditor;
using UnityEngine;
using System.IO;

public class TaskbarFixerDirect
{
    [MenuItem("Tools/Force Hide From Taskbar (Edit Asset File)")]
    public static void ForceHideTaskbar()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");

        if (!File.Exists(path))
        {
            Debug.LogError("❌ ProjectSettings.asset 파일을 찾을 수 없습니다.");
            return;
        }

        string content = File.ReadAllText(path);

        if (content.Contains("showInTaskbar:"))
        {
            // 값이 있으면 0으로 강제 수정
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"showInTaskbar:\s*[01]",
                "showInTaskbar: 0");
            Debug.Log("✅ 기존 설정을 '작업표시줄 숨김'으로 수정했습니다.");
        }
        else
        {
            // 없으면 맨 아래에 추가
            content += "\nshowInTaskbar: 0\n";
            Debug.Log("✅ 설정이 없어서 직접 추가했습니다.");
        }

        File.WriteAllText(path, content);
        AssetDatabase.Refresh();

        Debug.Log("📦 ProjectSettings.asset 저장 완료 — Unity 재시작 후 적용됩니다.");
    }
}