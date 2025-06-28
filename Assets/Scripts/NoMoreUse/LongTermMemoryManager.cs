// // --- START OF FILE LongTermMemoryManager.cs ---
//
// using System.Collections.Generic;
// using System.IO;
// using Newtonsoft.Json;
// using UnityEngine;
//
// public static class LongTermMemoryManager
// {
//     private static string GetMemoryFilePath(string presetId)
//     {
//         // persistentDataPath는 사용자의 AppData 폴더 등을 가리키므로 안전합니다.
//         return Path.Combine(Application.persistentDataPath, $"memory_{presetId}.json");
//     }
//
//     /// <summary>
//     /// 지정된 프리셋의 장기 기억 파일에 새로운 요약문을 추가합니다.
//     /// </summary>
//     public static void AppendSummary(string presetId, string summary)
//     {
//         string filePath = GetMemoryFilePath(presetId);
//         List<string> memories = LoadMemories(presetId);
//         memories.Add(summary);
//         
//         string json = JsonConvert.SerializeObject(memories, Formatting.Indented);
//         File.WriteAllText(filePath, json);
//     }
//
//     /// <summary>
//     /// 지정된 프리셋의 모든 장기 기억(요약문)을 하나의 문자열로 합쳐서 반환합니다.
//     /// </summary>
//     public static string GetCombinedMemories(string presetId)
//     {
//         List<string> memories = LoadMemories(presetId);
//         if (memories.Count == 0) return "";
//
//         // AI가 이해하기 쉽도록 서식을 추가하여 반환
//         return "참고: 너와 나는 과거에 다음과 같은 대화를 나누고 요약했었어. 이 내용을 바탕으로 대화를 이어가 줘.\n\n--- 이전 대화 요약 ---\n- " + string.Join("\n- ", memories);
//     }
//
//     /// <summary>
//     /// 파일에서 장기 기억 리스트를 불러옵니다. 파일이 없으면 빈 리스트를 반환합니다.
//     /// </summary>
//     public static List<string> LoadMemories(string presetId)
//     {
//         string filePath = GetMemoryFilePath(presetId);
//         if (!File.Exists(filePath))
//         {
//             return new List<string>();
//         }
//         
//         string json = File.ReadAllText(filePath);
//         // JSON 파싱 실패 시 null이 반환될 수 있으므로, ?? 연산자로 빈 리스트를 보장합니다.
//         return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
//     }
//
//     /// <summary>
//     /// 지정된 프리셋의 장기 기억 파일을 삭제합니다. (캐릭터 삭제 시 등)
//     /// </summary>
//     public static void ClearMemory(string presetId)
//     {
//         string filePath = GetMemoryFilePath(presetId);
//         if (File.Exists(filePath))
//         {
//             File.Delete(filePath);
//         }
//     }
// }