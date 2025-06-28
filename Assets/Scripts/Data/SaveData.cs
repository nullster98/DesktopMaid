// --- START OF FILE SaveData.cs ---

using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

[System.Serializable]
public class AppSaveData
{
    public UserSaveData userData;
    public List<SaveCharacterPresetData> presets;
    public List<CharacterGroup> groups;
    public AppConfigData config;
}

[System.Serializable]
public class UserSaveData
{
    public string userName;
    public string onMessage;
    public string sleepMessage;
    public string offMessage;
    public string profileImageBase64;
    public string userPrompt;
    public int conditionIndex;
    public string apiKey;
}

[System.Serializable]
public class SaveCharacterPresetData
{
    public string id;
    public string name;
    public string onMessage;
    public string sleepMessage;
    public string offMessage;
    public string gender;
    public string personality;
    public string setting;
    public string iq;
    public string intimacy;
    public float internalIntimacyScore;
    public List<string> dialogueExamples = new List<string>();
    public float  sittingOffsetY;
    public string characterImageBase64;
    public string vrmFilePath;
}

[System.Serializable]
public class AppConfigData
{
    public bool alwaysOnTop;
    public float bgmVolume;
    public float sfxVolume;
    public bool autoStartEnabled;
    public bool screenCaptureModuleEnabled;
    public bool selfAwarenessModuleEnabled;
    public float cameraZoomLevel;
}

public static class SaveData
{
    private static readonly string savePath = Path.Combine(Application.persistentDataPath, "appSave.json");

    public static void SaveAll(UserSaveData userData, List<SaveCharacterPresetData> presets, List<CharacterGroup> groups,  AppConfigData config)
    {
        AppSaveData data = new AppSaveData
        {
            userData = userData,
            presets = presets,
            groups = groups,
            config = config
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(savePath, json);
        Debug.Log("✅ 전체 저장 완료: " + savePath);
    }

    public static AppSaveData LoadAll()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("⚠️ 저장 파일이 없어 새 데이터 생성");
            return null;
        }

        string json = File.ReadAllText(savePath);
        return JsonConvert.DeserializeObject<AppSaveData>(json);
    }
}