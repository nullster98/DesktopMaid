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
    [Tooltip("최대 무시 횟수. 이 횟수를 넘어가면 AI는 말을 거는 것을 포기합니다.")]
    public int maxIgnoreCount = 2;
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
        // [수정] 무시 횟수에 따라 대기 시간을 점진적으로 늘립니다. (5분, 10분, 15분...)
        // ignoreCount는 0부터 시작하므로 +1을 해줍니다. 300초 = 5분.
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
            var arguments = new Dictionary<string, object>
            {
                ["charName"] = this.characterName 
            };
            LocalizationManager.Instance.ShowWarning("Character_Is_Offline", arguments);
            return;
        }
        
        if (_aiConfig.modelMode == ModelMode.GeminiApi)
        {
            string apiKey = UserData.Instance.GetAPIKey();
            if (string.IsNullOrEmpty(apiKey)) 
            { 
                UIManager.instance.ShowConfirmationWarning(ConfirmationType.ApiSetting);
                return; 
            }
        }

        if (string.IsNullOrWhiteSpace(characterSetting)) 
        { 
            UIManager.instance.ShowConfirmationWarning(ConfirmationType.CharacterSetting);
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
        switch (CurrentMode)
        {
            case CharacterMode.Activated:
                Message.text = onMessage;
                break;
            case CharacterMode.Sleep:
                Message.text = sleepMessage;
                break;
            case CharacterMode.Off:
                Message.text = offMessage;
                break;
            default:
                Message.text = offMessage;
                break;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { settingIcon.SetActive(true); }
    public void OnPointerExit(PointerEventData eventData) { settingIcon.SetActive(false); }
    public bool IsModelActive() { return vrmModel != null && vrmModel.activeSelf; }
    
    /// <summary>
    /// AI의 원본 응답 텍스트를 파싱하여 태그를 처리하고, UI에 표시될 깨끗한 텍스트를 반환합니다.
    /// [INTIMACY_CHANGE], [FAREWELL] 등의 태그를 감지하고 캐릭터 상태를 직접 변경합니다.
    /// </summary>
    /// <param name="responseText">AI가 생성한 원본 응답 텍스트</param>
    /// <returns>태그가 제거된, UI에 표시될 메시지</returns>
    public string ParseAndApplyResponse(string responseText)
    {
        string parsedText = responseText;
        if (string.IsNullOrEmpty(parsedText))
        {
            return "(빈 응답)";
        }
        
        if (parsedText.Contains("차단"))
        {
            return parsedText;
        }

        if (parsedText.Contains("[FAREWELL]"))
        {
            this.hasSaidFarewell = true;
            this.isWaitingForReply = false;
            this.ignoreCount = 0;
            parsedText = parsedText.Replace("[FAREWELL]", "");
            Debug.Log($"'{this.characterName}'가 [FAREWELL]을 발언하여 대화를 종료합니다.");
        }

        string changeTag = "[INTIMACY_CHANGE=";
        int tagIndex = parsedText.IndexOf(changeTag, StringComparison.OrdinalIgnoreCase);
        if (tagIndex != -1)
        {
            int endIndex = parsedText.IndexOf(']', tagIndex);
            if (endIndex != -1)
            {
                string valueStr = parsedText.Substring(tagIndex + changeTag.Length, endIndex - (tagIndex + changeTag.Length));
                if (float.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float delta))
                {
                    this.ApplyIntimacyChange(delta);
                }
                parsedText = parsedText.Remove(tagIndex, endIndex - tagIndex + 1);
            }
        }
        
        parsedText = parsedText.Replace("[ME]", "");
        
        return parsedText.Trim();
    }
}