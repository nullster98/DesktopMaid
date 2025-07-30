// --- START OF FILE ChatDatabaseManager.cs ---

using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public class ChatDatabaseManager
{
    public static ChatDatabaseManager Instance { get; private set; } = new();

    public static event Action OnAllChatDataCleared; // 전체 삭제 시에만 사용
    
    // [해결] 특정 채팅방만 리셋하기 위한 새 이벤트. string: ownerId (presetId 또는 groupId)
    public static event Action<string> OnChatHistoryCleared; 
    
    public static event Action<string, bool> OnGroupMessageAdded;
    public static event Action<string, bool> OnPersonalMessageAdded; 

    private Dictionary<string, ChatDatabase> dbMap = new();

    #region --- 1:1 채팅 데이터베이스 관리 ---

    public ChatDatabase GetDatabase(string presetId)
    {
        string dbKey = $"personal_{presetId}";
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            db.Open(dbKey);
            if(db == null) return null;
            dbMap[dbKey] = db;
        }
        return dbMap[dbKey];
    }

    public void InsertMessage(string presetId, string sender, string messageJson)
    {
        GetDatabase(presetId)?.InsertMessage(sender, messageJson);
        OnPersonalMessageAdded?.Invoke(presetId, sender == "user");
    }

    public List<ChatDatabase.ChatMessage> GetRecentMessages(string presetId, int count)
    {
        return GetDatabase(presetId)?.GetRecentMessages(count) ?? new List<ChatDatabase.ChatMessage>();
    }
    
    public void ClearMessages(string presetId)
    {
        GetDatabase(presetId)?.ClearAllMessages();
        var preset = CharacterPresetManager.Instance?.GetPreset(presetId);
        if (preset != null)
        {
            preset.longTermMemories.Clear();
            preset.knowledgeLibrary.Clear();
            preset.lastSummarizedMessageId = 0;
            Debug.Log($"[ChatDatabaseManager] '{preset.characterName}'의 모든 기억 데이터를 초기화했습니다.");
        }
        
        // [해결] 전역 이벤트 대신 특정 ID를 전달하는 이벤트를 호출합니다.
        OnChatHistoryCleared?.Invoke(presetId);
    }

    public void DeleteDatabase(string presetId)
    {
        string dbKey = $"personal_{presetId}";
        CloseDatabase(dbKey);
        string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch (Exception e) { Debug.LogError(e); }
        }
    }

    #endregion

    #region --- 그룹 채팅 데이터베이스 관리 ---

    public ChatDatabase GetGroupDatabase(string groupId)
    {
        string dbKey = $"group_{groupId}";
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            db.Open(dbKey);
            if(db == null) return null;
            dbMap[dbKey] = db;
        }
        return dbMap[dbKey];
    }

    public void InsertGroupMessage(string groupId, string senderPresetId, string messageJson)
    {
        GetGroupDatabase(groupId)?.InsertMessage(senderPresetId, messageJson);
        OnGroupMessageAdded?.Invoke(groupId, senderPresetId == "user");
    }

    public List<ChatDatabase.ChatMessage> GetRecentGroupMessages(string groupId, int count)
    {
        return GetGroupDatabase(groupId)?.GetRecentMessages(count) ?? new List<ChatDatabase.ChatMessage>();
    }

    public void DeleteGroupDatabase(string groupId)
    {
        string dbKey = $"group_{groupId}";
        CloseDatabase(dbKey);
        string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch (Exception e) { Debug.LogError(e); }
        }
    }
    
    public void ClearGroupHistoryAndMemories(string groupId)
    {
        var group = CharacterGroupManager.Instance?.GetGroup(groupId);
        if (group != null)
        {
            group.groupLongTermMemories.Clear();
            group.groupKnowledgeLibrary.Clear();
            group.lastSummarizedGroupMessageId = 0;
            group.currentContextSummary = "";
            Debug.Log($"[ChatDatabaseManager] '{group.groupName}' 그룹의 모든 기억 데이터를 초기화했습니다.");
        }
        GetGroupDatabase(groupId)?.ClearAllMessages();
        
        // [해결] 전역 이벤트 대신 특정 ID를 전달하는 이벤트를 호출합니다.
        OnChatHistoryCleared?.Invoke(groupId);
    }
    
    public void ClearAllChatData()
    {
        Debug.LogWarning("[ChatDatabaseManager] 모든 채팅 데이터와 기억 초기화를 시작합니다.");
        
        CloseAll();

        try
        {
            DirectoryInfo directory = new DirectoryInfo(Application.persistentDataPath);
            FileInfo[] filesToDelete = directory.GetFiles("chat_*.db");
            
            foreach (FileInfo file in filesToDelete)
            {
                file.Delete();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatDatabaseManager] DB 파일 삭제 중 오류 발생: {ex.Message}");
        }

        if (CharacterPresetManager.Instance != null && CharacterPresetManager.Instance.presets != null)
        {
            foreach (var preset in CharacterPresetManager.Instance.presets)
            {
                preset.longTermMemories.Clear();
                preset.knowledgeLibrary.Clear();
                preset.lastSummarizedMessageId = 0;
            }
        }

        if (CharacterGroupManager.Instance != null && CharacterGroupManager.Instance.allGroups != null)
        {
            foreach(var group in CharacterGroupManager.Instance.allGroups)
            {
                group.groupLongTermMemories.Clear();
                group.groupKnowledgeLibrary.Clear();
                group.lastSummarizedGroupMessageId = 0;
            }
        }
        
        Debug.Log("[ChatDatabaseManager] 데이터 초기화 완료. OnAllChatDataCleared 이벤트를 호출합니다.");
        OnAllChatDataCleared?.Invoke();
    }

    #endregion

    #region --- 공용 관리 함수 ---

    public void CloseDatabase(string dbKey)
    {
        if (dbMap.TryGetValue(dbKey, out ChatDatabase db))
        {
            db.Close();
            dbMap.Remove(dbKey);
        }
    }

    public void CloseAll()
    {
        foreach (var db in dbMap.Values) db.Close();
        dbMap.Clear();
    }

    #endregion
}
// --- END OF FILE ChatDatabaseManager.cs ---