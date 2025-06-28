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
    private string currentId;
    private const int MaxMessageCount = 500;

    public void Open(string id)
    {
        if (db != null) Close();
        // [수정] ID에 "group_" 접두사가 있는지 확인하여 파일 이름을 결정합니다.
        string dbFileName = id.StartsWith("group_") ? $"chat_{id}.db" : $"chat_personal_{id}.db";
        string path = Path.Combine(Application.persistentDataPath, dbFileName);
        
        db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
        db.CreateTable<ChatMessage>();
        currentId = id;
    }

    public void Close()
    {
        db?.Close();
        db = null;
        currentId = null;
    }
    
    public void InsertMessage(string senderPresetId, string messageJson)
    {
        if (db == null) throw new Exception("DB not opened");
        db.Insert(new ChatMessage { SenderID = senderPresetId, Message = messageJson, Timestamp = DateTime.UtcNow }); // [수정] DateTime 객체 직접 저장
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
        // [수정] 결과를 그대로 반환합니다. 이전처럼 문자열로 합치지 않습니다.
        return db.Table<ChatMessage>().OrderByDescending(m => m.Id).Take(count).ToList().OrderBy(m => m.Id).ToList();
    }

    // [추가] 요약 시스템을 위해 모든 메시지 객체를 반환하는 함수
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
    
    public class ChatMessage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [NotNull]
        public string SenderID { get; set; } // "user" 또는 캐릭터의 presetID
        
        public string Message { get; set; } // JSON content
        
        [NotNull]
        public DateTime Timestamp { get; set; }
    }
}