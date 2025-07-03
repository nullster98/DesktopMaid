using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlarmData
{
    public TimeSpan time;
    public List<AlarmPreset> presets;
    public AudioClip alarmSound;
    public bool isEnabled = true;
    public bool isTriggered = false;
}

public class AlarmPreset
{
    public CharacterPreset targetPreset;
    public Direction direction;
}

public enum Direction { UP, DOWN, LEFT, RIGHT }

public class AlarmManager : MonoBehaviour
{
    public static AlarmManager Instance { get; private set;}

    [SerializeField] private List<AlarmData> alarmList = new();
    [SerializeField] private AudioSource alarmAudioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(CheckAlarmsCoroutine());
    }

    private IEnumerator CheckAlarmsCoroutine()
    {
        while (true)
        {
            DateTime now = DateTime.Now;

            foreach (var alarm in alarmList)
            {
                if (!alarm.isEnabled || alarm.isTriggered) continue;

                // 초 단위가 너무 정밀하면 누락되기 쉬움 → 분 단위로 체크
                if (now.Hour == alarm.time.Hours && now.Minute == alarm.time.Minutes)
                {
                    TriggerAlarm(alarm);
                    alarm.isTriggered = true;
                }
            }

            yield return new WaitForSeconds(5f); // 너무 짧으면 CPU 부담, 너무 길면 지연 발생
        }
    }

    private void TriggerAlarm(AlarmData alarm)
    {
        Debug.Log($"[AlarmManager] 알람 트리거 : {alarm.time}");

        foreach (var preset in alarm.presets)
        {
            if(preset.targetPreset == null) continue;
            
            if (!preset.targetPreset.isVrmVisible)
            {
                preset.targetPreset.ToggleVrmVisibility();
            }
            
            //TODO : 위치이동 및 애니메이션 처리

            if (alarm.alarmSound != null)
            {
                alarmAudioSource.PlayOneShot(alarm.alarmSound);
            }
        }
    }

    public void AddAlarm(AlarmData data)
    {
        alarmList.Add(data);
    }

    public void RemoveAlarm(AlarmData data)
    {
        if (alarmList.Contains(data))
            alarmList.Remove(data);
    }
    
    public List<AlarmData> GetAllAlarms() => alarmList;
}
