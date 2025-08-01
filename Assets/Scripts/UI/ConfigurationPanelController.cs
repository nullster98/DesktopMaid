using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using AI;
using System.Linq;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization.Settings; 

public class ConfigurationPanelController : MonoBehaviour
{
    [Header("Gemini API 설정")]
    [SerializeField] private GameObject apiKeyGroup;
    [SerializeField] private TMP_InputField apiKeyField;
    [SerializeField] private Button apiKeyConfirmButton;

    [Header("Ollama 모델 설정")]
    [SerializeField] private GameObject ollamaModelGroup;
    [SerializeField] private TMP_Dropdown ollamaModelDropdown;
    [SerializeField] private Button ollamaApplyButton;

    [Header("UI 연결")]
    [Tooltip("전체 줌 레벨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private Toggle localModelToggle;
    [Tooltip("전체 사운드 볼륨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider soundVolumeSlider;
    [SerializeField] private TMP_Text localModelToggleText;
    [Tooltip("알람 사운드 볼륨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider alarmVolumeSlider;
    
    [Header("Sound Feedback")]
    [Tooltip("볼륨 조절 시 피드백 사운드를 재생할 AudioSource")]
    [SerializeField] private AudioSource feedbackAudioSource;
    [Tooltip("재생할 피드백 사운드 클립")]
    [SerializeField] private AudioClip feedbackSoundClip;

    [Header("Localization")]
    [SerializeField] private LocalizedString localModelInfoText;
    [SerializeField] private LocalizedString apiModelInfoText;
    [SerializeField] private LocalizedString useOllamaText;
    [SerializeField] private LocalizedString useApiText;

    private AIConfig cfg;
    private string actualApiKey;

    private bool isApplyingOllamaModel = false;
    private bool isUpdatingFromEvent = false;
    private bool isInitialized = false;
    private bool isCheckingConnection = false;
    private bool isRevertingToggle = false;
    
    // 실행 중인 API 키 유효성 검사 코루틴을 저장하기 위한 변수
    private Coroutine apiKeyValidationCoroutine;

    private void OnEnable()
    {
        // 언어(로케일) 변경 이벤트를 구독합니다.
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        
        if (isInitialized)
        {
            // 패널이 활성화될 때마다 현재 설정에 맞는 텍스트로 새로고침하고 슬라이더 값을 업데이트합니다.
            UpdateToggleText(localModelToggle.isOn);
            UpdateSliderValue();
        }
        CameraManager.OnCameraZoom += UpdateSliderFromCameraZoom;
    }

    private void OnDisable()
    {
        // 패널이 비활성화될 때, 백그라운드에서 실행중인 API 검사가 있다면 중지시킵니다.
        if (apiKeyValidationCoroutine != null)
        {
            StopCoroutine(apiKeyValidationCoroutine);
            apiKeyValidationCoroutine = null;
        }
        CameraManager.OnCameraZoom -= UpdateSliderFromCameraZoom;

        // [추가] 패널이 비활성화될 때 구독했던 이벤트를 해지하여 메모리 누수를 방지합니다.
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");
        actualApiKey = APIKeyProvider.Get();
        apiKeyField.text = MaskApiKey(actualApiKey);

        apiKeyConfirmButton.onClick.AddListener(APIConfirmBtn);
        ollamaApplyButton.onClick.AddListener(OnClick_ApplyOllamaModel);

        bool useLocal = cfg.modelMode == ModelMode.OllamaHttp;
        localModelToggle.isOn = useLocal;
        UpdateInteractable(useLocal);
        localModelToggle.onValueChanged.AddListener(OnToggleChangeAIModel);

        apiKeyField.onSelect.AddListener(OnApiKeyFieldSelected);
        apiKeyField.onDeselect.AddListener(OnApiKeyFieldDeselected);
    }

    void Start()
    {
        if (zoomSlider == null || CameraManager.Instance == null) return;
        zoomSlider.minValue = CameraManager.Instance.MinCameraSize;
        zoomSlider.maxValue = CameraManager.Instance.MaxCameraSize;
        var config = SaveData.LoadAll()?.config;
        if (config != null)
        {
            CameraManager.Instance.SetZoomLevel(config.cameraZoomLevel);
        }
        UpdateSliderValue();
        zoomSlider.onValueChanged.AddListener(OnSliderValueChanged);
        if (soundVolumeSlider != null && UserData.Instance != null)
        {
            soundVolumeSlider.value = UserData.Instance.SystemVolume;
            soundVolumeSlider.onValueChanged.AddListener(OnSoundVolumeChanged);
        }
        
        if (alarmVolumeSlider != null && UserData.Instance != null)
        {
            alarmVolumeSlider.value = UserData.Instance.AlarmVolume;
            alarmVolumeSlider.onValueChanged.AddListener(OnAlarmVolumeChanged);
        }

        isInitialized = true;
    }

    private string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 5) return key;
        return key.Substring(0, 5) + new string('*', key.Length - 5);
    }

    private void OnApiKeyFieldSelected(string currentText) { apiKeyField.text = actualApiKey; }
    private void OnApiKeyFieldDeselected(string currentText)
    {
        actualApiKey = currentText;
        apiKeyField.text = MaskApiKey(actualApiKey);
    }

    public void OnSoundVolumeChanged(float value)
    {
        if (UserData.Instance != null)
        {
            UserData.Instance.SystemVolume = value;
            FindObjectOfType<SaveController>()?.SaveEverything();
        }
    }

    private void UpdateSliderValue()
    {
        if (zoomSlider != null && CameraManager.Instance != null)
        {
            isUpdatingFromEvent = true;
            float currentSize = CameraManager.Instance.CurrentCameraSize;
            float sliderValue = zoomSlider.maxValue - currentSize + zoomSlider.minValue;
            zoomSlider.value = sliderValue;
            isUpdatingFromEvent = false;
        }
    }

    private void OnSliderValueChanged(float value)
    {
        if (isUpdatingFromEvent) return;
        if (CameraManager.Instance != null)
        {
            float newSize = zoomSlider.maxValue - value + zoomSlider.minValue;
            CameraManager.Instance.SetZoomLevel(newSize);
        }
    }

    private void UpdateSliderFromCameraZoom(float oldSize, float newSize)
    {
        if (zoomSlider != null)
        {
            isUpdatingFromEvent = true;
            float sliderValue = zoomSlider.maxValue - newSize + zoomSlider.minValue;
            zoomSlider.value = sliderValue;
            isUpdatingFromEvent = false;
        }
    }

    public void APIConfirmBtn()
    {
        string key = apiKeyField.text;
        if (apiKeyField.text != MaskApiKey(actualApiKey))
            actualApiKey = apiKeyField.text;
        else
            key = actualApiKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            LocalizationManager.Instance.ShowWarning("API 유효");
            return;
        }

        // [수정] 이전에 실행된 코루틴이 있다면 중지하고 새로 시작합니다.
        if (apiKeyValidationCoroutine != null)
        {
            StopCoroutine(apiKeyValidationCoroutine);
        }
        apiKeyValidationCoroutine = StartCoroutine(CheckAPIKeyValid(key));
    }

    private async void OnToggleChangeAIModel(bool useLocal)
    {
        if (isRevertingToggle)
        {
            isRevertingToggle = false;
            return;
        }
        if (isCheckingConnection) return;

        if (useLocal)
        {
            // [수정] Ollama 모드로 전환할 때, 만약 API 키 검사가 실행중이었다면 중지시킵니다.
            if (apiKeyValidationCoroutine != null)
            {
                StopCoroutine(apiKeyValidationCoroutine);
                apiKeyValidationCoroutine = null;
                Debug.Log("Ollama 모드로 전환하여 진행 중인 API 키 유효성 검사를 중단했습니다.");
            }

            isCheckingConnection = true;
            bool isConnected = await OllamaClient.CheckConnectionAsync();
            isCheckingConnection = false;

            if (!isConnected)
            {
                LocalizationManager.Instance.ShowWarning("Ollama_Connection_Failed", null, 3.0f);
                isRevertingToggle = true;
                localModelToggle.isOn = false;
                return;
            }
        }

        cfg.modelMode = useLocal ? ModelMode.OllamaHttp : ModelMode.GeminiApi;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(cfg);
#endif
        UpdateInteractable(useLocal);

        var arguments = new Dictionary<string, object>
        {
            ["icon"] = useLocal ? "🔄" : "🌐",
            ["modelInfo"] = useLocal ? localModelInfoText.GetLocalizedString() : apiModelInfoText.GetLocalizedString()
        };
        LocalizationManager.Instance.ShowWarning("AIModel_Status_Template", arguments);
        Debug.Log(useLocal ? "Using Local Model" : "Using API");
    }

    private void UpdateInteractable(bool useLocal)
    {
        apiKeyGroup.SetActive(!useLocal);
        ollamaModelGroup.SetActive(useLocal);
        UpdateToggleText(useLocal);
        if (useLocal)
        {
            UpdateOllamaModelSettings();
        }
    }

    private async void UpdateToggleText(bool isUsingLocal)
    {
        if (localModelToggleText == null) return;
        LocalizedString targetString = isUsingLocal ? useApiText : useOllamaText;
        var handle = targetString.GetLocalizedStringAsync();
        await handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            localModelToggleText.text = handle.Result;
        }
    }

    private void UpdateOllamaModelSettings()
    {
        ollamaModelDropdown.ClearOptions();
        // [수정] "None" 옵션을 명시적으로 추가하여 항상 선택할 수 있도록 합니다.
        var displayOptions = new List<string> { "None" }; 

        if (cfg.ollamaModelNames != null && cfg.ollamaModelNames.Count > 0)
        {
            // "None" 이나 빈 문자열이 아닌 모델만 추가 리스트에 추가합니다.
            displayOptions.AddRange(cfg.ollamaModelNames.Where(m => !string.IsNullOrEmpty(m) && m != "None"));
        }
        
        ollamaModelDropdown.AddOptions(displayOptions);

        if(displayOptions.Count <= 1) // "None"만 있을 경우
        {
            ollamaModelDropdown.interactable = false;
        }
        else
        {
            ollamaModelDropdown.interactable = true;
        }

        int currentIndex = displayOptions.IndexOf(cfg.ollamaModelName);
        if (currentIndex > -1)
        {
            ollamaModelDropdown.value = currentIndex;
        }
        else
        {
            ollamaModelDropdown.value = 0; // 기본값 "None"
            cfg.ollamaModelName = "None";
        }
        ollamaModelDropdown.RefreshShownValue();
    }
    
    private async void OnClick_ApplyOllamaModel()
    {
        if (isApplyingOllamaModel || cfg.ollamaModelNames.Count == 0) return;
        isApplyingOllamaModel = true;
        ollamaApplyButton.interactable    = false;
        ollamaModelDropdown.interactable  = false;

        string selectedModel = ollamaModelDropdown.options[ollamaModelDropdown.value].text;

        if (selectedModel == cfg.ollamaModelName)
        {
            LocalizationManager.Instance.ShowWarning("Ollama_Model_Already");
            UnlockUI();
            return;
        }
        
        if (string.IsNullOrEmpty(selectedModel) || selectedModel == "None")
        {
            Debug.Log("적용할 Ollama 모델이 선택되지 않았습니다.");
            cfg.ollamaModelName = "None";
            LocalizationManager.Instance.ShowWarning("Ollama_Model_Applied");
            UnlockUI();
            return;
        }
        
        LocalizationManager.Instance.ShowWarning("Ollama_Applying", null, -1f);

        try
        {
            string response = await OllamaClient.AskAsync(
                selectedModel, new List<OllamaMessage>());

            if (response.Contains("Ollama Connection Error") ||
                response.Contains("Model Not Found"))
            {
                var args = new Dictionary<string, object> { ["ModelName"] = selectedModel };
                LocalizationManager.Instance.ShowWarning("Ollama_Model_Not_Found", args, 3f);

                // “None”으로 롤백
                int noneIndex = ollamaModelDropdown.options
                    .FindIndex(opt => opt.text == "None");
                if (noneIndex != -1)
                {
                    ollamaModelDropdown.value = noneIndex;
                    cfg.ollamaModelName = "None";
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(cfg);
#endif
                }
            }
            else
            {
                cfg.ollamaModelName = selectedModel;
                LocalizationManager.Instance.ShowWarning("Ollama_Model_Applied"); // 2) **완료** 알림
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(cfg);
#endif
            }
        }
        finally
        {
            UnlockUI();
        }
    }

    private void UnlockUI()
    {
        isApplyingOllamaModel = false;
        ollamaApplyButton.interactable = true;
        ollamaModelDropdown.interactable = true;
    }

    private IEnumerator CheckAPIKeyValid(string key)
    {
        string testUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={key}";
        string dummyPayload = @"{ ""contents"": [{ ""role"": ""user"", ""parts"": [{""text"": ""Hi""}] }] }";
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(dummyPayload);
        using (UnityWebRequest request = new UnityWebRequest(testUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            
            // 코루틴이 끝났으므로 저장된 핸들을 null로 초기화
            apiKeyValidationCoroutine = null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ API Key 유효: 저장합니다.");
                LocalizationManager.Instance.ShowWarning("API 적용");
                actualApiKey = key;
                apiKeyField.text = MaskApiKey(actualApiKey);
                UserData.Instance.SetAPIKey(key);
                APIKeyProvider.Set(key);
                cfg.modelMode = ModelMode.GeminiApi;
                localModelToggle.isOn = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(cfg);
#endif
            }
            else
            {
                Debug.LogWarning("❌ API Key가 유효하지 않습니다: " + request.responseCode);
                Debug.LogWarning("에러 메시지: " + request.downloadHandler.text);
                var arguments = new Dictionary<string, object> { ["errorCode"] = request.responseCode };
                LocalizationManager.Instance.ShowWarning("APIKey_Invalid", arguments, 3.0f);
                apiKeyField.text = MaskApiKey(actualApiKey);
            }
        }
    }

    public void PlayVolumeFeedbackSound()
    {
        // 메서드가 제대로 호출되는지 확인하기 위한 디버그 로그
        Debug.Log("PlayVolumeFeedbackSound() called."); 

        if (feedbackAudioSource != null && feedbackSoundClip != null)
        {
            feedbackAudioSource.volume = soundVolumeSlider.value;
            feedbackAudioSource.PlayOneShot(feedbackSoundClip);
            Debug.Log($"Feedback sound played at volume: {feedbackAudioSource.volume}");
        }
        else
        {
            if (feedbackAudioSource == null) Debug.LogWarning("Feedback Audio Source is not assigned in the Inspector.");
            if (feedbackSoundClip == null) Debug.LogWarning("Feedback Sound Clip is not assigned in the Inspector.");
        }
    }
    
    public void OnAlarmVolumeChanged(float value)
    {
        if (UserData.Instance != null)
        {
            UserData.Instance.AlarmVolume = value;
            FindObjectOfType<SaveController>()?.SaveEverything();
        }
    }
    
    public void PlayAlarmVolumeFeedbackSound()
    {
        if (feedbackAudioSource != null && feedbackSoundClip != null)
        {
            feedbackAudioSource.volume = alarmVolumeSlider.value;
            feedbackAudioSource.PlayOneShot(feedbackSoundClip);
        }
    }
    
    // 앱 시작 시 또는 외부에서 AI 모델 설정을 검증하고 복구하기 위한 공개 함수
    public async void ValidateAndRecoverModelSelection()
    {
        if (cfg == null)
        {
            cfg = Resources.Load<AIConfig>("AIConfig");
            // AIConfig 리소스를 찾지 못하는 심각한 경우에 대한 방어 코드
            if (cfg == null)
            {
                Debug.LogError("[ConfigPanel] AIConfig 파일을 Resources 폴더에서 찾을 수 없습니다! 복구 로직을 실행할 수 없습니다.");
                return;
            }
        }
        
        // 현재 설정된 모드가 Ollama일 때만 검사
        if (cfg.modelMode == ModelMode.OllamaHttp)
        {
            Debug.Log("[ConfigPanel] 저장된 Ollama 설정을 발견하여 연결 유효성 검사를 시작합니다.");
            bool isConnected = await OllamaClient.CheckConnectionAsync();
            if (!isConnected)
            {
                // 연결 실패 시 Gemini로 강제 전환
                cfg.modelMode = ModelMode.GeminiApi;
        
                // 사용자에게 경고 표시 (기존의 경고 로직 활용)
                var args = new Dictionary<string, object> { ["ModelName"] = "Ollama" };
            
                // [수정] LocalizationManager.Instance가 null이 아닌지 확인 후 호출
                if (LocalizationManager.Instance != null)
                {
                    LocalizationManager.Instance.ShowWarning("Ollama_Connection_Failed", args, 4.0f); // 실패 메시지 표시
                }
                else
                {
                    Debug.LogError("[ConfigPanel] LocalizationManager가 초기화되지 않아 경고 메시지를 표시할 수 없습니다.");
                }
        
                Debug.LogWarning("[ConfigPanel] Ollama 연결에 실패하여 Gemini API 모드로 자동 전환합니다.");
            }
        }

        // 현재 AIConfig 상태에 맞게 UI를 최종적으로 업데이트
        UpdateToggleStateFromConfig();
    }

// 현재 AIConfig의 상태를 UI 토글에 정확하게 반영하는 함수
    public void UpdateToggleStateFromConfig()
    {
        bool useLocal = (cfg.modelMode == ModelMode.OllamaHttp);
    
        // UI 이벤트가 불필요하게 실행되는 것을 방지하기 위해 SetIsOnWithoutNotify 사용
        localModelToggle.SetIsOnWithoutNotify(useLocal);
    
        // 토글 상태에 따라 나머지 UI 요소(API키 입력, 모델 드롭다운)의 활성화 상태를 업데이트
        UpdateInteractable(useLocal);

        Debug.Log($"[ConfigPanel] AI 모델 UI가 현재 설정({cfg.modelMode})에 맞게 업데이트 되었습니다.");
    }
    

    /// <summary>
    /// 전역 언어 설정이 변경되었을 때 호출되는 이벤트 핸들러입니다.
    /// </summary>
    /// <param name="newLocale">새롭게 선택된 로케일 정보</param>
    private void OnLocaleChanged(Locale newLocale)
    {
        // 현재 토글 상태에 맞는 텍스트를 새로운 언어로 다시 불러옵니다.
        UpdateToggleText(localModelToggle.isOn);
    }
}