// --- START OF FILE AlarmManager.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Linq;

public class AlarmData
{
    public TimeSpan time;
    public AudioClip alarmSound;
    public bool isEnabled = true;
    public bool isTriggered = false;
    public string userSoundPath;
}

public class AlarmManager : MonoBehaviour
{
    public static AlarmManager Instance { get; private set;}
    
    [Header("알람 목록")]
    [SerializeField] private List<AlarmData> alarmList = new();
    
    [Header("알람 설정")]
    [SerializeField] private AudioSource alarmAudioSource;
    [SerializeField] private List<AudioClip> defaultAlarmSounds = new();
    
    [Header("사운드 효과")]
    [SerializeField] private float fadeInDuration = 2.0f;
    [SerializeField] private float fadeOutDuration = 3.0f;

    #region 테스트용 즉시 실행 함수

    /// <summary>
    /// [테스트용] UI 버튼에 연결하여 알람을 즉시 실행합니다.
    /// 알람 목록에 있는 첫 번째 활성화된 알람을 찾아 실행합니다.
    /// </summary>
    public void TriggerFirstAlarmForTesting()
    {
        AlarmData alarmToTest = alarmList.FirstOrDefault(a => a.isEnabled);
        if (alarmToTest != null)
        {
            Debug.LogWarning($"[테스트 모드] 알람 '{alarmToTest.time}'을(를) 즉시 실행합니다.");
            StartCoroutine(ProcessAlarmSequence(alarmToTest));
        }
        else
        {
            Debug.LogError("[테스트 모드] 실행할 활성화된 알람이 목록에 없습니다. 알람을 하나 이상 생성하고 활성화해주세요.");
        }
    }

    #endregion

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
                
                if (now.Hour == alarm.time.Hours && now.Minute == alarm.time.Minutes)
                {
                    alarm.isTriggered = true;
                    StartCoroutine(ProcessAlarmSequence(alarm));
                }
            }
            yield return new WaitForSeconds(5f);
        }
    }
    
    private IEnumerator ProcessAlarmSequence(AlarmData alarm)
    {
        Debug.Log($"[AlarmManager] 알람 시퀀스 시작: {alarm.time}");

        CharacterPreset[] allPresets = FindObjectsOfType<CharacterPreset>();
        foreach (CharacterPreset preset in allPresets)
        {
            if (preset != null && preset.vrmModel != null && preset.vrmModel.activeInHierarchy)
            {
                preset.StartAlarmBehavior();
            }
        }
        
        yield return StartCoroutine(PlayAlarmSoundWithFadeCoroutine(alarm));
        
        allPresets = FindObjectsOfType<CharacterPreset>();
        foreach (CharacterPreset preset in allPresets)
        {
            if (preset != null && preset.IsInAlarmState)
            {
                preset.StopAlarmBehavior();
            }
        }
        
        Debug.Log($"[AlarmManager] 알람 시퀀스 종료: {alarm.time}");
    }
    
    private IEnumerator PlayAlarmSoundWithFadeCoroutine(AlarmData alarm)
    {
        AudioClip clipToPlay = null;
        bool loadedFromPath = false;

        if (!string.IsNullOrEmpty(alarm.userSoundPath) && File.Exists(alarm.userSoundPath))
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + alarm.userSoundPath, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    clipToPlay = DownloadHandlerAudioClip.GetContent(www);
                    clipToPlay.name = Path.GetFileName(alarm.userSoundPath);
                    loadedFromPath = true;
                }
                else
                {
                    Debug.LogError($"[AlarmManager] 사용자 파일 로드 실패: {www.error}");
                }
            }
        }
        
        if (clipToPlay == null && alarm.alarmSound != null)
        {
            clipToPlay = alarm.alarmSound;
        }
        else if (clipToPlay == null && defaultAlarmSounds.Count > 0)
        {
            clipToPlay = defaultAlarmSounds[UnityEngine.Random.Range(0, defaultAlarmSounds.Count)];
        }

        if (clipToPlay != null)
        {
            // [수정] 알람 볼륨을 UserData.Instance.AlarmVolume에서 가져옵니다.
            float maxVolume = (UserData.Instance != null) ? UserData.Instance.AlarmVolume : 1.0f;
            
            alarmAudioSource.clip = clipToPlay;
            float maxPlaybackDuration = loadedFromPath ? UnityEngine.Random.Range(15f, 30f) : clipToPlay.length;
            float actualPlaybackDuration = Mathf.Min(clipToPlay.length, maxPlaybackDuration);
            
            float safeFadeIn = Mathf.Min(fadeInDuration, actualPlaybackDuration / 2);
            float safeFadeOut = Mathf.Min(fadeOutDuration, actualPlaybackDuration / 2);
            
            alarmAudioSource.volume = 0;
            alarmAudioSource.Play();
            Debug.Log($"[AlarmManager] '{clipToPlay.name}' 재생 시작 ({safeFadeIn}s 페이드 인).");
            float timer = 0f;
            while (timer < safeFadeIn)
            {
                alarmAudioSource.volume = Mathf.Lerp(0, maxVolume, timer / safeFadeIn);
                timer += Time.deltaTime;
                yield return null;
            }
            alarmAudioSource.volume = maxVolume; // 페이드 인 완료 후 최대 볼륨으로 설정
            
            float remainingTime = actualPlaybackDuration - safeFadeIn - safeFadeOut;
            if (remainingTime > 0)
            {
                yield return new WaitForSeconds(remainingTime);
            }
            
            Debug.Log($"[AlarmManager] '{clipToPlay.name}' 페이드 아웃 시작 ({safeFadeOut}s).");
            timer = 0f;
            float startFadeOutVolume = alarmAudioSource.volume;
            while (timer < safeFadeOut)
            {
                alarmAudioSource.volume = Mathf.Lerp(startFadeOutVolume, 0, timer / safeFadeOut);
                timer += Time.deltaTime;
                yield return null;
            }
            
            alarmAudioSource.Stop();
            alarmAudioSource.volume = 1; // 기본 볼륨으로 복원
            alarmAudioSource.clip = null;

            if (loadedFromPath)
            {
                Destroy(clipToPlay);
            }
        }
        else
        {
            Debug.LogWarning("[AlarmManager] 재생할 알람 사운드가 없습니다.");
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
// --- END OF FILE AlarmManager.cs ---