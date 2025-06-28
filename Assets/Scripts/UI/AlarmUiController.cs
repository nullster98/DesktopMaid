// --- START OF FILE AlarmUiController.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Any() 메서드 사용을 위해 추가
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components; // LocalizeStringEvent 사용을 위해 추가
using UnityEngine.UI;

public class AlarmUiController : MonoBehaviour
{
    [Header("알람UI")]
    [SerializeField] private GameObject alarmPanel;
    
    [Header("시간")]
    [SerializeField] private TMP_Dropdown hoursDropdown;
    [SerializeField] private TMP_Dropdown minutesDropdown;

    [Header("사운드")] 
    [SerializeField] private AudioClip selectedSound;

    [Header("슬롯")] 
    [SerializeField] private GameObject presetSelectContainer;
    [SerializeField] private GameObject presetBtnPrefab;
    [SerializeField] private Transform presetBtnParent;
    [SerializeField] private Button presetSlot1;
    [SerializeField] private Image presetSlotImage1;
    [SerializeField] private Button presetSlot2;
    [SerializeField] private Image presetSlotImage2;
    [SerializeField] private Sprite defaultPresetSprite;

    private CharacterPreset preset1;
    private CharacterPreset preset2;
    
    [Header("UI연동")]
    [SerializeField] private Transform alarmListContent;
    [SerializeField] private GameObject alarmListPrefab;

    [Header("방향전환")]
    [SerializeField] private TMP_Text directionLabel;
    [SerializeField] private Image upLeftImage;
    [SerializeField] private Image downRightImage;
    [SerializeField] private Sprite upSprite;
    [SerializeField] private Sprite downSprite;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;
    
    // ▶ [요청사항] 로컬라이제이션 지원을 위해 LocalizeStringEvent 컴포넌트 참조 추가
    // directionLabel 게임오브젝트에 LocalizeStringEvent 컴포넌트를 추가해야 합니다.
    private LocalizeStringEvent directionLabelLocalizer;

    public enum DirectionMode {Horizontal, Vertical}
    private DirectionMode currentMode = DirectionMode.Horizontal;
    
    // ▶ [요청사항] 선택된 알람 항목을 추적하기 위한 변수
    private AlarmListItem selectedAlarmListItem;

    private void Awake()
    {
        // LocalizeStringEvent 컴포넌트를 미리 찾아둡니다.
        directionLabelLocalizer = directionLabel.GetComponent<LocalizeStringEvent>();
        if(directionLabelLocalizer == null)
            Debug.LogError("directionLabel에 LocalizeStringEvent 컴포넌트가 없습니다!");
    }

    private void Start()
    {
        // 시작 시 UI 상태를 기본값으로 초기화합니다.
        ResetAlarmUI();
    }

    /// <summary>
    /// 방향 모드를 토글하고 UI를 갱신합니다.
    /// </summary>
    public void ToggleDirectionMode()
    {
        currentMode = currentMode == DirectionMode.Horizontal ? DirectionMode.Vertical : DirectionMode.Horizontal;
        UpdateDirectionUI();
    }

    /// <summary>
    /// 현재 방향 모드에 맞춰 UI(텍스트, 이미지)를 갱신합니다.
    /// </summary>
    private void UpdateDirectionUI()
    {
        if (currentMode == DirectionMode.Horizontal)
        {
            // ▶ [요청사항] 하드코딩된 텍스트 대신 로컬라이제이션 키를 사용합니다.
            if (directionLabelLocalizer != null)
                directionLabelLocalizer.StringReference.SetReference("string Table", "좌우"); // 테이블명: UI_Text, 키: direction_horizontal (예시)
            else
                directionLabel.text = "좌우"; // 폴백

            upLeftImage.sprite = leftSprite;
            downRightImage.sprite = rightSprite;
        }
        else // Vertical
        {
            if (directionLabelLocalizer != null)
                directionLabelLocalizer.StringReference.SetReference("string Table", "상하"); // 테이블명: UI_Text, 키: direction_vertical (예시)
            else
                directionLabel.text = "상하"; // 폴백
                
            upLeftImage.sprite = upSprite;
            downRightImage.sprite = downSprite;
        }
    }

    public TimeSpan GetSelectedTime()
    {
        int hour = hoursDropdown.value;
        int minute = minutesDropdown.value;
        
        return new TimeSpan(hour, minute, 0);
    }
    
    /// <summary>
    /// 알람을 저장하거나 기존 알람을 업데이트합니다.
    /// </summary>
    public void SaveOrUpdateAlarm()
    {
        // 1. 시간 가져오기
        TimeSpan time = GetSelectedTime();

        // ▶ [요청사항] 같은 시간에 다른 알람이 있는지 확인 (수정 중인 알람은 제외)
        if (AlarmManager.Instance.GetAllAlarms().Any(a => a != selectedAlarmListItem?.AlarmData && a.time == time))
        {
            Debug.LogWarning($"[알람UI] 동일한 시간({time:hh\\:mm})의 알람이 이미 존재하여 저장할 수 없습니다.");
            // 여기에 사용자에게 보여줄 경고 팝업 등을 띄우면 더 좋습니다.
            return;
        }
        
        // 만약 기존 알람을 수정하는 것이라면, 이전 데이터를 먼저 삭제합니다.
        if (selectedAlarmListItem != null)
        {
            AlarmManager.Instance.RemoveAlarm(selectedAlarmListItem.AlarmData);
            Destroy(selectedAlarmListItem.gameObject);
        }

        // 2. 프리셋 + 방향 정보
        List<AlarmPreset> presetList = new();

        if (preset1 != null)
        {
            presetList.Add(new AlarmPreset
            {
                targetPreset = preset1,
                direction = currentMode == DirectionMode.Horizontal ? Direction.LEFT : Direction.UP
            });
        }

        if (preset2 != null)
        {
            presetList.Add(new AlarmPreset
            {
                targetPreset = preset2,
                direction = currentMode == DirectionMode.Horizontal ? Direction.RIGHT : Direction.DOWN
            });
        }

        // 3. AlarmData 생성
        AlarmData data = new AlarmData
        {
            time = time,
            presets = presetList,
            alarmSound = selectedSound,
            isEnabled = true
        };

        // 4. AlarmManager에 추가
        AlarmManager.Instance.AddAlarm(data);

        // 5. 리스트에 UI 추가
        GameObject listItemObj = Instantiate(alarmListPrefab, alarmListContent);
        var listComponent = listItemObj.GetComponent<AlarmListItem>();
        listComponent.Init(data, this);
        
        // 6. UI 초기화
        ResetAlarmUI();
    }

    public void OnClickPresetSlot(bool isFirstSlot)
    {
        Debug.Log($"[알람UI] 프리셋 슬롯 클릭됨 → {(isFirstSlot ? "1번" : "2번")}");
        
        presetSelectContainer.SetActive(true);

        foreach (Transform child in presetBtnParent)
            Destroy(child.gameObject);

        // 선택 취소용 기본 버튼
        {
            var capturedSlot = isFirstSlot;
            GameObject defaultBtn = Instantiate(presetBtnPrefab, presetBtnParent);
            defaultBtn.GetComponent<Image>().sprite = defaultPresetSprite;
            defaultBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (capturedSlot)
                {
                    preset1 = null;
                    presetSlotImage1.sprite = defaultPresetSprite;
                }
                else
                {
                    preset2 = null;
                    presetSlotImage2.sprite = defaultPresetSprite;
                }
                presetSelectContainer.SetActive(false);
            });
        }
        
        // 프리셋 버튼들
        var allPresets = CharacterPresetManager.Instance.presets;
        foreach (var preset in allPresets)
        {
            GameObject btn = Instantiate(presetBtnPrefab, presetBtnParent);
            btn.GetComponent<Image>().sprite = preset.characterImage.sprite;
            var capturedPreset = preset;
            var capturedSlot = isFirstSlot;
            btn.GetComponent<Button>().onClick.AddListener(() => SelectPreset(capturedPreset, capturedSlot));
        }
    }

    private void SelectPreset(CharacterPreset selectedPreset, bool isFirstSlot)
    {
        if (isFirstSlot)
        {
            if (preset2 == selectedPreset)
            {
                preset2 = null;
                presetSlotImage2.sprite = defaultPresetSprite;
            }
            preset1 = selectedPreset;
            presetSlotImage1.sprite = selectedPreset.characterImage.sprite;
        }
        else
        {
            if (preset1 == selectedPreset)
            {
                preset1 = null;
                presetSlotImage1.sprite = defaultPresetSprite;
            }
            preset2 = selectedPreset;
            presetSlotImage2.sprite = selectedPreset.characterImage.sprite;
        }

        presetSelectContainer.SetActive(false);
    }
    
    /// <summary>
    /// 리스트에서 선택된 알람의 정보를 UI에 불러옵니다.
    /// </summary>
    /// <param name="item">선택된 리스트 아이템</param>
    public void LoadAlarm(AlarmListItem item)
    {
        // ▶ [요청사항] 선택된 아이템 하이라이트 처리
        SetSelectedListItem(item);
        
        var data = item.AlarmData;

        // 시간 설정
        hoursDropdown.value = data.time.Hours;
        minutesDropdown.value = data.time.Minutes;

        // 프리셋 리셋
        preset1 = null;
        preset2 = null;
        presetSlotImage1.sprite = defaultPresetSprite;
        presetSlotImage2.sprite = defaultPresetSprite;
        
        // 프리셋 & 방향 적용
        if (data.presets.Any())
        {
            bool isHorizontal = data.presets.Exists(p => p.direction is Direction.LEFT or Direction.RIGHT);
            currentMode = isHorizontal ? DirectionMode.Horizontal : DirectionMode.Vertical;

            foreach (var ap in data.presets)
            {
                if (ap.direction is Direction.LEFT or Direction.UP)
                {
                    preset1 = ap.targetPreset;
                    presetSlotImage1.sprite = ap.targetPreset.characterImage.sprite;
                }
                else if (ap.direction is Direction.RIGHT or Direction.DOWN)
                {
                    preset2 = ap.targetPreset;
                    presetSlotImage2.sprite = ap.targetPreset.characterImage.sprite;
                }
            }
        }
        else // 프리셋이 없는 알람일 경우 기본값으로
        {
            currentMode = DirectionMode.Horizontal;
        }
        
        // ▶ [버그수정] Toggle이 아닌 Update를 호출하여 UI만 갱신
        UpdateDirectionUI();

        selectedSound = data.alarmSound;
    }
    
    public void DeleteAlarm()
    {
        if (selectedAlarmListItem == null) return;

        AlarmManager.Instance.RemoveAlarm(selectedAlarmListItem.AlarmData);
        Destroy(selectedAlarmListItem.gameObject);
        ResetAlarmUI();
    }

    /// <summary>
    /// 알람 설정 UI를 기본 상태로 초기화합니다.
    /// </summary>
    private void ResetAlarmUI()
    {
        // 선택 해제
        SetSelectedListItem(null);

        // UI 값 초기화
        hoursDropdown.value = 0;
        minutesDropdown.value = 0;
        
        // ▶ [버그수정] 방향을 기본값(Horizontal)으로 설정하고 UI 갱신
        currentMode = DirectionMode.Horizontal;
        UpdateDirectionUI();

        preset1 = null;
        preset2 = null;
        presetSlotImage1.sprite = defaultPresetSprite;
        presetSlotImage2.sprite = defaultPresetSprite;
        
        // TODO: 기본 사운드가 있다면 할당
        selectedSound = null;
    }

    /// <summary>
    /// 지정된 리스트 아이템을 선택 상태로 만들고, 이전에 선택된 아이템은 해제합니다.
    /// </summary>
    private void SetSelectedListItem(AlarmListItem newItem)
    {
        // 이전에 선택된 아이템이 있었다면 선택 해제
        if (selectedAlarmListItem != null)
        {
            selectedAlarmListItem.Deselect();
        }

        selectedAlarmListItem = newItem;

        // 새로 선택된 아이템이 있다면 선택 상태로 변경
        if (selectedAlarmListItem != null)
        {
            selectedAlarmListItem.Select();
        }
    }

    public void OpenAlarmPanel()
    {
        alarmPanel.SetActive(true);
        alarmPanel.transform.SetAsLastSibling();
        // 패널을 열 때 항상 UI를 초기 상태로 보여주기
        ResetAlarmUI();
    }
}