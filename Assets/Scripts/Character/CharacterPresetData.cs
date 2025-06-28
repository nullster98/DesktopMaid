using System.Collections.Generic;

[System.Serializable]
public class CharacterPresetData
{
    public string name;
    public string onMessage;
    public string sleepMessage;
    public string offMessage;
    public string gender;
    public string personality;
    public string iq;
    public string setting;
    public List<string> dialogueExamples = new();

    public string characterImageBase64;
    public string vrmFilePath;
    public float sittingOffsetY;
}