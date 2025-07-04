// --- START OF FILE ChatDatabaseManager.cs ---

using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

/// <summary>
/// 모든 채팅 데이터베이스(1:1 채팅, 그룹 채팅)를 관리하는 싱글턴 클래스.
/// 각 채팅 대상(presetId 또는 groupId)에 대한 DB 연결을 관리하고,
/// 메시지 추가, 조회, 삭제 등의 기능을 제공합니다.
/// </summary>
public class ChatDatabaseManager
{
    public static ChatDatabaseManager Instance { get; private set; } = new();

    // 열려있는 DB 커넥션을 관리하는 딕셔너리.
    // Key: presetId 또는 "group_{groupId}"
    // Value: ChatDatabase 인스턴스
    private Dictionary<string, ChatDatabase> dbMap = new();
    
    /// <summary>
    /// 그룹 채팅에 새로운 메시지가 추가될 때 발생하는 이벤트입니다.
    /// string: 메시지가 추가된 그룹의 ID
    /// </summary>
    public static event Action<string> OnGroupMessageAdded;
    public static event Action<string> OnPersonalMessageAdded; 

    #region --- 1:1 채팅 데이터베이스 관리 ---

    /// <summary>
    /// 지정된 프리셋 ID에 대한 1:1 채팅 데이터베이스 연결을 가져옵니다.
    /// 연결이 없으면 새로 생성합니다.
    /// </summary>
    /// <param name="presetId">캐릭터 프리셋의 고유 ID</param>
    /// <returns>해당 프리셋의 ChatDatabase 인스턴스</returns>
    public ChatDatabase GetDatabase(string presetId)
    {
        string dbKey = $"personal_{presetId}";
    
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            db.Open(dbKey);

            try
            {
                // 연결 유효성 검사를 위해 메시지 하나라도 읽어봄
                var testMessages = db.GetRecentMessages(1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatDatabaseManager] '{presetId}'의 DB 연결 실패: {ex.Message}");
                db.Close(); // 연결 끊기
                return null;
            }

            dbMap[dbKey] = db;
        }
        
        return dbMap[dbKey];
    }

    /// <summary>
    /// 1:1 채팅에 메시지를 추가합니다.
    /// </summary>
    /// <param name="presetId">메시지를 추가할 채팅의 프리셋 ID</param>
    /// <param name="sender">메시지를 보낸 사람 (e.g., "user", "gemini")</param>
    /// <param name="messageJson">메시지 내용 (JSON 형식)</param>
    public void InsertMessage(string presetId, string sender, string messageJson)
    {
        GetDatabase(presetId).InsertMessage(sender, messageJson);
        OnPersonalMessageAdded?.Invoke(presetId);
    }

    /// <summary>
    /// 1:1 채팅의 최근 메시지를 가져옵니다.
    /// </summary>
    /// <param name="presetId">메시지를 가져올 채팅의 프리셋 ID</param>
    /// <param name="count">가져올 메시지 개수</param>
    /// <returns>최근 메시지 목록</returns>
    public List<ChatDatabase.ChatMessage> GetRecentMessages(string presetId, int count)
    {
        return GetDatabase(presetId).GetRecentMessages(count);
    }
    /// <summary>
    /// 1:1 채팅의 모든 메시지를 삭제하고, 관련된 장기 기억도 삭제합니다.
    /// </summary>
    /// <param name="presetId">초기화할 채팅의 프리셋 ID</param>
    public void ClearMessages(string presetId)
    {
        GetDatabase(presetId).ClearAllMessages();
        var preset = CharacterPresetManager.Instance?.presets.FirstOrDefault(p => p.presetID == presetId);
        if (preset != null)
        {
            preset.longTermMemories.Clear();
            preset.knowledgeLibrary.Clear();
            preset.lastSummarizedMessageId = 0;
            Debug.Log($"[ChatDatabaseManager] '{preset.characterName}'의 모든 기억 데이터를 초기화했습니다.");
        }
    }

    /// <summary>
    /// 1:1 채팅의 데이터베이스 파일을 삭제합니다. 캐릭터 프리셋이 삭제될 때 호출됩니다.
    /// </summary>
    /// <param name="presetId">삭제할 DB의 프리셋 ID</param>
    public void DeleteDatabase(string presetId)
    {
        CloseDatabase(presetId); // DB 연결을 먼저 닫음
        string path = Path.Combine(Application.persistentDataPath, $"chat_{presetId}.db");
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"✅ 1:1 채팅 DB 파일 삭제 완료: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 1:1 채팅 DB 파일 삭제 실패: {path}\n{ex.Message}");
            }
        }
    }

    #endregion

    #region --- 그룹 채팅 데이터베이스 관리 ---

    /// <summary>
    /// 지정된 그룹 ID에 대한 그룹 채팅 데이터베이스 연결을 가져옵니다.
    /// 연결이 없으면 새로 생성합니다.
    /// </summary>
    /// <param name="groupId">캐릭터 그룹의 고유 ID</param>
    /// <returns>해당 그룹의 ChatDatabase 인스턴스</returns>
    public ChatDatabase GetGroupDatabase(string groupId)
    {
        // 그룹 DB는 "group_" 접두사를 붙여 1:1 채팅과 구분합니다.
        string dbKey = $"group_{groupId}";
        if (!dbMap.ContainsKey(dbKey))
        {
            var db = new ChatDatabase();
            // 최종적으로 "chat_group_... .db" 파일이 생성됩니다.
            db.Open(dbKey);
            dbMap[dbKey] = db;
        }
        return dbMap[dbKey];
    }

    /// <summary>
    /// 그룹 채팅에 메시지를 추가합니다.
    /// </summary>
    /// <param name="groupId">메시지를 추가할 채팅의 그룹 ID</param>
    /// <param name="senderPresetId">메시지를 보낸 멤버의 프리셋 ID (사용자는 "user")</param>
    /// <param name="messageJson">메시지 내용 (JSON 형식)</param>
    public void InsertGroupMessage(string groupId, string senderPresetId, string messageJson)
    {
        GetGroupDatabase(groupId).InsertMessage(senderPresetId, messageJson);
        
        OnGroupMessageAdded?.Invoke(groupId);
    }

    /// <summary>
    /// 그룹 채팅의 최근 메시지를 가져옵니다.
    /// </summary>
    /// <param name="groupId">메시지를 가져올 채팅의 그룹 ID</param>
    /// <param name="count">가져올 메시지 개수</param>
    /// <returns>최근 메시지 목록</returns>
    public List<ChatDatabase.ChatMessage> GetRecentGroupMessages(string groupId, int count)
    {
        return GetGroupDatabase(groupId).GetRecentMessages(count);
    }

    /// <summary>
    /// 그룹 채팅의 모든 메시지를 삭제합니다.
    /// </summary>
    /// <param name="groupId">초기화할 채팅의 그룹 ID</param>
    public void ClearGroupMessages(string groupId)
    {
        GetGroupDatabase(groupId).ClearAllMessages();
    }

    /// <summary>
    /// 그룹 채팅의 데이터베이스 파일을 삭제합니다. 그룹이 삭제될 때 호출됩니다.
    /// </summary>
    /// <param name="groupId">삭제할 DB의 그룹 ID</param>
    public void DeleteGroupDatabase(string groupId)
    {
        string dbKey = $"group_{groupId}";
        CloseDatabase(dbKey); // DB 연결을 먼저 닫음
        string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"✅ 그룹 채팅 DB 파일 삭제 완료: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 그룹 채팅 DB 파일 삭제 실패: {path}\n{ex.Message}");
            }
        }
    }

    #endregion

    #region --- 공용 관리 함수 ---

    /// <summary>
    /// 지정된 키에 해당하는 데이터베이스 연결을 닫고 맵에서 제거합니다.
    /// </summary>
    /// <param name="dbKey">닫을 DB의 키 (presetId 또는 "group_{groupId}")</param>
    public void CloseDatabase(string dbKey)
    {
        if (dbMap.ContainsKey(dbKey))
        {
            dbMap[dbKey].Close();
            dbMap.Remove(dbKey);
        }
    }

    /// <summary>
    /// 현재 열려있는 모든 데이터베이스 연결을 닫습니다.
    /// 프로그램 종료 시 호출될 수 있습니다.
    /// </summary>
    public void CloseAll()
    {
        foreach (var db in dbMap.Values)
        {
            db.Close();
        }
        dbMap.Clear();
    }

    /// <summary>
    /// (요약 시스템용) 1:1 채팅의 모든 메시지 객체를 반환합니다.
    /// </summary>
    public List<ChatDatabase.ChatMessage> GetAllMessagesForSummary(string presetId)
    {
        var db = GetDatabase(presetId);
        var messages = db.GetAllMessages();
        // ... (이하 기존 코드와 동일)
        foreach (var msg in messages)
        {
            try
            {
                MessageData data = JsonUtility.FromJson<MessageData>(msg.Message);
                string combinedMessage = data.textContent ?? "";
                if (data.type == "image") { combinedMessage += " (이미지)"; }
                else if (data.type == "text" && data.fileSize > 0) { combinedMessage += $" ({data.fileName})"; }
                msg.Message = combinedMessage.Trim();
            }
            catch { /* 이전 버전 호환성 또는 오류 데이터는 원본 유지 */ }
        }
        return messages;
    }
    
    // 메시지 데이터 구조체 (내부에서만 사용)
    [System.Serializable]
    public class MessageData
    {
        public string type;
        public string textContent;
        public string fileContent;
        public string fileName;
        public long fileSize;
    }

    #endregion
}
// --- END OF FILE ChatDatabaseManager.cs ---