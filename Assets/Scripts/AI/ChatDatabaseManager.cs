using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

/// <summary>
/// 모든 채팅 데이터베이스(1:1, 그룹) 인스턴스를 관리하는 싱글턴 클래스.
/// DB 연결, 메시지 추가/조회/삭제 등의 고수준 API를 제공하고 이벤트를 통해 시스템에 알립니다.
/// </summary>
public class ChatDatabaseManager
{
    // 정적 생성자를 사용하여 스레드-세이프한 싱글턴 인스턴스 보장
    public static ChatDatabaseManager Instance { get; } = new ChatDatabaseManager();

    // 메시지가 추가되거나 DB가 초기화될 때 발생하는 이벤트
    public static event Action<string> OnPersonalMessageAdded; 
    public static event Action<string> OnGroupMessageAdded;
    public static event Action OnAllChatDataCleared;

    // 열려있는 DB 커넥션을 캐싱하여 재사용하기 위한 딕셔너리
    // Key: "personal_{presetId}" 또는 "group_{groupId}"
    private readonly Dictionary<string, ChatDatabase> dbMap = new Dictionary<string, ChatDatabase>();

    #region Personal Chat Database API

    /// <summary>
    /// 지정된 프리셋 ID에 대한 1:1 채팅 데이터베이스 연결을 가져옵니다. 없으면 새로 생성합니다.
    /// </summary>
    /// <returns>해당 프리셋의 ChatDatabase 인스턴스. 실패 시 null을 반환할 수 있습니다.</returns>
    public ChatDatabase GetDatabase(string presetId)
    {
        string dbKey = $"personal_{presetId}";
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            db.Open(dbKey);
            dbMap[dbKey] = db;
        }
        return dbMap[dbKey];
    }

    /// <summary>
    /// 1:1 채팅에 메시지를 추가하고, 관련 시스템에 이벤트를 통해 알립니다.
    /// </summary>
    public void InsertMessage(string presetId, string senderId, string messageJson)
    {
        GetDatabase(presetId)?.InsertMessage(senderId, messageJson);
        OnPersonalMessageAdded?.Invoke(presetId);
    }

    /// <summary>
    /// 1:1 채팅의 최근 메시지를 가져옵니다.
    /// </summary>
    public List<ChatDatabase.ChatMessage> GetRecentMessages(string presetId, int count)
    {
        return GetDatabase(presetId)?.GetRecentMessages(count) ?? new List<ChatDatabase.ChatMessage>();
    }
    
    /// <summary>
    /// 1:1 채팅의 모든 메시지와 관련 기억 데이터를 삭제합니다.
    /// </summary>
    public void ClearMessages(string presetId)
    {
        GetDatabase(presetId)?.ClearAllMessages();
        
        var preset = CharacterPresetManager.Instance?.GetPreset(presetId);
        if (preset != null)
        {
            preset.longTermMemories.Clear();
            preset.knowledgeLibrary.Clear();
            preset.lastSummarizedMessageId = 0;
            preset.currentContextSummary = "";
            Debug.Log($"[DBManager] '{preset.characterName}'의 모든 대화 기록과 기억 데이터를 초기화했습니다.");
        }
        
        OnAllChatDataCleared?.Invoke(); // UI 갱신 트리거
    }

    /// <summary>
    /// 1:1 채팅의 데이터베이스 파일을 물리적으로 삭제합니다. (캐릭터 삭제 시 호출)
    /// </summary>
    public void DeleteDatabase(string presetId)
    {
        string dbKey = $"personal_{presetId}";
        CloseDatabase(dbKey); // DB 연결을 먼저 닫음
        
        string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[DBManager] 1:1 채팅 DB 파일 삭제 완료: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBManager] 1:1 채팅 DB 파일 삭제 실패: {path}\n{ex.Message}");
            }
        }
    }

    #endregion

    #region Group Chat Database API

    /// <summary>
    /// 지정된 그룹 ID에 대한 그룹 채팅 데이터베이스 연결을 가져옵니다. 없으면 새로 생성합니다.
    /// </summary>
    public ChatDatabase GetGroupDatabase(string groupId)
    {
        string dbKey = $"group_{groupId}";
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            db.Open(dbKey);
            dbMap[dbKey] = db;
        }
        return dbMap[dbKey];
    }

    /// <summary>
    /// 그룹 채팅에 메시지를 추가하고, 관련 시스템에 이벤트를 통해 알립니다.
    /// </summary>
    public void InsertGroupMessage(string groupId, string senderPresetId, string messageJson)
    {
        GetGroupDatabase(groupId)?.InsertMessage(senderPresetId, messageJson);
        OnGroupMessageAdded?.Invoke(groupId);
    }

    /// <summary>
    /// 그룹 채팅의 최근 메시지를 가져옵니다.
    /// </summary>
    public List<ChatDatabase.ChatMessage> GetRecentGroupMessages(string groupId, int count)
    {
        return GetGroupDatabase(groupId)?.GetRecentMessages(count) ?? new List<ChatDatabase.ChatMessage>();
    }
    
    /// <summary>
    /// 그룹의 대화 기록과 관련된 모든 기억 데이터를 초기화합니다.
    /// </summary>
    public void ClearGroupHistoryAndMemories(string groupId)
    {
        GetGroupDatabase(groupId)?.ClearAllMessages();

        var group = CharacterGroupManager.Instance?.GetGroup(groupId);
        if (group != null)
        {
            group.groupLongTermMemories.Clear();
            group.groupKnowledgeLibrary.Clear();
            group.lastSummarizedGroupMessageId = 0;
            group.currentContextSummary = "";
            Debug.Log($"[DBManager] '{group.groupName}' 그룹의 모든 대화 기록과 기억 데이터를 초기화했습니다.");
        }
        
        OnAllChatDataCleared?.Invoke(); // UI 갱신 트리거
    }
    
    /// <summary>
    /// 그룹 채팅의 데이터베이스 파일을 물리적으로 삭제합니다. (그룹 삭제 시 호출)
    /// </summary>
    public void DeleteGroupDatabase(string groupId)
    {
        string dbKey = $"group_{groupId}";
        CloseDatabase(dbKey);
        
        string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[DBManager] 그룹 채팅 DB 파일 삭제 완료: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBManager] 그룹 채팅 DB 파일 삭제 실패: {path}\n{ex.Message}");
            }
        }
    }

    #endregion

    #region General Management

    /// <summary>
    /// 지정된 키에 해당하는 데이터베이스 연결을 닫고 관리 맵에서 제거합니다.
    /// </summary>
    public void CloseDatabase(string dbKey)
    {
        if (dbMap.TryGetValue(dbKey, out ChatDatabase db))
        {
            db.Close();
            dbMap.Remove(dbKey);
        }
    }

    /// <summary>
    /// 현재 열려있는 모든 데이터베이스 연결을 닫습니다. (프로그램 종료 시 호출)
    /// </summary>
    public void CloseAll()
    {
        foreach (var db in dbMap.Values)
        {
            db.Close();
        }
        dbMap.Clear();
        Debug.Log("[DBManager] 모든 데이터베이스 연결을 닫았습니다.");
    }
    
    /// <summary>
    /// 모든 채팅 관련 데이터(DB 파일, 인메모리 기억)를 초기화합니다.
    /// </summary>
    public void ClearAllChatData()
    {
        Debug.LogWarning("[DBManager] 모든 채팅 데이터와 기억 초기화를 시작합니다.");
        
        CloseAll(); // 열려있는 DB 연결 모두 닫기

        // 모든 chat_*.db 파일 삭제
        try
        {
            var directory = new DirectoryInfo(Application.persistentDataPath);
            FileInfo[] filesToDelete = directory.GetFiles("chat_*.db");
            foreach (FileInfo file in filesToDelete)
            {
                file.Delete();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DBManager] DB 파일 삭제 중 오류 발생: {ex.Message}");
        }

        // 모든 캐릭터 프리셋의 인메모리 기억 초기화
        if (CharacterPresetManager.Instance?.presets != null)
        {
            foreach (var preset in CharacterPresetManager.Instance.presets)
            {
                preset.longTermMemories.Clear();
                preset.knowledgeLibrary.Clear();
                preset.lastSummarizedMessageId = 0;
            }
        }
        
        Debug.Log("[DBManager] 데이터 초기화 완료. UI 갱신을 위해 이벤트를 호출합니다.");
        OnAllChatDataCleared?.Invoke();
    }
    
    #endregion
}