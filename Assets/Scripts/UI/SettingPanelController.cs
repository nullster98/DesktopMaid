// --- START OF FILE SettingPanelController.cs ---

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SFB;
using TMPro;

[System.Serializable]
public class LabelledToggle
{
    public Toggle toggle;
    public string value;
    public Image image;
}

public class SettingPanelController : MonoBehaviour
{
    [Header("세팅 토글 필드")] 
    public Image moveIcon;
    public Image visibleIcon;
    
    [Header("입력 필드")]
    public Image characterImage;
    public TMP_InputField nameInput;
    public TMP_InputField onMessageInput;
    public TMP_InputField sleepMessageInput;
    public TMP_InputField offMessageInput;
    public TMP_InputField genderInput;
    public TMP_InputField personalityInput;
    public TMP_InputField settingInput;
    public List<TMP_InputField> dialogueInputs;

    [Header("스냅 설정")]
    public Slider sittingOffsetYSlider;
    
    [Header("토글 그룹")]
    public List<LabelledToggle> iqToggles;
    public List<LabelledToggle> intimacyToggles;
    
    [Header("토글 제어")]
    public bool isIntimacyEditable = false;
    
    [Header("예시 대사 관리")]
    public GameObject dialogueInputPrefab;
    public Transform dialogueParent;
    public GameObject plusButton;
    private List<TMP_InputField> dynamicInputs = new();

    [Header("연결된 프리셋")]
    public CharacterPreset targetPreset;
    
    // [추가] 현재 구독중인 프리셋을 기억하기 위한 변수
    private CharacterPreset _subscribedPreset;

    private LabelledToggle lastIQ = null;
    private LabelledToggle lastIntimacy = null;
    
    private void Start()
    {
        InitToggleGroup(iqToggles, ref lastIQ);
        InitToggleGroup(intimacyToggles, ref lastIntimacy);
        sittingOffsetYSlider.onValueChanged.AddListener(OnSittingOffsetYChanged);
        SetIntimacyEditable(isIntimacyEditable);
    }
    
    // [추가] 오브젝트가 파괴될 때 이벤트 구독을 안전하게 해제
    private void OnDestroy()
    {
        if (_subscribedPreset != null)
        {
            _subscribedPreset.OnVrmStateChanged -= UpdateVrmControlIcons;
        }
    }
    
    private void LateUpdate()
    {
        var currentIQ = iqToggles.FirstOrDefault(t => t.toggle.isOn);
        if (currentIQ != lastIQ)
        {
            lastIQ = currentIQ;
            UpdateToggleVisuals(iqToggles, lastIQ);
        }

        var currentIntimacy = intimacyToggles.FirstOrDefault(t => t.toggle.isOn);
        if (currentIntimacy != lastIntimacy)
        {
            lastIntimacy = currentIntimacy;
            UpdateToggleVisuals(intimacyToggles, lastIntimacy);

            if (isIntimacyEditable && targetPreset != null && currentIntimacy != null)
            {
                targetPreset.SetIntimacyFromString(currentIntimacy.value);
            }
        }
    }

    public void LoadPresetToUI()
    {
        // [수정] 이벤트 구독 관리 로직 추가
        // 1. 만약 이전에 구독한 프리셋이 있다면, 이전 구독을 해제
        if (_subscribedPreset != null)
        {
            _subscribedPreset.OnVrmStateChanged -= UpdateVrmControlIcons;
        }
        
        if (targetPreset == null)
        {
            LoadDefaultValues();
            _subscribedPreset = null; // 타겟이 없으므로 구독할 것도 없음
            return;
        }

        // 2. 새로운 targetPreset의 이벤트에 구독
        targetPreset.OnVrmStateChanged += UpdateVrmControlIcons;
        _subscribedPreset = targetPreset; // 현재 구독한 프리셋을 기억
        
        nameInput.text = targetPreset.characterName;
        onMessageInput.text = targetPreset.onMessage;
        sleepMessageInput.text = targetPreset.sleepMessage;
        offMessageInput.text = targetPreset.offMessage;
        genderInput.text = targetPreset.gender;
        personalityInput.text = targetPreset.personality;
        settingInput.text = targetPreset.characterSetting;
        characterImage.sprite = targetPreset.characterImage.sprite;
        sittingOffsetYSlider.value = targetPreset.sittingOffsetY;

        SetToggleByValue(iqToggles, targetPreset.iQ);
        SetToggleByValue(intimacyToggles, targetPreset.intimacy); 

        SetIntimacyEditable(isIntimacyEditable);
        
        // [수정] 패널이 열릴 때(LoadPresetToUI 호출 시) 현재 상태에 맞게 아이콘을 즉시 업데이트 (질문 3 해결)
        UpdateVrmControlIcons();

        foreach (var i in dynamicInputs) Destroy(i.transform.parent.gameObject);
        dynamicInputs.Clear();
        dialogueInputs.ForEach(i => i.text = "");

        for (int i = 0; i < targetPreset.dialogueExample.Count; i++)
        {
            if (i < dialogueInputs.Count) { dialogueInputs[i].text = targetPreset.dialogueExample[i]; }
            else
            {
                AddDialogueField();
                dynamicInputs.Last().text = targetPreset.dialogueExample[i];
            }
        }
    }
    
    public void OnClickSaveButton()
    {
        if (targetPreset == null) return;
        
        var allDialogueInputs = dialogueInputs.Concat(dynamicInputs)
            .Select(i => i.text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        
        targetPreset.characterName = nameInput.text;
        targetPreset.onMessage = onMessageInput.text;
        targetPreset.sleepMessage = sleepMessageInput.text;
        targetPreset.offMessage = offMessageInput.text;
        targetPreset.gender = genderInput.text;
        targetPreset.personality = personalityInput.text;
        targetPreset.characterSetting = settingInput.text;
        targetPreset.iQ = iqToggles.FirstOrDefault(t => t.toggle.isOn)?.value;
        targetPreset.sittingOffsetY = sittingOffsetYSlider.value;
        targetPreset.characterImage.sprite = characterImage.sprite;
        targetPreset.dialogueExample.Clear();
        targetPreset.dialogueExample.AddRange(allDialogueInputs);
        
        targetPreset.SetProfile();
        
        UIManager.instance.TriggerWarning("적용 완료");
    }

    public void LoadCharacterImage()
    {
        var extensions = new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("이미지 선택", "", extensions, false);
        if (paths.Length > 0) StartCoroutine(LoadImageCoroutine(paths[0]));
    }

    public void AddDialogueField()
    {
        GameObject obj = Instantiate(dialogueInputPrefab, dialogueParent);
        TMP_InputField input = obj.GetComponentInChildren<TMP_InputField>();
        if (input != null) dynamicInputs.Add(input);
        plusButton.transform.SetAsLastSibling();
    }

    public void SetIntimacyEditable(bool editable)
    {
        isIntimacyEditable = editable;
        foreach (var pair in intimacyToggles)
        {
            if (pair.toggle != null) pair.toggle.interactable = editable;
        }
    }
    
    public void ToggleIntimacyEditable()
    {
        SetIntimacyEditable(!isIntimacyEditable);

        if (isIntimacyEditable)
        {
            UIManager.instance.TriggerWarning("친밀도 수동 편집 활성화");
        }
        else
        {
            UIManager.instance.TriggerWarning("친밀도 자동 관리 활성화");
        }
    }
    
    private void OnSittingOffsetYChanged(float value)
    {
        if (targetPreset != null) targetPreset.sittingOffsetY = value;
    }
    
    private void LoadDefaultValues()
    {
        characterImage.sprite = UIManager.instance.defaultCharacterSprite;
        nameInput.text = "New Character";
        onMessageInput.text = "";
        sleepMessageInput.text = "";
        offMessageInput.text = "";
        genderInput.text = "Unknown";
        personalityInput.text = "Neutral";
        settingInput.text = "";

        dynamicInputs.ForEach(input => Destroy(input.transform.parent.gameObject));
        dynamicInputs.Clear();
        dialogueInputs.ForEach(input => input.text = "");

        SetToggleByValue(iqToggles, "3");
        SetToggleByValue(intimacyToggles, "3");
        SetIntimacyEditable(isIntimacyEditable);
        sittingOffsetYSlider.value = 0f;
    }

    private void InitToggleGroup(List<LabelledToggle> group, ref LabelledToggle last)
    {
        foreach (var pair in group)
        {
            if (pair.image != null) SetAlpha(pair.image, 0f);
            pair.toggle.onValueChanged.AddListener((_) => {});
        }
        last = group.FirstOrDefault(t => t.toggle.isOn);
        UpdateToggleVisuals(group, last);
    }
    
    private void UpdateToggleVisuals(List<LabelledToggle> group, LabelledToggle active)
    {
        foreach (var pair in group)
        {
            if (pair.image != null) SetAlpha(pair.image, pair == active ? 1f : 0f);
        }
    }
    
    private void SetToggleByValue(List<LabelledToggle> toggles, string value)
    {
        if (toggles == intimacyToggles)
        {
            if (string.IsNullOrEmpty(value))
            {
                value = "5";
            }
            else if (int.TryParse(value, out int intValue) && intValue % 2 == 0)
            {
                value = (intValue - 1).ToString();
                Debug.LogWarning($"[SetToggleByValue] 짝수 친밀도 값('{intValue}') 감지. UI 표시를 위해 '{value}'로 보정합니다.");
            }
        }
        else if (string.IsNullOrEmpty(value))
        {
            value = "3";
        }

        bool toggleSet = false;
        foreach (var t in toggles)
        {
            bool shouldBeOn = (t.value == value);
            if (t.toggle.isOn != shouldBeOn)
            {
                t.toggle.isOn = shouldBeOn;
            }
            if (shouldBeOn)
            {
                toggleSet = true;
            }
        }

        if (!toggleSet)
        {
            string defaultValue = (toggles == intimacyToggles) ? "5" : "3";
            var defaultToggle = toggles.FirstOrDefault(t => t.value == defaultValue);
            if (defaultToggle != null)
            {
                defaultToggle.toggle.isOn = true;
            }
        }
    }
    
    private void SetAlpha(Image img, float alpha)
    {
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private IEnumerator LoadImageCoroutine(string path)
    {
        using (WWW www = new WWW("file://" + path))
        {
            yield return www;
            if (string.IsNullOrEmpty(www.error))
            {
                Texture2D tex = www.texture;
                characterImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
    }

    // [수정] 버튼 클릭 시 CharacterPreset의 함수를 호출하도록 변경.
    // 아이콘 업데이트는 이벤트 시스템이 처리하므로 직접 스프라이트를 바꾸지 않음.
    public void ToggleVrmMoving()
    {
        if (targetPreset == null) return;
        targetPreset.ToggleAutoMove();
    }

    public void ToggleVrmVisible()
    {
        if (targetPreset == null) return;
        targetPreset.ToggleVrmVisibility();
    }
    
    // [추가] VRM 컨트롤 아이콘을 업데이트하는 중앙 관리 함수
    // CharacterPreset의 이벤트 또는 LoadPresetToUI에 의해 호출됨
    private void UpdateVrmControlIcons()
    {
        if (targetPreset == null) return;

        moveIcon.sprite = targetPreset.isAutoMoveEnabled ? UIManager.instance.vrmMoveSprite : UIManager.instance.vrmStopSprite;
        visibleIcon.sprite = targetPreset.isVrmVisible ? UIManager.instance.vrmOnSprite : UIManager.instance.vrmOffSprite; 
    }
}