using UnityEngine;

/// <summary>
/// 채팅 메시지의 상세 데이터를 담는 직렬화 가능한 클래스.
/// 이 객체는 JSON으로 변환되어 데이터베이스에 저장됩니다.
/// </summary>
[System.Serializable]
public class MessageData
{
    // 메시지의 유형 (예: "text", "image", "system")
    public string type;
    
    // 실제 텍스트 내용
    public string textContent;
    
    // 파일 내용 (이미지: base64, 텍스트 파일: 원본 텍스트)
    public string fileContent;
    
    // 첨부 파일의 원래 이름
    public string fileName;
    
    // 첨부 파일의 크기 (바이트 단위)
    public long fileSize;
}

/// <summary>
/// AI의 시간 기반 자율 행동 이벤트의 종류를 정의합니다.
/// </summary>
public enum TimeEventType 
{ 
    Morning, 
    Lunch, 
    Evening, 
    Night, 
    Dawn 
}

/// <summary>
/// AI의 랜덤 자율 행동 이벤트의 종류를 정의합니다.
/// </summary>
public enum RandomEventType 
{ 
    Compliment, 
    Question, 
    Joke, 
    Encouragement 
}