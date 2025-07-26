using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using SQLite4Unity3d;

/// <summary>
/// 단일 채팅 채널(1:1 또는 그룹)의 대화 기록을 관리하는 SQLite 데이터베이스 클래스.
/// ChatDatabaseManager에 의해 인스턴스화되고 관리됩니다.
/// </summary>
public class ChatDatabase
{
    private SQLiteConnection dbConnection;
    private string databasePath;

    // 데이터베이스에 저장할 최대 메시지 수. 이를 초과하면 가장 오래된 메시지부터 삭제됩니다.
    private const int MaxMessageCount = 500;

    /// <summary>
    /// 지정된 ID를 기반으로 데이터베이스 파일을 열고 연결을 설정합니다.
    /// </summary>
    /// <param name="databaseId">DB 파일을 식별하는 고유 ID (예: "personal_preset123", "group_group456")</param>
    public void Open(string databaseId)
    {
        try
        {
            if (dbConnection != null)
            {
                Close();
            }

            // Application.persistentDataPath는 플랫폼에 맞는 안전한 저장 경로를 제공합니다.
            databasePath = Path.Combine(Application.persistentDataPath, $"chat_{databaseId}.db");
            dbConnection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
            
            // ChatMessage 테이블이 없으면 새로 생성합니다.
            dbConnection.CreateTable<ChatMessage>();
            
            Debug.Log($"[ChatDatabase] DB 연결 성공: {databasePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatDatabase] DB 열기 실패 (ID: {databaseId}, Path: {databasePath})\n{ex.Message}\n{ex.StackTrace}");
            dbConnection = null;
        }
    }

    /// <summary>
    /// 데이터베이스 연결을 안전하게 닫습니다.
    /// </summary>
    public void Close()
    {
        dbConnection?.Close();
        dbConnection = null;
    }
    
    /// <summary>
    /// 새로운 메시지를 데이터베이스에 추가하고, 최대 메시지 수 제한을 적용합니다.
    /// </summary>
    /// <param name="senderId">메시지 발신자의 ID ("user" 또는 캐릭터의 presetID)</param>
    /// <param name="messageJson">메시지 내용을 담은 JSON 문자열</param>
    public void InsertMessage(string senderId, string messageJson)
    {
        if (dbConnection == null) throw new InvalidOperationException("데이터베이스가 열려있지 않습니다.");
        
        var newMessage = new ChatMessage 
        { 
            SenderID = senderId, 
            Message = messageJson, 
            Timestamp = DateTime.UtcNow // UTC 시간으로 저장하여 시간대 문제 방지
        };
        dbConnection.Insert(newMessage);
        
        EnforceMessageLimit();
    }
    
    /// <summary>
    /// 지정된 개수만큼의 최근 메시지를 시간순으로 정렬하여 반환합니다.
    /// </summary>
    /// <param name="count">가져올 메시지 개수</param>
    /// <returns>ChatMessage 객체 리스트</returns>
    public List<ChatMessage> GetRecentMessages(int count)
    {
        if (dbConnection == null) throw new InvalidOperationException("데이터베이스가 열려있지 않습니다.");
        
        // ID를 내림차순으로 정렬하여 최근 N개를 가져온 후, 다시 시간순(오름차순)으로 정렬하여 반환
        return dbConnection.Table<ChatMessage>().OrderByDescending(m => m.Id).Take(count).ToList().OrderBy(m => m.Id).ToList();
    }

    /// <summary>
    /// 데이터베이스에 있는 모든 메시지를 시간순으로 정렬하여 반환합니다. (기억 시스템용)
    /// </summary>
    /// <param name="limit">가져올 최대 메시지 수</param>
    /// <returns>ChatMessage 객체 리스트</returns>
    public List<ChatMessage> GetAllMessages(int limit = int.MaxValue)
    {
        if (dbConnection == null) throw new InvalidOperationException("데이터베이스가 열려있지 않습니다.");
        return dbConnection.Table<ChatMessage>().OrderBy(m => m.Id).Take(limit).ToList();
    }

    /// <summary>
    /// 데이터베이스의 모든 메시지를 삭제합니다.
    /// </summary>
    public void ClearAllMessages()
    {
        if (dbConnection == null) throw new InvalidOperationException("데이터베이스가 열려있지 않습니다.");
        dbConnection.DeleteAll<ChatMessage>();
    }

    /// <summary>
    /// 데이터베이스에 저장될 메시지의 데이터 구조.
    /// </summary>
    public class ChatMessage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [NotNull]
        public string SenderID { get; set; }
        
        public string Message { get; set; } // JSON 형식의 메시지 내용
        
        [NotNull]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 데이터베이스 파일의 총 개수가 한도를 초과하면 가장 오래된 메시지를 삭제합니다.
    /// </summary>
    private void EnforceMessageLimit()
    {
        int totalMessages = dbConnection.Table<ChatMessage>().Count();
        if (totalMessages > MaxMessageCount)
        {
            int deleteCount = totalMessages - MaxMessageCount;
            // 가장 오래된 메시지(ID가 가장 낮은 메시지)를 찾아 삭제
            var oldestMessages = dbConnection.Table<ChatMessage>().OrderBy(m => m.Id).Take(deleteCount).ToList();
            foreach (var msg in oldestMessages)
            {
                dbConnection.Delete(msg);
            }
        }
    }
}