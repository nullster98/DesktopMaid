using System; // Action 사용을 위해 추가
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
using UnityEngine.Localization;

public class CharacterPresetManager : MonoBehaviour
{
    public static CharacterPresetManager Instance { get; private set; }

    public static event Action OnPresetsChanged;
    
    [SerializeField] private CharacterPreset initialPreset;
    
    [SerializeField] private Transform scrollContent;       // ScrollView의 Content
    [SerializeField] private GameObject presetPrefab;       // CharacterPreset 프리팹
    [SerializeField] private GameObject chatPrefab;
    public Transform rightChatArea;
    
    public List<CharacterPreset> presets = new List<CharacterPreset>();
    public List<ChatUI> chatUIs = new List<ChatUI>();
    private int currentIndex = -1;
    
    [Header("Preset Slot Limit")]
    [SerializeField] private int defaultFreeLimit = 3;
    
    private Callback<DlcInstalled_t> _dlcInstalledCallback;
    
    private bool hasNotifiedAboutLock = false;
    
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
        
#if !UNITY_EDITOR
        if (SteamManager.Initialized)
            _dlcInstalledCallback = Callback<DlcInstalled_t>.Create(OnDlcInstalled);
#endif
        
        SaveController.OnLoadComplete += CheckAndEnforcePresetLimit;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            SaveController.OnLoadComplete -= CheckAndEnforcePresetLimit;
            // Steam 콜백도 여기서 해제하는 것이 안전합니다.
            _dlcInstalledCallback?.Dispose();
        }
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
        
        StartCoroutine(PeriodicLimitCheckRoutine());
    }
    
    private IEnumerator PeriodicLimitCheckRoutine()
    {
        while (true)
        {
            // 3분(180초)마다 확인 (너무 잦을 필요 없음)
            yield return new WaitForSeconds(180f); 
            CheckAndEnforcePresetLimit();
        }
    }
    
    public void CheckAndEnforcePresetLimit()
    {
        bool isDlcOwned = HasUnlimitedPresets();
        int limit = defaultFreeLimit;
        
        // 사용자가 직접 만든 프리셋만 대상으로 합니다 (기본 프리셋 제외)
        var userPresets = presets.Where(p => p != initialPreset).ToList();

        // DLC를 소유하고 있거나, 프리셋 수가 제한 이내인 경우
        if (isDlcOwned || userPresets.Count <= limit)
        {
            // 모든 프리셋의 잠금을 해제합니다.
            foreach (var preset in userPresets)
            {
                if (preset.isLocked)
                {
                    preset.SetLockState(false);
                }
            }
            // 알림 상태를 초기화합니다.
            hasNotifiedAboutLock = false;
            return;
        }

        // --- 여기부터는 DLC가 없고, 프리셋 수가 제한을 초과한 경우의 로직 ---

        // 생성 시간 순서대로 프리셋을 정렬합니다 (오래된 것이 먼저).
        var sortedPresets = userPresets.OrderBy(p => p.creationTimestamp).ToList();
        
        int unlockedCount = 0;
        foreach (var preset in sortedPresets)
        {
            if (unlockedCount < limit)
            {
                // 가장 오래된 프리셋부터 limit 개수만큼은 잠금을 해제합니다.
                if (preset.isLocked) preset.SetLockState(false);
                unlockedCount++;
            }
            else
            {
                // 제한을 초과하는 나머지 프리셋(최신 프리셋)은 잠급니다.
                // -> [수정] 오래된 프리셋을 잠그는 것이 더 합리적이므로 정렬 순서를 유지합니다.
                if (!preset.isLocked) preset.SetLockState(true);
            }
        }

        // 잠금이 발생했고, 아직 사용자에게 알리지 않았다면 팝업을 표시합니다.
        if (!hasNotifiedAboutLock)
        {
            int lockedCount = userPresets.Count - limit;
            var arguments = new Dictionary<string, object>
            {
                ["LockedCount"] = lockedCount,
                ["LimitCount"] = limit
            };
            // 예시: "프리셋 슬롯 부족", "DLC가 없어 {LockedCount}개의 프리셋이 잠겼습니다. 슬롯은 {LimitCount}개까지 지원됩니다."
            LocalizationManager.Instance.ShowConfirmationPopup(
                "Popup_Title_PresetLocked", 
                "Popup_Msg_PresetLocked", 
                () => { /* 확인 시 Steam 상점 페이지로 이동하는 등의 액션 추가 가능 */ },
                null,
                arguments);
                
            hasNotifiedAboutLock = true; // 알림 완료 처리
        }
    }
    
    public void MovePresetToTop(string presetId)
    {
        // 1. ID에 해당하는 프리셋을 찾습니다.
        var presetToMove = presets.FirstOrDefault(p => p.presetID == presetId);
        if (presetToMove == null)
        {
            Debug.LogWarning($"[CharacterPresetManager] MovePresetToTop: ID '{presetId}'에 해당하는 프리셋을 찾을 수 없습니다.");
            return;
        }

        // 2. 해당 프리셋의 Transform 컴포넌트를 가져옵니다.
        Transform presetTransform = presetToMove.transform;
        
        presetTransform.SetAsFirstSibling();
        
        if (presets.Remove(presetToMove))
        {
            presets.Insert(0, presetToMove);
        }
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.MoveItemToTop(presetId);
        }

        Debug.Log($"[CharacterPresetManager] '{presetToMove.characterName}' 프리셋을 목록 최상단으로 이동시켰습니다.");
    }

    public CharacterPreset AddNewPreset(string existingId = null)
    {
        if (presets.Count >= GetCurrentPresetLimit())
        {
            LocalizationManager.Instance.ShowWarning("프리셋 제한");
            return null;
        }
        
        string id = existingId;
        if (string.IsNullOrEmpty(id))
        {
            id = $"Preset_{System.DateTime.Now.Ticks}";
        }
        
        GameObject newObj = Instantiate(presetPrefab, scrollContent);
        CharacterPreset newPreset = newObj.GetComponent<CharacterPreset>();
        newPreset.presetID = id;
        
        newPreset.creationTimestamp = System.DateTime.UtcNow.Ticks;
        
        newPreset.intimacy = "3";
        newPreset.SetIntimacyFromString("3");
        newPreset.iQ = "3";
        
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
            }
        }
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.CreateItemForPreset(newPreset);
        }
        
        FindObjectOfType<GroupPanelController>()?.RefreshGroupListUI();
        
        OnPresetsChanged?.Invoke();
        
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
        if (current == null) return;

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
        
        OnPresetsChanged?.Invoke();
    }
    
    /// <summary>
    /// [수정] 실제 프리셋 삭제 로직. 외부에서는 직접 호출하지 않습니다.
    /// </summary>
    private void DeletePresetInternal(CharacterPreset targetPreset)
    {
        if (targetPreset == null || !presets.Contains(targetPreset)) return;

        string idToDelete = targetPreset.presetID;
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.RemoveItemForPreset(idToDelete);
        }
        
        if (!string.IsNullOrEmpty(targetPreset.groupID))
        {
            string previousGroupID = targetPreset.groupID;
            string characterName = targetPreset.characterName;

            var localizedString = new LocalizedString("string Table", "Group_Member_Deleted");
            var args = new Dictionary<string, object> { { "CharacterName", characterName } };
            localizedString.Arguments = new object[] { args }; // Dictionary를 배열에 담아 전달
            string systemMessageText = localizedString.GetLocalizedString();
            
            var messageData = new MessageData { type = "system", textContent = systemMessageText };
            string messageJson = JsonUtility.ToJson(messageData);
            ChatDatabaseManager.Instance.InsertGroupMessage(previousGroupID, "system", messageJson);

            CharacterGroupManager.Instance.RemoveMemberFromGroup(idToDelete, false);
        }

        ChatDatabaseManager.Instance.DeleteDatabase(idToDelete);

        if (targetPreset.vrmModel != null)
        {
            Destroy(targetPreset.vrmModel.transform.root.gameObject);
        }

        if (targetPreset.chatUI != null)
        {
            chatUIs.Remove(targetPreset.chatUI);
            Destroy(targetPreset.chatUI.gameObject);
        }

        presets.Remove(targetPreset);
        Destroy(targetPreset.gameObject);
        
        CheckAndEnforcePresetLimit();

        SaveController saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
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

        // 현재 열려있는 모든 패널을 닫아 초기 화면으로 돌아가도록 합니다.
        if (UIManager.instance.characterPanel.activeSelf)
            UIManager.instance.OpenAndCloseCharacterPanel();
        
        OnPresetsChanged?.Invoke();
    }
    
    /// <summary>
    /// [핵심 수정] 'deletePanel'의 확인 버튼에서 호출되는 함수.
    /// 바로 삭제하지 않고, 최종 확인을 위한 범용 팝업을 띄웁니다.
    /// </summary>
    public void OnClickDeleteCurrentPreset()
    {
        var target = GetCurrentPreset();
        if (target == null) return;

        // 2. 기본 프리셋은 삭제할 수 없음을 경고하고 종료합니다.
        if (target == initialPreset)
        {
            LocalizationManager.Instance.ShowWarning("기본 프리셋 삭제");
            return;
        }
        
        // 3. Smart String에 전달할 인자를 생성합니다.
        var arguments = new Dictionary<string, object>
        {
            ["CharacterName"] = target.characterName
        };

        // 4. '확인'을 눌렀을 때 실제 삭제 로직(내부 함수)을 호출하는 Action을 정의합니다.
        Action onConfirm = () =>
        {
            DeletePresetInternal(target);
        };

        // 5. LocalizationManager를 통해 최종 확인 팝업을 요청합니다.
        LocalizationManager.Instance.ShowConfirmationPopup(
            "Popup_Title_DeleteChar",
            "Popup_Msg_DeleteChar",
            onConfirm,
            null, // 취소 버튼은 아무것도 하지 않음
            arguments
        );
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
                intimacy = preset.intimacy,
                internalIntimacyScore = preset.internalIntimacyScore,
                dialogueExamples = new List<string>(preset.dialogueExample),
                characterImageBase64 = imageBase64,
                vrmFilePath = preset.vrmFilePath,
                sittingOffsetY = preset.sittingOffsetY,
                creationTimestamp = preset.creationTimestamp
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
            
            newPreset.ApplyData(data);
            newPreset.vrmFilePath = data.vrmFilePath;

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
            
            if (MiniModeController.Instance != null)
            {
                MiniModeController.Instance.RefreshAllItems();
            }
        }
        
        OnPresetsChanged?.Invoke();
    }

    #region DLC 관련

    private bool HasUnlimitedPresets()
    {
        return SteamManager.Initialized &&
               SteamApps.BIsDlcInstalled(SteamIds.DLC_ID_UNLIMITED_PRESETS);
    }
    
    private int GetCurrentPresetLimit()
        => HasUnlimitedPresets() ? int.MaxValue : defaultFreeLimit;

    private void OnDlcInstalled(DlcInstalled_t data)
    {
        if (!data.m_nAppID.Equals(SteamIds.DLC_ID_UNLIMITED_PRESETS)) return;
        
        LocalizationManager.Instance.ShowWarning("DLC 적용");
        
        CheckAndEnforcePresetLimit();
    }

    #endregion
}