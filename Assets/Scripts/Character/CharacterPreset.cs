// --- START OF FILE CharacterPreset.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum CharacterMode
{
    Off = 0,       // 모든 채팅 기능 불가능
    Activated = 1, // 모든 기능 사용 가능 (자율 행동 포함)
    Sleep = 2,     // 1:1, 그룹 채팅만 가능 (자율 행동 비활성화)
}

public class CharacterPreset : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // [수정 1-1] VRM 상태(표시, 자동이동)가 변경되었음을 알리는 이벤트 선언
    public event Action OnVrmStateChanged;
    
    public GameObject vrmModel;
    public float sittingOffsetY;
    public GameObject settingIcon;
    public GameObject notifyImage;
    public Image vrmModeIcon;
    
    public string vrmFilePath;
    public string voiceFolder = "Female1";
    public ChatUI chatUI;

    [SerializeField] private TMP_Text name;
    [SerializeField] private TMP_Text Message;
    
    public LocalizedString localizedName;

    public string presetID;
    public string groupID;
    public string characterName;
    public string onMessage;
    public string sleepMessage;
    public string offMessage;
    public string gender;
    public string personality;
    public string characterSetting;
    public Image characterImage;
    public Image modeImage;
    public List<string> dialogueExample = new();

    public CharacterMode CurrentMode { get; private set; } = CharacterMode.Off;

    public bool isVrmVisible { get; private set; } = false;
    public bool isAutoMoveEnabled { get; private set; } = false;

    public string iQ;
    public string intimacy;
    public float internalIntimacyScore;
    public int ignoreCount = 0;
    public bool hasResponded = false;
    public bool hasSaidFarewell = false;

    [Header("자율 행동 설정")]
    [Tooltip("답장 대기 상태에서 몇 초 후에 '무시'로 간주할지 설정합니다.")]
    public float ignoreCheckTime = 180f; // 기본 3분
    [Tooltip("최대 무시 횟수. 이 횟수를 넘어가면 AI는 말을 거는 것을 포기합니다.")]
    public int maxIgnoreCount = 3;
    private Coroutine ignoredRoutine; // 무시 처리 코루틴 핸들러

    [Header("기억 저장소")] 
    public string currentContextSummary;
    public List<string> longTermMemories = new List<string>(); // 장기 기억 (요약문 리스트)
    public Dictionary<string, string> knowledgeLibrary = new Dictionary<string, string>(); // 초장기 기억 (Key-Value)
    public int lastSummarizedMessageId = 0; // 개인 대화 요약 위치 추적
    
    [SerializeField] private bool _isWaitingForReply = false;
    
    private AIConfig _aiConfig;
    public bool isWaitingForReply
    {
        get { return _isWaitingForReply; }
        set
        {
            if (_isWaitingForReply == value) return;

            _isWaitingForReply = value;

            if (!_isWaitingForReply)
            {
                if (ignoredRoutine != null)
                {
                    StopCoroutine(ignoredRoutine);
                    ignoredRoutine = null;
                }
            }
        }
    }
    
    public void StartWaitingForReply()
    {
        this.isWaitingForReply = true; 

        var observer = FindObjectOfType<AIScreenObserver>();
        if (observer != null && observer.selfAwarenessModuleEnabled &&
            UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.On)
        {
            if (ignoredRoutine != null) StopCoroutine(ignoredRoutine);
            ignoredRoutine = StartCoroutine(CheckIfIgnoredCoroutine());
        }
    }

    private void Awake()
    {
        _aiConfig = Resources.Load<AIConfig>("AIConfig");
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }
    
    public void Start()
    {
        CurrentMode = CharacterMode.Off;
        Message.text = offMessage;
        UpdateToggleSprite(UIManager.instance.modeOffSprite);
        
        isVrmVisible = false;
        isAutoMoveEnabled = false;

        if (presetID == "DefaultPreset" && !localizedName.IsEmpty)
        {
            StartCoroutine(UpdateLocalizedName());
        }
    }
    
    // --- 이하 친밀도, 코루틴, 데이터 적용 관련 함수들은 변경 없음 ---
    #region Unchanged Methods
    private void OnLocaleChanged(Locale newLocale)
    {
        if (presetID == "DefaultPreset" && !localizedName.IsEmpty)
        {
            StartCoroutine(UpdateLocalizedName());
        }
    }
    
    public void ApplyIntimacyChange(float delta)
    {
        this.internalIntimacyScore = Mathf.Clamp(this.internalIntimacyScore + delta, -100f, 100f);
        UpdateIntimacyStringValue();
        Debug.Log($"[Intimacy] '{characterName}'의 친밀도 변경: {delta:F1}. 현재 점수: {internalIntimacyScore:F1}, UI 레벨: {intimacy}");
    }
    
    public void SetIntimacyFromString(string level)
    {
        this.intimacy = level;
        switch (level)
        {
            case "1": this.internalIntimacyScore = -90f; break;
            case "2": this.internalIntimacyScore = -70f; break;
            case "3": this.internalIntimacyScore = -50f; break;
            case "4": this.internalIntimacyScore = -30f; break;
            case "5": this.internalIntimacyScore = -10f; break;
            case "6": this.internalIntimacyScore = 10f; break;
            case "7": this.internalIntimacyScore = 30f; break;
            case "8": this.internalIntimacyScore = 50f; break;
            case "9": this.internalIntimacyScore = 70f; break;
            case "10": this.internalIntimacyScore = 90f; break;
            default:  this.internalIntimacyScore = 0f;   break;
        }
        Debug.Log($"[Intimacy] '{characterName}'의 친밀도를 수동으로 설정. UI 레벨: {intimacy}, 내부 점수: {internalIntimacyScore:F1}");
    }
    
    public void UpdateIntimacyStringValue()
    {
        if      (internalIntimacyScore >= 81f)  this.intimacy = "10";
        else if (internalIntimacyScore >= 61f)  this.intimacy = "9";
        else if (internalIntimacyScore >= 41f)  this.intimacy = "8";
        else if (internalIntimacyScore >= 21f)  this.intimacy = "7";
        else if (internalIntimacyScore >= 1f)   this.intimacy = "6";
        else if (internalIntimacyScore >= -20f) this.intimacy = "5";
        else if (internalIntimacyScore >= -40f) this.intimacy = "4";
        else if (internalIntimacyScore >= -60f) this.intimacy = "3";
        else if (internalIntimacyScore >= -80f) this.intimacy = "2";
        else                                    this.intimacy = "1";
    }

    private IEnumerator CheckIfIgnoredCoroutine()
    {
        Debug.Log($"[CharacterPreset] '{characterName}'가 응답 대기 시작. {ignoreCheckTime}초 후 무시로 간주합니다.");
        yield return new WaitForSeconds(ignoreCheckTime);

        if (this.isWaitingForReply)
        {
            Debug.LogWarning($"[CharacterPreset] '{characterName}'가 사용자의 답장을 {ignoreCheckTime}초 동안 받지 못했습니다. 무시 처리 시작.");
            
            var observer = FindObjectOfType<AIScreenObserver>();
            if (observer != null)
            {
                observer.TriggerIgnoredResponse(this);
            }
        }
        ignoredRoutine = null;
    }
    
    private IEnumerator UpdateLocalizedName()
    {
        var handle = localizedName.GetLocalizedStringAsync();
        yield return handle;
        
        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            characterName = handle.Result;
            SetProfile();
        }
    }
    
    public void ApplyData(CharacterPresetData data)
    {
        characterName = data.name;
        onMessage = data.onMessage;
        sleepMessage = data.sleepMessage;
        offMessage = data.offMessage;
        gender = data.gender;
        personality = data.personality;
        characterSetting = data.setting;
        iQ = data.iq;
        sittingOffsetY = data.sittingOffsetY;
        dialogueExample.Clear();
        if(data.dialogueExamples != null)
            dialogueExample.AddRange(data.dialogueExamples);
            
        isVrmVisible = false;
        isAutoMoveEnabled = false;
    }

    public void ApplyData(SaveCharacterPresetData data)
    {
        characterName = data.name;
        onMessage = data.onMessage;
        sleepMessage = data.sleepMessage;
        offMessage = data.offMessage;
        gender = data.gender;
        personality = data.personality;
        characterSetting = data.setting;
        iQ = data.iq;
        this.intimacy = data.intimacy;
        this.internalIntimacyScore = data.internalIntimacyScore;
        sittingOffsetY = data.sittingOffsetY;
        dialogueExample.Clear();
        dialogueExample.AddRange(data.dialogueExamples);
        
        isVrmVisible = data.isVrmVisible;
        isAutoMoveEnabled = data.isAutoMoveEnabled;
    }
    
    private void UpdateToggleSprite(Sprite sprite)
    {
        Transform toggleBtn = transform.Find("conditionBtn");
        if (toggleBtn != null)
        {
            var image = toggleBtn.GetComponent<Image>();
            if (image != null) image.sprite = sprite;
        }
    }
    #endregion

    public void ToggleVrmVisibility()
    {
        isVrmVisible = !isVrmVisible;

        if (isVrmVisible)
        {
            var manager = FindObjectOfType<CharacterPresetManager>();
            if (manager != null) manager.SetCurrentPreset(this);

            if (vrmModel != null && vrmModel.scene.IsValid())
            {
                vrmModel.SetActive(true);
            }
            else if (vrmModel != null && !vrmModel.scene.IsValid())
            {
                var loader = FindObjectOfType<LoadNewVRM>();
                if (loader != null)
                {
                    GameObject model = loader.InstantiateFromPreset(this);
                    if (model != null) { model.SetActive(true); }
                }
            }
            else if (!string.IsNullOrEmpty(vrmFilePath) && File.Exists(vrmFilePath))
            {
                var loader = FindObjectOfType<LoadNewVRM>();
                if (loader != null)
                {
                    loader.LoadVRM(vrmFilePath, (model) =>
                    {
                        if (model != null && isVrmVisible) { model.SetActive(true); }
                    });
                }
            }
        }
        else
        {
            if (vrmModel != null && vrmModel.scene.IsValid())
            {
                vrmModel.SetActive(false);
            }
        }
        
        vrmModeIcon.sprite = isVrmVisible ? UIManager.instance.vrmOnSprite : UIManager.instance.vrmOffSprite;
        
        // [수정 1-2] 상태가 변경되었음을 구독자들에게 알림
        OnVrmStateChanged?.Invoke();
    }

    public void ToggleAutoMove()
    {
        isAutoMoveEnabled = !isAutoMoveEnabled;
        
        if (!isAutoMoveEnabled && vrmModel != null)
        {
            var autoActivate = vrmModel.transform.root.GetComponent<VRMAutoActivate>();
            if (autoActivate != null)
            {
                autoActivate.StopWalking();
            }
        }
        
        // [수정 1-3] 상태가 변경되었음을 구독자들에게 알림
        OnVrmStateChanged?.Invoke();
    }
    
    public void CycleCharacterMode()
    {
        int nextMode = ((int)CurrentMode + 1) % 3;
        CurrentMode = (CharacterMode)nextMode;
        
        switch (CurrentMode)
        {
            case CharacterMode.Activated:
                Message.text = onMessage;
                UpdateToggleSprite(UIManager.instance.modeOnSprite);
                Debug.Log($"'{characterName}' 상태 변경: Activated (모든 기능 활성화)");
                break;
            case CharacterMode.Sleep:
                Message.text = sleepMessage;
                UpdateToggleSprite(UIManager.instance.modeSleepSprite);
                Debug.Log($"'{characterName}' 상태 변경: Sleep (자율 행동 비활성화)");
                break;
            case CharacterMode.Off:
                Message.text = offMessage;
                UpdateToggleSprite(UIManager.instance.modeOffSprite);
                Debug.Log($"'{characterName}' 상태 변경: Off (모든 채팅 비활성화)");
                break;
        }
    }

    #region Unchanged UI/Interaction Methods
    public void ChatBtn()
    {
        var manager = FindObjectOfType<CharacterPresetManager>();
        if (manager == null) return;
        manager.ActivatePreset(this);
    }
    
    public void OnClickPresetButton()
    {
        if (CurrentMode == CharacterMode.Off)
        {
            UIManager.instance.TriggerWarning($"'{characterName}'님은 현재 오프라인 상태입니다.");
            return;
        }
        
        if (_aiConfig.modelMode == ModelMode.GeminiApi)
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey)) 
            { 
                UIManager.instance.TriggerWarning("온라인 AI 모델을 사용하려면 API 키를 먼저 입력해야 합니다!"); 
                return; 
            }
        }

        if (string.IsNullOrWhiteSpace(characterSetting)) 
        { 
            UIManager.instance.charWarningBox.SetActive(true); 
            return; 
        }

        ChatFunction.CharacterSession.SetPreset(presetID);
        ChatUI[] allUIs = Resources.FindObjectsOfTypeAll<ChatUI>();
        foreach (var ui in allUIs)
        {
            if (ui.presetID == this.presetID && ui.gameObject.scene.IsValid())
            {
                ui.SetupForPresetChat(this);
                var canvasGroup = ui.GetComponent<CanvasGroup>();
                if (canvasGroup != null) 
                { 
                    canvasGroup.alpha = 1f; 
                    canvasGroup.interactable = true; 
                    canvasGroup.blocksRaycasts = true; 
                }
                ui.transform.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                var chatFunc = ui.GetComponent<ChatFunction>();
                if (notifyImage != null) { notifyImage.SetActive(false); }
                return;
            }
        }
    }

    public void SetProfile()
    {
        if (name == null || Message == null) return;
        name.text = characterName;
        Message.text = offMessage;
    }

    public void OnPointerEnter(PointerEventData eventData) { settingIcon.SetActive(true); }
    public void OnPointerExit(PointerEventData eventData) { settingIcon.SetActive(false); }
    public bool IsModelActive() { return vrmModel != null && vrmModel.activeSelf; }
    #endregion
}