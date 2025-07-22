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
    
    [Header("UI연동")]
    [SerializeField] private Transform alarmListContent;
    [SerializeField] private GameObject alarmListPrefab;
    
    // ▶ [요청사항] 로컬라이제이션 지원을 위해 LocalizeStringEvent 컴포넌트 참조 추가
    // directionLabel 게임오브젝트에 LocalizeStringEvent 컴포넌트를 추가해야 합니다.
    private LocalizeStringEvent directionLabelLocalizer;
    
    
    // ▶ [요청사항] 선택된 알람 항목을 추적하기 위한 변수
    private AlarmListItem selectedAlarmListItem;

    private void Awake()
    {

    }

    private void Start()
    {
        // 시작 시 UI 상태를 기본값으로 초기화합니다.
        ResetAlarmUI();
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

        // 3. AlarmData 생성
        AlarmData data = new AlarmData
        {
            time = time,
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