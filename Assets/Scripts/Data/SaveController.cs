// --- START OF FILE SaveController.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class SaveController : MonoBehaviour
{
    public static event Action OnLoadComplete;
    public static bool isLoadCompleted { get; private set; } = false;
    
    public float saveInterval = 60f;
    private float timer;
    
    [Header("Startup Settings")]
    [SerializeField] private InitialLanguageSelector initialLanguageSelector;

    void Start()
    {
        // 먼저 저장 파일을 로드 시도합니다.
        var data = SaveData.LoadAll();

        // 로드된 데이터가 없으면(파일이 없으면) 최초 실행으로 간주합니다.
        if (data == null)
        {
            // 최초 실행일 경우:
            // 1. InitialLanguageSelector에게 언어 선택 프로세스를 시작하라고 명령합니다.
            initialLanguageSelector.StartLanguageSelectionProcess();
            // 2. 언어 선택이 완료되면 LoadEverything을 다시 호출하도록 이벤트를 구독합니다.
            InitialLanguageSelector.OnLanguageSelected += HandleFirstLaunchLanguageSelected;
        }
        else
        {
            // 최초 실행이 아닐 경우(파일이 있을 경우): 로드된 데이터를 사용하여 앱을 초기화합니다.
            LoadEverything(data);
        }
    }
    
    private void HandleFirstLaunchLanguageSelected()
    {
        InitialLanguageSelector.OnLanguageSelected -= HandleFirstLaunchLanguageSelected;
        
        // 언어가 설정되었으므로, 앱의 나머지 로딩 절차를 시작합니다.
        // 이제 막 파일이 생성될 것이므로, 인자 없이 호출하여 새로 로드합니다.
        LoadEverything();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= saveInterval)
        {
            SaveEverything();
            timer = 0f;
        }
    }

    public void SaveEverything()
    {
        var aisObserver = FindObjectOfType<AIScreenObserver>();
        var transparentOverlay = FindObjectOfType<TransparentOverlay>();
        var windowAutoStart = FindObjectOfType<WindowAutoStart>();
        
        var aiConfig = Resources.Load<AIConfig>("AIConfig");
        
        AppConfigData configData = new AppConfigData();

        if (transparentOverlay != null) configData.alwaysOnTop = transparentOverlay.IsAlwaysOnTopState;
        if (windowAutoStart != null) configData.autoStartEnabled = windowAutoStart.IsAutoStartEnabled;
        if (aisObserver != null)
        {
            configData.screenCaptureModuleEnabled = aisObserver.screenCaptureModuleEnabled;
            configData.selfAwarenessModuleEnabled = aisObserver.selfAwarenessModuleEnabled;
        }
        if (CameraManager.Instance != null) configData.cameraZoomLevel = CameraManager.Instance.CurrentCameraSize;
        
        if (UserData.Instance != null)
        {
            configData.systemVolume = UserData.Instance.SystemVolume;
            configData.alarmVolume = UserData.Instance.AlarmVolume;
        }

        if (aiConfig != null)
        {
            configData.modelMode = (int)aiConfig.modelMode;
        }
        
        if (LocalizationSettings.HasSettings && LocalizationSettings.SelectedLocale != null)
        {
            configData.languageCode = LocalizationSettings.SelectedLocale.Identifier.Code;
        }
        
        if (CharacterPresetManager.Instance != null)
        {
            List<string> orderedPresetIds = new List<string>();
            Transform content = CharacterPresetManager.Instance.scrollContent;
            if (content != null)
            {
                foreach (Transform child in content)
                {
                    CharacterPreset preset = child.GetComponent<CharacterPreset>();
                    if (preset != null)
                    {
                        orderedPresetIds.Add(preset.presetID);
                    }
                }
            }
            configData.presetOrder = orderedPresetIds;
        }
        
        SaveData.SaveAll(
            UserData.Instance.GetUserSaveData(),
            CharacterPresetManager.Instance.GetAllPresetData(),
            CharacterGroupManager.Instance.allGroups,
            configData
        );
    }

    public void LoadEverything(AppSaveData dataToLoad = null)
    {
        isLoadCompleted = false;
        
        var data = dataToLoad ?? SaveData.LoadAll();
        
        if(data == null)
        {
            CharacterPresetManager.Instance?.FinalizePresetLoading();
            OnLoadComplete?.Invoke();
            isLoadCompleted = true;
            return;
        }
        
        var aiConfig = Resources.Load<AIConfig>("AIConfig");
        
        if (data.config != null && !string.IsNullOrEmpty(data.config.languageCode))
        {
            var locale = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(l => l.Identifier.Code == data.config.languageCode);
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
                Debug.Log($"[SaveController] 저장된 언어 설정 로드: {locale.LocaleName}");
            }
            else
            {
                Debug.LogWarning($"[SaveController] 저장된 언어 코드 '{data.config.languageCode}'에 해당하는 로케일을 찾을 수 없습니다.");
            }
        }

        UserData.Instance.ApplyUserSaveData(data.userData);
        CharacterPresetManager.Instance.LoadPresetsFromData(data.presets);

        if (data.config != null)
        {
            UserData.Instance.ApplyAppConfigData(data.config);

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetZoomLevel((data.config.cameraZoomLevel));
            }

            if (aiConfig != null)
            {
                // 저장된 값이 유효한 enum 범위 내에 있는지 확인하는 것이 더 안전합니다.
                if (System.Enum.IsDefined(typeof(ModelMode), data.config.modelMode))
                {
                    aiConfig.modelMode = (ModelMode)data.config.modelMode;
                    Debug.Log($"[SaveController] 저장된 AI Model Mode 로드: {aiConfig.modelMode}");
                }
            }
        }
        
        // 저장된 프리셋 순서가 있다면, 그 순서대로 프리셋 리스트를 정렬
        if (data.config.presetOrder != null && data.config.presetOrder.Count > 0)
        {
            var loadedPresets = CharacterPresetManager.Instance.presets;
            var presetsDict = loadedPresets.ToDictionary(p => p.presetID);
            var sortedList = new List<CharacterPreset>();
            var sortedIds = new HashSet<string>();

            foreach (string id in data.config.presetOrder)
            {
                if (presetsDict.TryGetValue(id, out var preset))
                {
                    sortedList.Add(preset);
                    sortedIds.Add(id);
                }
            }
                
            // 저장된 순서에 포함되지 않은 프리셋(새로 추가된 프리셋 등)을 리스트 뒤에 추가
            foreach (var preset in loadedPresets)
            {
                if (!sortedIds.Contains(preset.presetID))
                {
                    sortedList.Add(preset);
                }
            }
                
            // CharacterPresetManager의 원본 리스트를 정렬된 리스트로 교체
            CharacterPresetManager.Instance.presets = sortedList;
            Debug.Log("[SaveController] 저장된 프리셋 순서에 따라 리스트를 정렬했습니다.");
            CharacterPresetManager.Instance.ReorderPresetUIElements();
        }
        
        if (CharacterGroupManager.Instance != null && data.groups != null)
        {
            CharacterGroupManager.Instance.allGroups = data.groups;
        }
        
        CharacterPresetManager.Instance?.FinalizePresetLoading();
        
        isLoadCompleted = true;
        
        // 각 컴포넌트의 Start()에서 자신의 설정을 로드하므로, 여기서 config를 적용할 필요는 없습니다.
        Debug.Log("전체 데이터 로드 완료.");
        OnLoadComplete?.Invoke();
        
        var configPanel = FindObjectOfType<ConfigurationPanelController>(true); // 비활성화 상태일 수 있으므로 true
        if (configPanel != null)
        {
            configPanel.ValidateAndRecoverModelSelection();
        }
    }

    public void OnClickSave()
    {
        SaveEverything();
        LocalizationManager.Instance.ShowWarning("저장 완료");
    }

    private void OnApplicationQuit()
    {
        SaveEverything();
    }
}