using System;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    // [핵심 추가] Singleton 패턴으로 어디서든 쉽게 접근 가능하도록 합니다.
    public static CameraManager Instance { get; private set; }

    [Header("전체 줌 설정 (Camera Size)")]
    [SerializeField] private float minCameraSize = 1.0f;
    [SerializeField] private float maxCameraSize = 3.0f;

    private Camera mainCamera;
    
    // 파라미터: <이전 Size, 새 Size>
    public static event Action<float, float> OnCameraZoom;
    
    // 다른 스크립트에서 현재 카메라 설정을 읽을 수 있도록 public 프로퍼티 추가
    public float MinCameraSize => minCameraSize;
    public float MaxCameraSize => maxCameraSize;
    public float CurrentCameraSize => mainCamera ? mainCamera.orthographicSize : 0f;

    private void Awake()
    {
        // Singleton 인스턴스 설정
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
        
        mainCamera = Camera.main;
        if (mainCamera == null || !mainCamera.orthographic)
        {
            Debug.LogError("Orthographic 타입의 Main Camera를 찾을 수 없습니다!");
            this.enabled = false;
        }
        
    }

    void Start()
    {
        
    }

    void Update()
    {
        HandleGlobalZoomWithShortcut();
    }

    private void HandleGlobalZoomWithShortcut()
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newSize = mainCamera.orthographicSize - Mathf.Sign(scroll) * 0.1f;
                SetZoomLevel(newSize); // UI 슬라이더와 동일한 로직을 사용
            }
        }
    }

    // [핵심 추가] UI 슬라이더 등 외부에서 호출할 Public 메서드
    public void SetZoomLevel(float newSize)
    {
        if (mainCamera == null) return;

        float oldSize = mainCamera.orthographicSize;
        
        // 값을 범위 내로 제한
        float clampedSize = Mathf.Clamp(newSize, minCameraSize, maxCameraSize);

        // 실제 값이 변경되었는지 확인
        if (Mathf.Abs(mainCamera.orthographicSize - clampedSize) > 0.001f)
        {
            mainCamera.orthographicSize = clampedSize;
            
            // 크기가 실제로 변경되었을 때만 이벤트를 방송합니다.
            OnCameraZoom?.Invoke(oldSize, mainCamera.orthographicSize);
        }
    }
}