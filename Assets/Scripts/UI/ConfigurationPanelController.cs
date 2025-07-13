using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;

public class ConfigurationPanelController : MonoBehaviour
{
    [SerializeField] TMP_InputField apiKeyField;

    [Header("UI 연결")]
    [Tooltip("전체 줌 레벨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private Toggle localModelToggle;
    [Tooltip("전체 사운드 볼륨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider soundVolumeSlider;
    
    [Header("Localization")]
    [SerializeField] private LocalizedString localModelInfoText; // AIModel_Info_Local 키 연결
    [SerializeField] private LocalizedString apiModelInfoText;   // AIModel_Info_API 키 연결


    private AIConfig cfg;
    private string actualApiKey; // 실제 API 키를 저장할 변수

    // 슬라이더와 이벤트 간의 무한 루프를 방지하기 위한 플래그
    private bool isUpdatingFromEvent = false;
    private bool isInitialized = false; // 초기화가 완료되었는지 확인하는 플래그

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
        CameraManager.OnCameraZoom -= UpdateSliderFromCameraZoom;
    }

    void Awake()
    {
        cfg = Resources.Load<AIConfig>("AIConfig");

        // 실제 API 키를 불러옵니다.
        actualApiKey = APIKeyProvider.Get();
        // 마스킹 처리된 키를 UI에 표시합니다.
        apiKeyField.text = MaskApiKey(actualApiKey);

        bool useLocal = cfg.modelMode == ModelMode.GemmaLocal;
        localModelToggle.isOn = useLocal;
        UpdateInteractable(useLocal);
        localModelToggle.onValueChanged.AddListener(OnToggleChangeAIModel);

        // 사용자가 입력을 시작하면 실제 키를 보여주고, 입력이 끝나면 다시 마스킹합니다.
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

    /// <summary>
    /// API 키를 마스킹 처리하는 함수
    /// </summary>
    /// <param name="key">마스킹할 API 키</param>
    /// <returns>마스킹 처리된 문자열</returns>
    private string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 5)
        {
            return key;
        }
        // 문자열의 앞 5자리를 제외한 나머지를 '*'로 채웁니다.
        return key.Substring(0, 5) + new string('*', key.Length - 5);
    }

    // API 키 입력 필드를 선택했을 때 호출될 함수
    private void OnApiKeyFieldSelected(string currentText)
    {
        // 필드를 선택하면 실제 API 키를 보여줍니다.
        apiKeyField.text = actualApiKey;
    }

    // API 키 입력 필드 선택이 해제되었을 때 호출될 함수
    private void OnApiKeyFieldDeselected(string currentText)
    {
        // 입력이 완료되면 실제 키를 업데이트하고 다시 마스킹 처리합니다.
        actualApiKey = currentText;
        apiKeyField.text = MaskApiKey(actualApiKey);
    }

    public void OnSoundVolumeChanged(float value)
    {
        if (UserData.Instance == null) return;

        UserData.Instance.SystemVolume = value;
        
        var saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
            Debug.Log($"[ConfigPanel] 시스템 볼륨 변경({value}) 후 전체 저장 요청 완료.");
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
        // 사용자가 입력 필드에서 수정한 최신 키 값을 가져옵니다.
        string key = apiKeyField.text;

        // 사용자가 필드를 클릭하지 않고 바로 버튼을 누를 경우를 대비해,
        // 마스킹되지 않은 실제 키(actualApiKey)와 비교하여 변경되었는지 확인합니다.
        if (apiKeyField.text != MaskApiKey(actualApiKey))
        {
             actualApiKey = apiKeyField.text;
        }
        else
        {
            key = actualApiKey; // 마스킹된 상태라면 실제 키를 사용
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            LocalizationManager.Instance.ShowWarning("API 유효");
            return;
        }

        StartCoroutine(CheckAPIKeyValid(key));
    }

    private void OnToggleChangeAIModel(bool useLocal)
    {
        cfg.modelMode = useLocal ? ModelMode.OllamaHttp : ModelMode.GeminiApi;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(cfg);
#endif
        UpdateInteractable(useLocal);
        // [핵심] Smart String에 전달할 인자(arguments) 딕셔너리를 만듭니다.
        var arguments = new Dictionary<string, object>
        {
            // 1. "{icon}" 변수에 들어갈 이모지를 설정합니다.
            ["icon"] = useLocal ? "🔄" : "🌐",

            // 2. "{modelInfo}" 변수에 들어갈 '현지화된 텍스트'를 설정합니다.
            // GetLocalizedString()를 호출하여 현재 언어에 맞는 텍스트를 즉시 가져옵니다.
            ["modelInfo"] = useLocal ? localModelInfoText.GetLocalizedString() : apiModelInfoText.GetLocalizedString()
        };

        // 3. 템플릿의 이름표("AIModel_Status_Template")와 인자(arguments)를 함께 전달합니다.
        LocalizationManager.Instance.ShowWarning("AIModel_Status_Template", arguments);

        Debug.Log(useLocal ? "Using Local Model" : "Using API");
    }

    private void UpdateInteractable(bool useLocal)
    {
        apiKeyField.interactable = !useLocal;
    }

    private IEnumerator CheckAPIKeyValid(string key)
    {
        string testUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={key}";

        string dummyPayload = @"{
            ""contents"": [{
                ""role"": ""user"",
                ""parts"": [{""text"": ""Hi""}]
            }]
        }";

        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(dummyPayload);
        UnityWebRequest request = new UnityWebRequest(testUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(jsonBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("✅ API Key 유효: 저장합니다.");
            LocalizationManager.Instance.ShowWarning("API 적용");
            
            // 실제 키와 마스킹 처리를 업데이트합니다.
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

            // [핵심 수정 부분]
            // 1. Smart String에 전달할 인자 딕셔너리를 만듭니다.
            var arguments = new Dictionary<string, object>
            {
                // String Table에 정의한 변수명 "{errorCode}"에 실제 에러 코드를 값으로 넣어줍니다.
                ["errorCode"] = request.responseCode
            };

            // 2. LocalizationManager를 호출할 때, 메시지 키와 함께 인자 딕셔너리를 전달합니다.
            LocalizationManager.Instance.ShowWarning("APIKey_Invalid", arguments, 3.0f); // 3초간 표시

            // 유효하지 않은 경우, UI를 마스킹 처리된 상태로 되돌립니다.
            apiKeyField.text = MaskApiKey(actualApiKey);
        }
    }
}