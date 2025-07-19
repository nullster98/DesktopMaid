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

    [Header("Localization")]
    [SerializeField] private LocalizedString localModelInfoText;
    [SerializeField] private LocalizedString apiModelInfoText;
    [SerializeField] private LocalizedString useOllamaText;
    [SerializeField] private LocalizedString useApiText;

    private AIConfig cfg;
    private string actualApiKey;

    private bool isUpdatingFromEvent = false;
    private bool isInitialized = false;
    private bool isCheckingConnection = false;
    private bool isRevertingToggle = false;
    
    // [추가] 실행 중인 API 키 유효성 검사 코루틴을 저장하기 위한 변수
    private Coroutine apiKeyValidationCoroutine;

    private void OnEnable()
    {
        if (isInitialized)
        {
            UpdateSliderValue();
        }
        CameraManager.OnCameraZoom += UpdateSliderFromCameraZoom;
    }

    private void OnDisable()
    {
        // [수정] 패널이 비활성화될 때, 백그라운드에서 실행중인 API 검사가 있다면 중지시킵니다.
        if (apiKeyValidationCoroutine != null)
        {
            StopCoroutine(apiKeyValidationCoroutine);
            apiKeyValidationCoroutine = null;
        }
        CameraManager.OnCameraZoom -= UpdateSliderFromCameraZoom;
    }

    void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");
        actualApiKey = APIKeyProvider.Get();
        apiKeyField.text = MaskApiKey(actualApiKey);

        apiKeyConfirmButton.onClick.AddListener(APIConfirmBtn);
        ollamaApplyButton.onClick.AddListener(() => OnClick_ApplyOllamaModel().Forget());

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
        if (cfg.ollamaModelNames == null || cfg.ollamaModelNames.Count == 0)
        {
            ollamaModelDropdown.AddOptions(new List<string> { "모델 없음" });
            ollamaModelDropdown.interactable = false;
            return;
        }
        ollamaModelDropdown.interactable = true;
        ollamaModelDropdown.AddOptions(cfg.ollamaModelNames);
        int currentIndex = cfg.ollamaModelNames.IndexOf(cfg.ollamaModelName);
        if (currentIndex > -1)
        {
            ollamaModelDropdown.value = currentIndex;
        }
        else
        {
            ollamaModelDropdown.value = 0;
            if (cfg.ollamaModelNames.Any())
                cfg.ollamaModelName = cfg.ollamaModelNames[0];
        }
        ollamaModelDropdown.RefreshShownValue();
    }
    
    private async UniTaskVoid OnClick_ApplyOllamaModel()
    {
        if (cfg.ollamaModelNames.Count == 0) return;

        string selectedModel = ollamaModelDropdown.options[ollamaModelDropdown.value].text;

        string response = await OllamaClient.AskAsync(selectedModel, new List<OllamaMessage>());

        if (response.Contains("Ollama 연결 오류") || response.Contains("모델을 찾을 수 없습니다"))
        {
            var args = new Dictionary<string, object> { ["ModelName"] = selectedModel };
            LocalizationManager.Instance.ShowWarning("Ollama_Model_Not_Found", args, 3.0f);
            return;
        }
        
        cfg.ollamaModelName = selectedModel;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(cfg);
        Debug.Log($"Ollama 모델이 '{selectedModel}' (으)로 변경 및 저장되었습니다.");
#endif

        LocalizationManager.Instance.ShowWarning("Ollama_Model_Applied");
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
}