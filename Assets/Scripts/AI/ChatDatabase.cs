// --- START OF FILE ChatDatabase.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using SQLite4Unity3d;

public class ChatDatabase
{
    private SQLiteConnection db;
    private const int MaxMessageCount = 500;

    public void Open(string dbKey)
    {
        try
        {
            if (db != null) Close();

            // [수정] DB Key를 그대로 파일명에 사용하여 일관성을 유지합니다.
            // 예: personal_PresetID -> chat_personal_PresetID.db
            // 예: group_GroupID -> chat_group_GroupID.db
            string path = Path.Combine(Application.persistentDataPath, $"chat_{dbKey}.db");

            db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
            db.CreateTable<ChatMessage>();
            Debug.Log($"[ChatDatabase] DB 연결 성공: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatDatabase] DB 열기 실패 (Key: {dbKey})\n{ex.Message}\n{ex.StackTrace}");
            db = null;
        }
    }

    public void Close()
    {
        db?.Close();
        db = null;
    }
    
    public void InsertMessage(string senderPresetId, string messageJson)
    {
        if (db == null) throw new Exception("DB not opened");
        db.Insert(new ChatMessage { SenderID = senderPresetId, Message = messageJson, Timestamp = DateTime.UtcNow });
        EnforceMessageLimit();
    }
    
    private void EnforceMessageLimit()
    {
        int total = db.Table<ChatMessage>().Count();
        if (total > MaxMessageCount)
        {
            int toDelete = total - MaxMessageCount;
            var oldest = db.Table<ChatMessage>().OrderBy(m => m.Id).Take(toDelete).ToList();
            foreach (var msg in oldest) { db.Delete(msg); }
        }
    }
    
    public List<ChatMessage> GetRecentMessages(int count)
    {
        if (db == null) throw new Exception("DB not opened");
        return db.Table<ChatMessage>().OrderByDescending(m => m.Id).Take(count).ToList().OrderBy(m => m.Id).ToList();
    }

    public List<ChatMessage> GetAllMessages(int limit = int.MaxValue)
    {
        if (db == null) throw new Exception("DB not opened");
        return db.Table<ChatMessage>().OrderBy(m => m.Id).Take(limit).ToList();
    }

    public void ClearAllMessages()
    {
        if(db == null) throw new Exception("DB not opened");
        db.DeleteAll<ChatMessage>();
    }
    
    [System.Serializable]
    public class ChatMessage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [NotNull]
        public string SenderID { get; set; } // "user", "system", 또는 캐릭터의 presetID
        
        public string Message { get; set; } // JSON content
        
        [NotNull]
        public DateTime Timestamp { get; set; }
    }
}
// --- END OF FILE ChatDatabase.cs ---