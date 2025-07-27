// --- START OF FILE CharacterPreset.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions; // 정규식을 사용하기 위해 이 줄이 필요합니다.
using AI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum CharacterMode
{
    Off = 0,
    Activated = 1,
    Sleep = 2,
}

public class CharacterPreset : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
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
    [Tooltip("최대 무시 횟수. 이 횟수를 넘어가면 AI는 말을 거는 것을 포기합니다.")]
    public int maxIgnoreCount = 2;
    private Coroutine ignoredRoutine;

    [Header("기억 저장소")] 
    public string currentContextSummary;
    public List<string> longTermMemories = new List<string>();
    public Dictionary<string, string> knowledgeLibrary = new Dictionary<string, string>();
    public int lastSummarizedMessageId = 0;
    
    [SerializeField] private bool _isWaitingForReply = false;

    public long creationTimestamp;
    [Header("상태")] 
    public GameObject lockOverlay;
    public bool isLocked { get; private set; } = false;
    
    private bool _wasVrmVisibleBeforeLock;
    private bool _wasAutoMoveEnabledBeforeLock;
    private CharacterMode _modeBeforeLock;
    
    public bool IsInAlarmState { get; private set; } = false;
    
    private VRMAutoActivate _vrmAutoActivate;
    private SnapAwareVRM _snapAwareVRM;
    
    private AIConfig _aiConfig;
    public bool isWaitingForReply
    {
        get { return _isWaitingForReply; }
        set
        {
            if (_isWaitingForReply == value) return;
            _isWaitingForReply = value;
            if (!_isWaitingForReply && ignoredRoutine != null)
            {
                StopCoroutine(ignoredRoutine);
                ignoredRoutine = null;
            }
        }
    }
    
    public void StartWaitingForReply()
    {
        this.isWaitingForReply = true; 
        var observer = FindObjectOfType<AIScreenObserver>();
        if (observer != null && observer.selfAwarenessModuleEnabled && UserData.Instance?.CurrentUserMode == UserMode.On)
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
        SetProfile();
        
        if (presetID == "DefaultPreset" && !localizedName.IsEmpty)
        {
            StartCoroutine(UpdateLocalizedName());
        }
    }
    
    private void OnLocaleChanged(Locale newLocale)
    {
        if (presetID == "DefaultPreset" && !localizedName.IsEmpty)
        {
            StartCoroutine(UpdateLocalizedName());
        }
    }
    
    public void SetupVRMComponents(GameObject vrmRootObject)
    {
        Debug.Log($"[{gameObject.name}] SetupVRMComponents가 '{vrmRootObject.name}' 오브젝트로 호출되었습니다.");
        
        if (vrmRootObject != null)
        {
            _vrmAutoActivate = vrmRootObject.GetComponentInChildren<VRMAutoActivate>(true);
            _snapAwareVRM = vrmRootObject.GetComponentInChildren<SnapAwareVRM>(true);

            if (_vrmAutoActivate != null)
                Debug.Log($"<color=green>SUCCESS:</color> 'VRMAutoActivate'를 찾았습니다.");
            else
                Debug.LogError($"<color=red>FAILED:</color> 'VRMAutoActivate'를 찾지 못했습니다.");
            
            if (_snapAwareVRM != null)
                Debug.Log($"<color=green>SUCCESS:</color> 'SnapAwareVRM'를 찾았습니다.");
            else
                Debug.LogWarning($"<color=yellow>INFO:</color> 'SnapAwareVRM'를 찾지 못했습니다.");
        }
    }
    
    public void StartAlarmBehavior()
    {
        if (IsInAlarmState) return;
        IsInAlarmState = true;
        Debug.Log($"[{characterName}] 알람 동작 시작 신호를 보냅니다.");

        _vrmAutoActivate?.SetAlarmState(true);
        _snapAwareVRM?.SetAlarmState(true);
    }
    
    public void StopAlarmBehavior()
    {
        if (!IsInAlarmState) return;
        IsInAlarmState = false;
        Debug.Log($"[{characterName}] 알람 동작 종료 신호를 보냅니다.");

        _vrmAutoActivate?.SetAlarmState(false);
        _snapAwareVRM?.SetAlarmState(false);
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
        float waitTime = (this.ignoreCount + 1) * 300.0f; 
        Debug.Log($"[CharacterPreset] '{characterName}'가 응답 대기 시작. {waitTime}초 후 무시로 간주합니다. (현재 무시 횟수: {this.ignoreCount})");
        yield return new WaitForSeconds(waitTime);
        if (this.isWaitingForReply)
        {
            Debug.LogWarning($"[CharacterPreset] '{characterName}'가 사용자의 답장을 {waitTime}초 동안 받지 못했습니다. 무시 처리 시작.");
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
            var settingPanel = FindObjectOfType<SettingPanelController>(true);
            if (settingPanel != null && settingPanel.targetPreset == this)
            {
                settingPanel.LoadPresetToUI();
            }
            var groupPanel = FindObjectOfType<GroupPanelController>(true);
            if (groupPanel != null)
            {
                groupPanel.RefreshGroupListUI();
            }
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
        this.creationTimestamp = data.creationTimestamp;
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
        this.creationTimestamp = data.creationTimestamp;
        
        // 저장된 데이터로부터 CharacterMode(컨디션)를 불러옵니다.
        this.CurrentMode = (CharacterMode)data.currentMode;
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
    
    // [수정] isVrmVisible 값을 직접 바꾸지 않고, 상태를 토글하는 전용 함수를 사용합니다.
    // 이는 이벤트(OnVrmStateChanged) 호출을 보장합니다.
    public void SetVrmVisible(bool visible)
    {
        if (isVrmVisible == visible) return;
        ToggleVrmVisibility();
    }

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
        vrmModeIcon.sprite = isVrmVisible ? UIManager.instance.vrmVisibleSprite : UIManager.instance.vrmInvisibleSprite;
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
        OnVrmStateChanged?.Invoke();
    }
    
    public void CycleCharacterMode()
    {
        if (string.IsNullOrWhiteSpace(characterSetting)) 
        { 
            UIManager.instance.ShowConfirmationWarning(ConfirmationType.CharacterSetting);
            return; 
        }
        
        int nextMode = ((int)CurrentMode + 1) % 3;
        CurrentMode = (CharacterMode)nextMode;
        
        // [수정] SetProfile을 호출하여 UI를 업데이트하도록 로직 통합
        SetProfile();
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.UpdateItemUI(this.presetID);
        }
    }

    public void ChatBtn()
    {
        var manager = FindObjectOfType<CharacterPresetManager>();
        if (manager == null) return;
        manager.ActivatePreset(this);
    }
    
    public async void OnClickPresetButton()
    {
        if (isLocked)
        {
            Debug.Log($"'{characterName}' 프리셋은 현재 잠겨있어 상호작용할 수 없습니다.");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(characterSetting)) 
        { 
            UIManager.instance.ShowConfirmationWarning(ConfirmationType.CharacterSetting);
            return; 
        }
        if (CurrentMode == CharacterMode.Off)
        {
            var arguments = new Dictionary<string, object> {["charName"] = this.characterName};
            LocalizationManager.Instance.ShowWarning("Character_Is_Offline", arguments);
            return;
        }
        if (_aiConfig.modelMode == ModelMode.GeminiApi)
        {
            if (string.IsNullOrEmpty(UserData.Instance.GetAPIKey())) 
            { 
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.ApiSetting);
                return; 
            }
        }
        else if (_aiConfig.modelMode == ModelMode.OllamaHttp)
        {
            if (!await OllamaClient.CheckConnectionAsync())
            {
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.LocalModelSetting);
                return;
            }
        }
        
        ChatFunction.CharacterSession.SetPreset(presetID);
        foreach (var ui in Resources.FindObjectsOfTypeAll<ChatUI>())
        {
            if (ui.presetID == this.presetID && ui.gameObject.scene.IsValid())
            {
                ui.SetupForPresetChat(this);
                var canvasGroup = ui.GetComponent<CanvasGroup>();
                if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
                ui.transform.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                if (notifyImage != null) { notifyImage.SetActive(false); }
                if (MiniModeController.Instance != null)
                {
                    MiniModeController.Instance.UpdateItemUI(this.presetID);
                }
                return;
            }
        }
    }
    
     public void SetLockState(bool shouldBeLocked)
    {
        if (isLocked == shouldBeLocked) return;
        isLocked = shouldBeLocked;
        if (lockOverlay != null) lockOverlay.SetActive(isLocked);
        if (isLocked)
        {
            _wasVrmVisibleBeforeLock = isVrmVisible;
            _wasAutoMoveEnabledBeforeLock = isAutoMoveEnabled;
            _modeBeforeLock = CurrentMode;
            if (isVrmVisible) ToggleVrmVisibility(); 
            if (isAutoMoveEnabled) ToggleAutoMove();
            CurrentMode = CharacterMode.Off;
            
            // [수정] SetProfile 호출로 UI 업데이트 로직 통일
            SetProfile();

            if (chatUI != null && chatUI.OwnerID == this.presetID)
            {
                var canvasGroup = chatUI.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha > 0)
                {
                    canvasGroup.alpha = 0; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false;
                }
            }
        }
        else
        {
            CurrentMode = _modeBeforeLock;

            // [수정] SetProfile 호출로 UI 업데이트 로직 통일
            SetProfile();

            if (_wasVrmVisibleBeforeLock) ToggleVrmVisibility();
            if (_wasAutoMoveEnabledBeforeLock) ToggleAutoMove();
        }
        
        if (MiniModeController.Instance != null)
        {
            MiniModeController.Instance.UpdateItemUI(this.presetID);
        }
    }

    public void SetProfile()
    {
        if (name == null || Message == null) return;
        name.text = characterName;
        
        // [수정] 현재 모드에 따라 메시지와 아이콘을 모두 업데이트하도록 로직 통합
        switch (CurrentMode)
        {
            case CharacterMode.Activated:
                Message.text = onMessage;
                UpdateToggleSprite(UIManager.instance.modeOnSprite);
                break;
            case CharacterMode.Sleep:
                Message.text = sleepMessage;
                UpdateToggleSprite(UIManager.instance.modeSleepSprite);
                break;
            case CharacterMode.Off:
                Message.text = offMessage;
                UpdateToggleSprite(UIManager.instance.modeOffSprite);
                break;
            default:
                Message.text = offMessage;
                UpdateToggleSprite(UIManager.instance.modeOffSprite);
                break;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { settingIcon.SetActive(true); }
    public void OnPointerExit(PointerEventData eventData) { settingIcon.SetActive(false); }
    public bool IsModelActive() { return vrmModel != null && vrmModel.activeSelf; }
    
    public string ParseAndApplyResponse(string responseText)
    {
        if (string.IsNullOrEmpty(responseText)) return "(빈 응답)";
        
        string processedMessage = responseText;

        if (processedMessage.Contains("차단")) return processedMessage;

        // 1. 작별 태그 `[FAREWELL]` 처리
        var farewellRegex = new Regex(@"\[\s*FAREWELL\s*\]", RegexOptions.IgnoreCase);
        if (farewellRegex.IsMatch(processedMessage))
        {
            this.hasSaidFarewell = true;
            this.isWaitingForReply = false;
            this.ignoreCount = 0;
            Debug.Log($"'{this.characterName}'가 [FAREWELL]을 발언하여 대화를 종료합니다.");
            processedMessage = farewellRegex.Replace(processedMessage, "");
        }

        // 2. 친밀도 변경 태그 `[INTIMACY_CHANGE=값]` 처리
        // [최종 수정] '+' 기호를 포함하도록 정규식 패턴을 수정했습니다.
        var intimacyRegex = new Regex(@"\[\s*INTIMACY_CHANGE\s*=\s*([+-]?\d+\.?\d*)\s*\]", RegexOptions.IgnoreCase);
        Match intimacyMatch = intimacyRegex.Match(processedMessage);

        if (intimacyMatch.Success)
        {
            string valueStr = intimacyMatch.Groups[1].Value;
            if (float.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float delta))
            {
                this.ApplyIntimacyChange(delta);
            }
            processedMessage = intimacyRegex.Replace(processedMessage, "");
        }

        // 3. 기타 불필요한 태그 제거 (예: [ME])
        processedMessage = processedMessage.Replace("[ME]", "");
        
        return processedMessage.Trim();
    }
}