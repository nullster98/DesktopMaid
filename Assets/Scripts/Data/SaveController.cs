// --- START OF FILE SaveController.cs ---

using System;
using System.Collections;
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
    
    private string _cachedLocaleCode = "ko";
    private bool _isQuitRoutineRunning = false;
    
    [Header("Startup Settings")]
    [SerializeField] private InitialLanguageSelector initialLanguageSelector;

    private void Awake()
    {
        // 1. Locale 캐싱
        LocalizationSettings.SelectedLocaleChanged +=
            loc => _cachedLocaleCode = loc.Identifier.Code;

        // 2. 종료 요청 가로채기
        Application.wantsToQuit += OnWantsToQuit;
    }
    
    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -=
            loc => _cachedLocaleCode = loc.Identifier.Code;

        Application.wantsToQuit -= OnWantsToQuit;
    }
    
    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        
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
        
        configData.languageCode = _cachedLocaleCode;
        Debug.Log($"언어 저장 {configData.languageCode}");

        // [수정] CharacterPresetManager 대신 MainListController에서 통합 아이템 순서를 가져옵니다.
        if (MainListController.Instance != null)
        {
            configData.mainItemListOrder = MainListController.Instance.GetCurrentItemIDOrder();
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
            // 데이터가 전혀 없을 때도 로드 완료 처리를 해야 합니다.
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
                Debug.Log($"[SaveController] 언어 적용 완료 → {LocalizationSettings.SelectedLocale.Identifier.Code}");
            }
            else
            {
                Debug.LogWarning($"[SaveController] 저장된 언어 코드 '{data.config.languageCode}'에 해당하는 로케일을 찾을 수 없습니다.");
            }
        }

        // 데이터 로드는 순서가 중요합니다.
        // 매니저들이 데이터를 먼저 로드해야 MainListController가 UI를 그릴 수 있습니다.
        UserData.Instance.ApplyUserSaveData(data.userData);
        CharacterPresetManager.Instance.LoadPresetsFromData(data.presets);
        
        if (CharacterGroupManager.Instance != null && data.groups != null)
        {
            CharacterGroupManager.Instance.allGroups = data.groups;
        }

        if (data.config != null)
        {
            UserData.Instance.ApplyAppConfigData(data.config);

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetZoomLevel((data.config.cameraZoomLevel));
            }

            if (aiConfig != null)
            {
                if (System.Enum.IsDefined(typeof(ModelMode), data.config.modelMode))
                {
                    aiConfig.modelMode = (ModelMode)data.config.modelMode;
                    Debug.Log($"[SaveController] 저장된 AI Model Mode 로드: {aiConfig.modelMode}");
                }
            }
        }
        
        // [삭제] 프리셋 순서 정렬 로직은 MainListController로 이전됩니다.
        // 이 코드는 MainListController.RefreshList() 내부에서 처리됩니다.
        
        CharacterPresetManager.Instance?.FinalizePresetLoading();
        
        isLoadCompleted = true;
        
        Debug.Log("전체 데이터 로드 완료.");
        OnLoadComplete?.Invoke();
        
        // 모든 데이터 로드가 끝난 후, MainListController에게 UI를 그리라고 명령합니다.
        // 저장된 순서 정보를 인자로 전달합니다.
        if (MainListController.Instance != null)
        {
            MainListController.Instance.RefreshList(data.config?.mainItemListOrder);
        }
        
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
    
    private bool OnWantsToQuit()
    {
        // 이미 저장 코루틴이 돌고 있으면 OK
        if (_isQuitRoutineRunning) return true;

        // 아직이면 저장 먼저!
        StartCoroutine(QuitAfterSave());
        return false;            // 이번 종료는 취소
    }
    
    private IEnumerator QuitAfterSave()
    {
        _isQuitRoutineRunning = true;

        // ① 설정이 살아 있을 때 캐시 갱신
        LocalizationSettings.SelectedLocaleChanged -=
            loc => _cachedLocaleCode = loc.Identifier.Code;

        SaveEverything();        // ② 동기식 저장

        yield return null;       // ③ 한 프레임 대기 (WriteAllText flush 보장)

        // ④ 이벤트 분리: Application.Quit() 재귀 호출 방지
        Application.wantsToQuit -= OnWantsToQuit;

        Application.Quit();      // ⑤ 이번엔 진짜 종료
    }
}
// --- END OF FILE SaveController.cs ---