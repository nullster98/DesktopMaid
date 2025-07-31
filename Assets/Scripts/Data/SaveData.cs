// --- START OF FILE SaveData.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine.Serialization;

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
    public string groupID;
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

    public long creationTimestamp;

    public DateTime lastSpokeTime;
    
    // 프리셋의 마지막 상태(On, Sleep, Off)를 저장하기 위한 필드
    public int currentMode;
    public bool isAutoMoveEnabled;
}

[System.Serializable]
public class AppConfigData
{
    public bool alwaysOnTop;
    public float systemVolume;
    public float alarmVolume;
    public bool autoStartEnabled;
    public bool screenCaptureModuleEnabled;
    public bool selfAwarenessModuleEnabled;
    public float cameraZoomLevel;
    public int modelMode;
    public string languageCode;
    
    public List<string> mainItemListOrder; 
}

public static class SaveData
{
    // 확장자를 .dat 와 같이 알아보기 힘든 것으로 변경하는 것을 추천합니다.
    private static readonly string savePath = Path.Combine(Application.persistentDataPath, "appSave.cat");

    public static void SaveAll(UserSaveData userData, List<SaveCharacterPresetData> presets, List<CharacterGroup> groups, AppConfigData config)
    {
        // Steam이 초기화되지 않았으면 저장을 중단합니다.
#if !DISABLESTEAMWORKS && !UNITY_EDITOR
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SaveData] Steam이 실행 중이 아니므로 저장할 수 없습니다.");
            return;
        }
        // 사용자의 고유 Steam ID를 가져옵니다. 이것이 비밀번호가 됩니다.
        string key = SteamUser.GetSteamID().m_SteamID.ToString();
#else
        // 스팀을 사용하지 않는 빌드에서는 임시 키를 사용 (디버깅용)
        string key = "non_steam_debug_key";
        Debug.Log("[SaveData] 에디터 또는 Non-Steam 환경이므로 디버그 키를 사용하여 저장합니다.");
#endif

        AppSaveData data = new AppSaveData
        {
            userData = userData,
            presets = presets,
            groups = groups,
            config = config
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

        // Encrypt 함수를 호출하여 JSON을 암호화합니다.
        string encryptedJson = SaveEncryptor.Encrypt(json, key);

        if (encryptedJson != null)
        {
            File.WriteAllText(savePath, encryptedJson);
            Debug.Log("✅ 전체 저장 완료 (암호화됨): " + savePath);
        }
    }

    public static AppSaveData LoadAll()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("⚠️ 저장 파일이 없어 새 데이터 생성");
            return null;
        }

        // Steam이 초기화되지 않았으면 로드를 중단합니다.
#if !DISABLESTEAMWORKS && !UNITY_EDITOR
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SaveData] Steam이 실행 중이 아니므로 로드할 수 없습니다.");
            return null;
        }

        // 저장할 때 사용했던 것과 동일한 키를 생성합니다.
        string key = SteamUser.GetSteamID().m_SteamID.ToString();
#else
        string key = "non_steam_debug_key";
        Debug.Log("[SaveData] 에디터 또는 Non-Steam 환경이므로 디버그 키를 사용하여 로드합니다.");
#endif

        string encryptedJson = File.ReadAllText(savePath);

        // [핵심 변경] Decrypt 함수를 호출하여 암호문을 원본 JSON으로 복호화합니다.
        string json = SaveEncryptor.Decrypt(encryptedJson, key);

        if (json != null)
        {
            return JsonConvert.DeserializeObject<AppSaveData>(json);
        }

        // 복호화 실패 시 null 반환
        return null;
    }
}
// --- END OF FILE SaveData.cs ---