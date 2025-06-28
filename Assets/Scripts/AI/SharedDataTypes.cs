using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MessageData
{
    public string type;
    public string textContent;
    public string fileContent;
    public string fileName;
    public long fileSize;
}

public enum TimeEventType{Morning, Lunch, Evening, Night, Dawn}
public enum RandomEventType {Compliment, Question, Joke, Encouragement}
