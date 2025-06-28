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

    private LabelledToggle lastIQ = null;
    private LabelledToggle lastIntimacy = null;
    
    private void Start()
    {
        InitToggleGroup(iqToggles, ref lastIQ);
        InitToggleGroup(intimacyToggles, ref lastIntimacy);
        sittingOffsetYSlider.onValueChanged.AddListener(OnSittingOffsetYChanged);
        SetIntimacyEditable(isIntimacyEditable);
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

            // [수정] 유저가 토글을 변경했을 때의 처리 로직
            // 편집 가능한 상태이고, 타겟 프리셋이 연결되어 있을 때만 값 변경
            if (isIntimacyEditable && targetPreset != null && currentIntimacy != null)
            {
                // CharacterPreset에 새로 만든 함수를 호출하여 내부/외부 친밀도 값을 모두 업데이트
                targetPreset.SetIntimacyFromString(currentIntimacy.value);
            }
        }
    }

    public void LoadPresetToUI()
    {
        if (targetPreset == null)
        {
            LoadDefaultValues();
            return;
        }
        
        nameInput.text = targetPreset.characterName;
        onMessageInput.text = targetPreset.onMessage;
        sleepMessageInput.text = targetPreset.sleepMessage;
        offMessageInput.text = targetPreset.offMessage;
        genderInput.text = targetPreset.gender;
        personalityInput.text = targetPreset.personality;
        settingInput.text = targetPreset.characterSetting;
        characterImage.sprite = targetPreset.characterImage.sprite;
        sittingOffsetYSlider.value = targetPreset.sittingOffsetY;

        // [수정] targetPreset에 저장된 string intimacy 값을 기반으로 토글을 설정
        SetToggleByValue(iqToggles, targetPreset.iQ);
        SetToggleByValue(intimacyToggles, targetPreset.intimacy); 

        SetIntimacyEditable(isIntimacyEditable);

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
        
        // UI의 값들을 targetPreset 객체에 직접 할당
        targetPreset.characterName = nameInput.text;
        targetPreset.onMessage = onMessageInput.text;
        targetPreset.sleepMessage = sleepMessageInput.text;
        targetPreset.offMessage = offMessageInput.text;
        targetPreset.gender = genderInput.text;
        targetPreset.personality = personalityInput.text;
        targetPreset.characterSetting = settingInput.text;
        targetPreset.iQ = iqToggles.FirstOrDefault(t => t.toggle.isOn)?.value;
        // intimacy는 LateUpdate에서 이미 반영되었으므로 여기서는 건드리지 않음
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
        // 현재 상태의 반대 값으로 설정
        SetIntimacyEditable(!isIntimacyEditable);

        // (선택 사항) 현재 상태를 유저에게 알려주는 피드백
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
    
    // [수정] 친밀도 값 설정 로직 강화
    private void SetToggleByValue(List<LabelledToggle> toggles, string value)
    {
        // 1. 친밀도 토글 그룹일 때만 특수 규칙 적용
        if (toggles == intimacyToggles)
        {
            // 값이 비어있으면 기본값 "5"로 설정
            if (string.IsNullOrEmpty(value))
            {
                value = "5";
            }
            // 짝수 값을 바로 아래 홀수 값으로 보정 (예: "10" -> "9", "8" -> "7")
            else if (int.TryParse(value, out int intValue) && intValue % 2 == 0)
            {
                value = (intValue - 1).ToString();
                Debug.LogWarning($"[SetToggleByValue] 짝수 친밀도 값('{intValue}') 감지. UI 표시를 위해 '{value}'로 보정합니다.");
            }
        }
        // IQ 토글이나 다른 그룹은 기존 로직 유지
        else if (string.IsNullOrEmpty(value))
        {
            value = "3"; // IQ의 기본값
        }

        // 2. 최종 결정된 value 값으로 토글을 켬
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

        // 3. 만약 위 과정 후에도 켜진 토글이 없다면(예: 데이터 오류), 기본 토글을 강제로 켬
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
}