// --- START OF FILE NotificationManager.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    public GameObject notificationPrefab;
    public Transform notificationContainer;
    public float displayDuration = 5f;
    public float moveUpDuration = 0.5f;
    public float fadeOutDuration = 0.3f;
    public Vector3 initialOffset = new Vector3(0, -100, 0);

    [Header("Audio Setting")] 
    public AudioClip notificationSound;
    private AudioSource audioSource;
    
    private Queue<NotificationRequest> notificationQueue = new Queue<NotificationRequest>();
    private bool isShowingNotification = false;

    private struct NotificationRequest
    {
        public CharacterPreset preset;
        public string messagePreview;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowNotification(CharacterPreset preset, string messagePreview = "새 메시지가 도착했습니다!")
    {
        if (notificationPrefab == null || notificationContainer == null)
        {
            Debug.LogError("Notification Prefab 또는 Container가 설정되지 않았습니다.");
            return;
        }

        // [수정] 사용자 모드가 Off이면 어떤 알림도 큐에 추가하지 않음
        if (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Off)
        {
            Debug.Log("[NotificationManager] 사용자 모드가 Off이므로 알림을 무시합니다.");
            return;
        }

        notificationQueue.Enqueue(new NotificationRequest { preset = preset, messagePreview = messagePreview });
        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;
        while (notificationQueue.Count > 0)
        {
            NotificationRequest request = notificationQueue.Dequeue();
            yield return StartCoroutine(DisplaySingleNotification(request.preset, request.messagePreview));
        }
        isShowingNotification = false;
    }

    private IEnumerator DisplaySingleNotification(CharacterPreset preset, string messagePreview)
    {
        // [추가] Sleep 모드인지 확인
        bool isSleepMode = (UserData.Instance != null && UserData.Instance.CurrentUserMode == UserMode.Sleep);

        GameObject notificationInstance = Instantiate(notificationPrefab, notificationContainer);
        RectTransform rectTransform = notificationInstance.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = notificationInstance.GetComponent<CanvasGroup>() ?? notificationInstance.AddComponent<CanvasGroup>();
        
        Button button = notificationInstance.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() =>
            {
                if (notificationInstance != null) Destroy(notificationInstance);
                StopAllCoroutines();
                isShowingNotification = false;
                StartCoroutine(ProcessNotificationQueue());
            });
        }
        
        Image charImage = notificationInstance.transform.Find("Background/ProfileMask/CharacterImage")?.GetComponent<Image>();
        TMP_Text nameText = notificationInstance.transform.Find("Background/CharacterNameText")?.GetComponent<TMP_Text>();
        if (charImage != null && preset.characterImage != null) charImage.sprite = preset.characterImage.sprite;
        if (nameText != null) nameText.text = preset.characterName;

        // [수정] Sleep 모드일 경우, 팝업 애니메이션을 건너뛰고 Red Dot만 표시하고 종료
        if (isSleepMode)
        {
            Debug.Log($"[NotificationManager] Sleep 모드이므로 '{preset.characterName}'의 팝업 알림을 생략합니다.");
            // Red Dot 표시는 AIScreenObserver에서 이미 처리하고 있으므로 여기서 추가로 할 일 없음.
            Destroy(notificationInstance); // 생성했던 팝업 인스턴스는 즉시 파괴
            yield break; // 이 알림 처리는 여기서 즉시 종료
        }

        // --- 이하 팝업 애니메이션 로직 (Sleep 모드가 아닐 때만 실행됨) ---
        
        // 알림 사운드 재생
        if (notificationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(notificationSound);
        }

        Vector3 finalTargetPosition = notificationPrefab.GetComponent<RectTransform>().anchoredPosition;
        Vector3 startPosition = finalTargetPosition + initialOffset;
        rectTransform.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;
        
        float elapsedTime = 0f;
        while (elapsedTime < moveUpDuration)
        {
            float t = elapsedTime / moveUpDuration;
            rectTransform.anchoredPosition = Vector3.Lerp(startPosition, finalTargetPosition, t);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        rectTransform.anchoredPosition = finalTargetPosition;
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        elapsedTime = 0f;
        Vector3 currentPosition = rectTransform.anchoredPosition;
        Vector3 endPosition = currentPosition + new Vector3(0, rectTransform.rect.height, 0);
        
        while (elapsedTime < fadeOutDuration)
        {
            float t = elapsedTime / fadeOutDuration;
            rectTransform.anchoredPosition = Vector3.Lerp(currentPosition, endPosition, t);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(notificationInstance);
    }
}