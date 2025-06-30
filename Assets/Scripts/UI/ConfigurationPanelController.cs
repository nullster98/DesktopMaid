using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class ConfigurationPanelController : MonoBehaviour
{
    [SerializeField] TMP_InputField apiKeyField;
    
    [Header("UI 연결")]
    [Tooltip("전체 줌 레벨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider zoomSlider;
    [SerializeField] private Toggle localModelToggle;
    [Tooltip("전체 사운드 볼륨을 조절할 UI 슬라이더")]
    [SerializeField] private Slider soundVolumeSlider;

    private AIConfig cfg;
    
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

        string savedKey = APIKeyProvider.Get();
        apiKeyField.text = savedKey;          // TMP_InputField

        bool useLocal = cfg.modelMode == ModelMode.GemmaLocal;
        localModelToggle.isOn = useLocal;
        UpdateInteractable(useLocal);             // 입력칸/버튼 잠금
        localModelToggle.onValueChanged.AddListener(OnToggleChangeAIModel);
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
            // 슬라이더의 초기값을 UserData의 실시간 볼륨 값으로 설정
            soundVolumeSlider.value = UserData.Instance.SystemVolume;
            // 슬라이더 값이 변경될 때마다 OnSoundVolumeChanged 함수를 호출하도록 연결
            soundVolumeSlider.onValueChanged.AddListener(OnSoundVolumeChanged);
        }
        
        isInitialized = true;
    }
    
    public void OnSoundVolumeChanged(float value)
    {
        if (UserData.Instance == null) return;

        // 1. UserData의 실시간 볼륨 값을 업데이트합니다.
        UserData.Instance.SystemVolume = value;
        
        // 2. 직접 저장하는 대신, SaveController에게 저장을 요청합니다.
        var saveController = FindObjectOfType<SaveController>();
        if (saveController != null)
        {
            saveController.SaveEverything();
            Debug.Log($"[ConfigPanel] 시스템 볼륨 변경({value}) 후 전체 저장 요청 완료.");
        }
    }
    
    // 슬라이더 값을 현재 카메라 값으로 업데이트하는 헬퍼 함수
    private void UpdateSliderValue()
    {
        if (zoomSlider != null && CameraManager.Instance != null)
        {
            isUpdatingFromEvent = true;
            // [핵심 수정 2] 현재 카메라 Size를 반전된 슬라이더 값으로 변환합니다.
            float currentSize = CameraManager.Instance.CurrentCameraSize;
            float sliderValue = zoomSlider.maxValue - currentSize + zoomSlider.minValue;
            zoomSlider.value = sliderValue;
            isUpdatingFromEvent = false;
        }
    }

    // 슬라이더를 움직였을 때 호출될 함수
    private void OnSliderValueChanged(float value)
    {
        if (isUpdatingFromEvent) return;

        if (CameraManager.Instance != null)
        {
            // [핵심 수정 3] 슬라이더 값을 반전시켜 카메라 Zoom Level로 설정합니다.
            // 슬라이더가 최대일 때(value = maxValue) -> newSize는 minValue가 됨
            // 슬라이더가 최소일 때(value = minValue) -> newSize는 maxValue가 됨
            float newSize = zoomSlider.maxValue - value + zoomSlider.minValue;
            CameraManager.Instance.SetZoomLevel(newSize);
        }
    }

    // Ctrl+휠 등으로 카메라 줌이 변경되었을 때 호출될 함수 (이벤트 수신)
    private void UpdateSliderFromCameraZoom(float oldSize, float newSize)
    {
        if (zoomSlider != null)
        {
            isUpdatingFromEvent = true;
            // [핵심 수정 4] 외부에서 변경된 newSize도 반전시켜 슬라이더에 반영합니다.
            float sliderValue = zoomSlider.maxValue - newSize + zoomSlider.minValue;
            zoomSlider.value = sliderValue;
            isUpdatingFromEvent = false;
        }
    }

    public void APIConfirmBtn()
    {
        string key = apiKeyField.text;
        
        if (string.IsNullOrWhiteSpace(key))
        {
            UIManager.instance.TriggerWarning("⚠️ API 키를 입력해주세요!");
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
        string msg = useLocal ? "🔄 Ollama 로컬 모델 사용" : "🌐 API 사용";
        UIManager.instance.TriggerWarning(msg);

        Debug.Log(msg);
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
            UIManager.instance.TriggerWarning("API 키 적용 완료");
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
            
            UIManager.instance.TriggerWarning("❌ API 키가 유효하지 않습니다! \n ErrorCode : " + request.responseCode);
        }
    }
}