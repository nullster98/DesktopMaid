using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsManager : MonoBehaviour
{
    [Header("연결할 UI 및 컴포넌트")]
    [SerializeField] private Slider volumeSlider; // 볼륨 조절 UI 슬라이더
    [SerializeField] private AudioSource notificationAudioSource; // NotificationManager의 AudioSource

    // PlayerPrefs에 저장할 때 사용할 키 (오타 방지를 위해 상수로 관리)
    private const string NotificationVolumeKey = "NotificationVolume";

    void Start()
    {
        // 슬라이더나 오디오소스가 연결되지 않았으면 경고를 출력하고 종료
        if (volumeSlider == null || notificationAudioSource == null)
        {
            Debug.LogError("VolumeSlider 또는 NotificationAudioSource가 연결되지 않았습니다!");
            return;
        }

        // 1. 앱 시작 시 저장된 볼륨 값을 불러옴. 만약 저장된 값이 없으면 기본값 1 (최대 볼륨) 사용
        float savedVolume = PlayerPrefs.GetFloat(NotificationVolumeKey, 1.0f);

        // 2. 불러온 값으로 오디오 소스의 볼륨과 슬라이더의 현재 값을 설정
        notificationAudioSource.volume = savedVolume;
        volumeSlider.value = savedVolume;

        // 3. 슬라이더의 값이 변경될 때마다 OnVolumeChanged 함수가 호출되도록 리스너 추가
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    /// <summary>
    /// 슬라이더 값이 변경될 때 호출되는 함수
    /// </summary>
    /// <param name="value">슬라이더의 새로운 값 (0.0 ~ 1.0)</param>
    private void OnVolumeChanged(float value)
    {
        // 전달받은 값으로 오디오 소스의 볼륨을 실시간으로 업데이트
        notificationAudioSource.volume = value;

        // 변경된 값을 PlayerPrefs에 저장하여 다음 실행 시에도 유지되도록 함
        PlayerPrefs.SetFloat(NotificationVolumeKey, value);
    }

    // 오브젝트가 파괴될 때 리스너를 제거하여 메모리 누수를 방지 (좋은 습관)
    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        }
    }
}