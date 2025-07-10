// --- START OF FILE CharacterPresetManager.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SFB;
using Steamworks;
using DesktopMaid;

public class CharacterPresetManager : MonoBehaviour
{
    public static CharacterPresetManager Instance { get; private set; }
    
    [SerializeField] private CharacterPreset initialPreset;
    
    [SerializeField] private Transform scrollContent;       // ScrollView의 Content
    [SerializeField] private GameObject presetPrefab;       // CharacterPreset 프리팹
    [SerializeField] private GameObject chatPrefab;
    public Transform rightChatArea;
    
    public List<CharacterPreset> presets = new List<CharacterPreset>();
    public List<ChatUI> chatUIs = new List<ChatUI>();
    private int currentIndex = -1;
    private int presetCounter = 1;
    
    [Header("Preset Slot Limit")]
    [SerializeField] private int defaultFreeLimit = 3;
    
    // DLC 설치 직후 실시간 반영용
    private Callback<DlcInstalled_t> _dlcInstalledCallback;
    
    public SettingPanelController settingPanelController;
    public UIManager uiManager;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        
        if (SteamManager.Initialized)
            _dlcInstalledCallback = Callback<DlcInstalled_t>.Create(OnDlcInstalled);
        
        Debug.Log($"[Awake] CharacterPresetManager 초기화됨. 현재 ChatUI 수 = {FindObjectsOfType<ChatUI>(true).Length}");
    }
    
    private void Start()
    {
        if (initialPreset != null && (string.IsNullOrEmpty(initialPreset.presetID) || initialPreset.presetID != "DefaultPreset"))
        {
            initialPreset.presetID = "DefaultPreset";
        }

        if (presets.Count == 0 && initialPreset != null)
        {
            presets.Add(initialPreset);
            currentIndex = 0;
            
            var initialChatUI = FindObjectsOfType<ChatUI>(true).FirstOrDefault(ui => ui.presetID == initialPreset.presetID);
            if (initialChatUI != null)
            {
                chatUIs.Add(initialChatUI);
                initialPreset.chatUI = initialChatUI;
            }
        }
    }

    public CharacterPreset AddNewPreset(string existingId = null)
    {
        //프리셋 한도 검사
        if (presets.Count >= GetCurrentPresetLimit())
        {
            UIManager.instance.TriggerWarning(
                $"현재 프리셋 한도({GetCurrentPresetLimit()}개)를 초과할 수 없습니다.\n" +
                "Steam DLC 'Unlimited Presets' 구매 시 한도가 해제됩니다.");
            return null;
        }
        
        string id = existingId;
        if (string.IsNullOrEmpty(id))
        {
            id = $"Preset_{System.DateTime.Now.Ticks}"; // 중복 방지를 위해 유니크한 ID 생성
        }
        
        GameObject newObj = Instantiate(presetPrefab, scrollContent);
        CharacterPreset newPreset = newObj.GetComponent<CharacterPreset>();
        newPreset.presetID = id;
        
        // [추가] 새로 생성된 프리셋의 친밀도 기본값을 설정합니다.
        newPreset.intimacy = "3"; // UI 및 프롬프트용
        newPreset.SetIntimacyFromString("3"); // 내부 점수도 '3' 레벨에 맞게 설정 (-50f)
        newPreset.iQ = "3"; // IQ 기본값도 함께 설정해주는 것이 좋습니다.
        
        presets.Add(newPreset);

        Button btn = newObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => newPreset.OnClickPresetButton());
        }
        
        GameObject chatObj = Instantiate(chatPrefab, rightChatArea);
        ChatUI chatUI = chatObj.GetComponent<ChatUI>();
        chatUI.presetID = id;
        
        newPreset.chatUI = chatUI;
        chatUIs.Add(chatUI);

        if (WindowSnapManager.Instance != null)
        {
            var header = chatObj.transform.Find("Header")?.GetComponent<RectTransform>();
            var mainPanel = chatObj.transform.Find("Panel")?.GetComponent<RectTransform>(); 
            
            if (header != null && mainPanel != null)
            {
                WindowSnapManager.Instance.uiTargets.Add(new WindowSnapManager.UITarget
                {
                    header = header,
                    mainPanel = mainPanel
                });
                Debug.Log($"✅ Snap Target으로 등록: {chatObj.name} (ID: {id})");
            }
            else
            {
                if (header == null) Debug.LogWarning($"⚠️ {chatObj.name}에서 Header를 찾지 못했습니다.");
                if (mainPanel == null) Debug.LogWarning($"⚠️ {chatObj.name}에서 'Panel'이라는 이름의 메인 패널을 찾지 못했습니다.");
            }
        }
        
        FindObjectOfType<GroupPanelController>()?.RefreshGroupListUI();
        
        Debug.Log($"[CharacterPresetManager] 프리셋 추가 완료 (ID: {id})");
        return newPreset;
    }
    
    public void OnClickAddNewPresetButton()
    {
        AddNewPreset();
    }
    
    public CharacterPreset GetCurrentPreset()
    {
        if (currentIndex < 0 || currentIndex >= presets.Count) return null;
        return presets[currentIndex];
    }
    
    public CharacterPreset GetPreset(string presetId)
    {
        // presets 리스트에서 제공된 ID와 일치하는 첫 번째 프리셋을 찾아 반환합니다.
        // Linq의 FirstOrDefault를 사용하면 일치하는 항목이 없을 경우 null을 반환하여 안전합니다.
        return presets.FirstOrDefault(p => p.presetID == presetId);
    }
    
    public void ActivatePreset(CharacterPreset selected)
    {
        currentIndex = presets.IndexOf(selected);

        if (settingPanelController != null)
        {
            settingPanelController.targetPreset = selected;
        }

        uiManager.OpenAndCloseCharacterPanel();
    }
    
    public void SetCurrentPreset(CharacterPreset preset)
    {
        int idx = presets.IndexOf(preset);
        if (idx >= 0) currentIndex = idx;
    }

    public void ExportCurrentPreset()
    {
        var preset = GetCurrentPreset();
        if (preset == null) return;

        string imageBase64 = "";
        if (preset.characterImage != null && preset.characterImage.sprite != null)
        {
            Texture2D tex = preset.characterImage.sprite.texture;
            byte[] bytes = tex.EncodeToPNG();
            imageBase64 = System.Convert.ToBase64String(bytes);
        }

        var data = new CharacterPresetData
        {
            name = preset.characterName,
            gender = preset.gender,
            personality = preset.personality,
            onMessage = preset.onMessage,
            sleepMessage = preset.sleepMessage,
            offMessage = preset.offMessage,
            setting = preset.characterSetting,
            iq = preset.iQ,
            dialogueExamples = preset.dialogueExample,
            characterImageBase64 = imageBase64,
            sittingOffsetY = preset.sittingOffsetY,
        };

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string path = StandaloneFileBrowser.SaveFilePanel("프리셋 저장", "", preset.characterName, "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"✅ 프리셋 저장 완료: {path}");
        }
    }

    public void ImportPresetFromFile()
    {
        string[] paths = StandaloneFileBrowser.OpenFilePanel("프리셋 불러오기", "", "json", false);
        if (paths.Length == 0 || string.IsNullOrEmpty(paths[0])) return;

        string json = File.ReadAllText(paths[0]);
        CharacterPresetData data = JsonConvert.DeserializeObject<CharacterPresetData>(json);
        if (data == null) return;

        var current = GetCurrentPreset();
        if (current == null)
        {
            Debug.LogWarning("⚠️ 불러올 대상 프리셋이 없습니다.");
            return;
        }

        current.ApplyData(data);
        current.characterName = data.name;

        if (!string.IsNullOrEmpty(data.characterImageBase64))
        {
            byte[] imageBytes = System.Convert.FromBase64String(data.characterImageBase64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            current.characterImage.sprite = sprite;
        }

        if (settingPanelController != null)
        {
            settingPanelController.targetPreset = current;
            settingPanelController.LoadPresetToUI();
        }
        
        current.SetProfile();

        Debug.Log($"✅ 프리셋 덮어쓰기 완료: {data.name}");
    }
    
    public void DeletePreset(CharacterPreset targetPreset)
    {
        if (!presets.Contains(targetPreset))
        {
            Debug.LogWarning("삭제하려는 프리셋이 존재하지 않습니다.");
            return;
        }

        string idToDelete = targetPreset.presetID;
        Debug.Log($"🗑️ 프리셋 삭제 시작 (ID: {idToDelete})");
        
        if (!string.IsNullOrEmpty(targetPreset.groupID))
        {
            string previousGroupID = targetPreset.groupID;
            string characterName = targetPreset.characterName;

            // 그룹 채팅 DB에 '연결 끊어짐' 메시지 기록
            string systemMessageText = $"'{characterName}'님과의 연결이 완전히 끊어졌습니다.";
            var messageData = new MessageData { type = "system", textContent = systemMessageText };
            string messageJson = JsonUtility.ToJson(messageData);
            ChatDatabaseManager.Instance.InsertGroupMessage(previousGroupID, "system", messageJson);

            // CharacterGroupManager를 통해 그룹의 멤버 목록からも 공식적으로 제거
            CharacterGroupManager.Instance.RemoveMemberFromGroup(idToDelete, false);
        }

        ChatDatabaseManager.Instance.DeleteDatabase(idToDelete);

        if (targetPreset.vrmModel != null)
        {
            Destroy(targetPreset.vrmModel.transform.root.gameObject);
            Debug.Log($"✅ VRM 모델 제거 완료: {targetPreset.vrmModel.name}");
        }

        if (targetPreset.chatUI != null)
        {
            chatUIs.Remove(targetPreset.chatUI);
            Destroy(targetPreset.chatUI.gameObject);
            Debug.Log($"✅ ChatUI 제거 완료: {targetPreset.chatUI.name}");
        }

        presets.Remove(targetPreset);
        
        Destroy(targetPreset.gameObject);
        Debug.Log($"✅ 프리셋 리스트 및 UI에서 제거 완료: {idToDelete}");


        SaveController saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
            Debug.Log("✅ SaveController를 통한 동기화 완료");
        }
        
        FindObjectOfType<GroupPanelController>()?.RefreshGroupListUI();

        if (presets.Count > 0)
        {
            currentIndex = 0;
        }
        else
        {
            currentIndex = -1;
        }
    }
    
    public void OnClickDeleteCurrentPreset()
    {
        var target = GetCurrentPreset();
        if (target != null && target != initialPreset)
        {
            DeletePreset(target);
        }
        else if (target == initialPreset)
        {
            UIManager.instance.TriggerWarning("기본 프리셋은 삭제할 수 없습니다.");
        }
        
        UIManager.instance.OpenAndCloseCharacterPanel();
        UIManager.instance.OpenAndCloseDeletePanel();
    }
    
    public List<SaveCharacterPresetData> GetAllPresetData()
    {
        List<SaveCharacterPresetData> list = new List<SaveCharacterPresetData>();
        foreach (var preset in presets)
        {
            if (preset == initialPreset) continue;
            
            string imageBase64 = "";
            if (preset.characterImage != null && preset.characterImage.sprite != null)
            {
                Texture2D tex = preset.characterImage.sprite.texture;
                if(tex.isReadable)
                {
                    byte[] bytes = tex.EncodeToPNG();
                    imageBase64 = System.Convert.ToBase64String(bytes);
                }
            }

            list.Add(new SaveCharacterPresetData
            {
                id = preset.presetID,
                name = preset.characterName,
                onMessage = preset.onMessage,
                sleepMessage = preset.sleepMessage,
                offMessage = preset.offMessage,
                gender = preset.gender,
                personality = preset.personality,
                setting = preset.characterSetting,
                iq = preset.iQ,
                intimacy = preset.intimacy, // UI용 string 값 저장
                internalIntimacyScore = preset.internalIntimacyScore, // [추가] 내부 float 점수 저장
                dialogueExamples = new List<string>(preset.dialogueExample),
                characterImageBase64 = imageBase64,
                vrmFilePath = preset.vrmFilePath,
                sittingOffsetY = preset.sittingOffsetY,
            });
        }
        return list;
    }

    public void LoadPresetsFromData(List<SaveCharacterPresetData> dataList)
    {
        for (int i = presets.Count - 1; i >= 0; i--)
        {
            var p = presets[i];
            if (p != initialPreset)
            {
                if (p.chatUI != null)
                {
                    chatUIs.Remove(p.chatUI);
                    Destroy(p.chatUI.gameObject);
                }
                presets.RemoveAt(i);
                Destroy(p.gameObject);
            }
        }
        presets.Clear();
        chatUIs.Clear();

        if(initialPreset != null)
        {
             presets.Add(initialPreset);
             
             var initialChatUI = FindObjectsOfType<ChatUI>(true).FirstOrDefault(ui => ui.presetID == initialPreset.presetID);
             if (initialChatUI != null)
             {
                chatUIs.Add(initialChatUI);
                initialPreset.chatUI = initialChatUI;
             }
        }
       
        foreach (var data in dataList)
        {
            var newPreset = AddNewPreset(data.id);
            
            // ApplyData를 통해 저장된 값을 프리셋에 적용
            newPreset.ApplyData(data);
            newPreset.vrmFilePath = data.vrmFilePath;

            // 로드된 float 점수 기준으로 UI용 string 값을 한번 보정해줌
            newPreset.UpdateIntimacyStringValue();

            if (!string.IsNullOrEmpty(data.characterImageBase64))
            {
                byte[] imageBytes = System.Convert.FromBase64String(data.characterImageBase64);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(imageBytes);
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                newPreset.characterImage.sprite = sprite;
            }
            
            newPreset.SetProfile();
        }
        
        Debug.Log($"✅ {dataList.Count}개의 프리셋 로딩 및 동기화 완료.");
    }

    #region DLC 관련

    /// <summary>DLC 설치 여부 확인</summary>
    private bool HasUnlimitedPresets()
    {
        return SteamManager.Initialized &&
               SteamApps.BIsDlcInstalled(SteamIds.DLC_ID_UNLIMITED_PRESETS);
    }
    
    /// <summary>현재 허용 프리셋 한도</summary>
    private int GetCurrentPresetLimit()
        => HasUnlimitedPresets() ? int.MaxValue : defaultFreeLimit;

    /// <summary>게임 실행 중 DLC 구매 시 한도 해제</summary>
    private void OnDlcInstalled(DlcInstalled_t data)
    {
        if (!data.m_nAppID.Equals(SteamIds.DLC_ID_UNLIMITED_PRESETS)) return;
        
        Debug.Log("[CharacterPresetManager] Unlimited Presets DLC 설치 확인 → 한도 해제");
        
        UIManager.instance.TriggerWarning("프리셋 한도가 해제되었습니다!");
        
    }

    #endregion
}