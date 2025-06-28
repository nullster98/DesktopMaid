// --- START OF FILE VRMAutoActivate.cs ---

using UnityEngine;

public class VRMAutoActivate : MonoBehaviour
{
    #region Inspector Fields
    [Header("자동 행동 설정")]
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private float idleTimeThreshold = 10f;
    [SerializeField] private string[] randomTriggers;
    [SerializeField] private float maxWalkDistance = 1f;
    [Header("회전 설정")]
    [Tooltip("자동 행동 시의 부드러운 회전 속도")]
    [SerializeField] private float autoRotationSpeed = 180f;
    #endregion

    #region Private Fields
    private Animator animator;
    private float moveDirection;
    private float idleTimer = 0f;
    private Vector3 walkStartPos;
    private Quaternion userTargetRotation;
    private Quaternion walkTargetRotation;
    private Camera mainCamera;

    // 이동 가능 범위 저장을 위한 변수
    private float minWalkableX;
    private float maxWalkableX;

    [SerializeField] private bool isPlayingRandom = false;
    [SerializeField] private bool isUnderUserRotation = false;
    [SerializeField] private bool isSnapped = false; // 앉기 상태 플래그
    [SerializeField] private CharacterPreset preset;
    #endregion

    #region Unity Lifecycle Methods
    private void OnEnable()
    {
        // 카메라 줌 이벤트 구독
        CameraManager.OnCameraZoom += HandleCameraZoom;
    }

    private void OnDisable()
    {
        // 카메라 줌 이벤트 구독 해제 (메모리 누수 방지)
        CameraManager.OnCameraZoom -= HandleCameraZoom;
    }

    private void Start()
    {
        isPlayingRandom = false;
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
        if (animator != null) animator.speed = 0.7f;
        userTargetRotation = transform.rotation;
    }

    private void Update()
    {
        if (preset == null || isSnapped) return;

        switch (preset.CurrentMode)
        {
            case CharacterMode.Activated:
                HandleAutoActions();
                break;
            case CharacterMode.Sleep:
                break;
        }
        HandleRotation();
    }
    #endregion

    #region Public Methods
    public CharacterPreset GetPreset()
    {
        return preset;
    }

    public void SetSnappedState(bool snapped)
    {
        isSnapped = snapped;
        if (snapped)
        {
            StopWalking();
        }
    }

    public void SetPreset(CharacterPreset p)
    {
        preset = p;
    }

    public void SetUserRotationStatus(bool isRotating)
    {
        isUnderUserRotation = isRotating;
    }

    public void SyncTargetRotationToCurrent()
    {
        userTargetRotation = transform.rotation;
    }

    public void StopWalking()
    {
        if (animator != null && animator.GetBool("Walking"))
        {
            animator.SetBool("Walking", false);
        }
        isPlayingRandom = false;
        idleTimer = 0f;
    }
    #endregion

    #region Core Logic

    private void HandleCameraZoom(float oldSize, float newSize)
    {
        // 스냅 상태(앉아있는 상태)이거나 드래그 중이면 위치 보정하지 않음
        if (isSnapped || (animator != null && animator.GetBool("Drag")) || mainCamera == null)
        {
            return;
        }
        
        // [수정] 줌 변경 시 현재 걷기 동작을 멈춰서, 다음에 걸을 때 화면 경계를 새로 계산하도록 함
        StopWalking();

        // 1. 카메라 중심으로부터 캐릭터까지의 현재 월드 오프셋 계산
        Vector3 offsetFromCenter = transform.position - mainCamera.transform.position;

        // 2. 카메라 크기 변경 비율 계산
        float ratio = newSize / oldSize;

        // 3. 현재 오프셋에 비율 적용하여 새로운 오프셋 산출
        Vector3 newOffset = offsetFromCenter * ratio;

        // 4. 카메라 중심에 새로운 오프셋 더해 최종 위치 계산 및 적용
        Vector3 newPosition = mainCamera.transform.position + newOffset;
        // Z축 위치는 변경하지 않음 (깊이 고정)
        newPosition.z = transform.position.z;
        transform.position = newPosition;
    }

    private void HandleAutoActions()
    {
        if (animator == null || isUnderUserRotation) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (isPlayingRandom && state.IsName("Idle"))
        {
            isPlayingRandom = false;
            idleTimer = 0f;
        }

        if (!isPlayingRandom && state.IsName("Idle"))
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleTimeThreshold) PlayRandomIdleAction();
        }

        if (state.IsName("Walking"))
        {
            Vector3 nextPos = transform.position + new Vector3(moveDirection * moveSpeed * transform.localScale.x * Time.deltaTime, 0f, 0f);

            // [수정] maxWalkDistance 체크와 화면 경계 체크를 함께 수행
            if (nextPos.x < minWalkableX || nextPos.x > maxWalkableX || Vector3.Distance(walkStartPos, nextPos) >= maxWalkDistance * transform.localScale.x)
            {
                StopWalking();
            }
            else
            {
                transform.position = nextPos;
            }
        }
    }

    private void HandleRotation()
    {
        if (isUnderUserRotation || animator == null) return;

        Quaternion finalTargetRotation;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (state.IsName("Walking"))
        {
            finalTargetRotation = walkTargetRotation;
        }
        else
        {
            finalTargetRotation = userTargetRotation;
        }

        // Z축 회전 고정
        Vector3 euler = finalTargetRotation.eulerAngles;
        euler.z = 0;
        finalTargetRotation = Quaternion.Euler(euler);

        // 부드럽게 회전 적용
        transform.rotation = Quaternion.RotateTowards(transform.rotation, finalTargetRotation, autoRotationSpeed * Time.deltaTime);
    }

    private void PlayRandomIdleAction()
    {
        // 일정 확률로 Walking 또는 다른 랜덤 트리거 실행
        if (UnityEngine.Random.value < 0.3f)
            StartWalking();
        else
            TriggerRandomAnimation();
    }

    private void StartWalking()
    {
        if (animator == null || mainCamera == null) return;

        // 걷기 시작 설정
        animator.SetBool("Walking", true);
        isPlayingRandom = true;
        idleTimer = 0f;
        walkStartPos = transform.position;
        moveDirection = (UnityEngine.Random.value < 0.5f) ? 1f : -1f;

        // --- [수정된 부분 시작] ---
        // 이동 가능 X 범위를 현재 카메라 뷰 기준으로 계산합니다.
        float distance = Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
        
        // 화면의 왼쪽과 오른쪽 끝 월드 좌표를 얻습니다.
        Vector3 screenLeftWorld = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, distance));
        Vector3 screenRightWorld = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, 0, distance));

        // 캐릭터가 화면 밖으로 완전히 나가지 않도록 패딩 값을 줍니다. (캐릭터 너비의 절반 정도)
        float padding = 0.5f * transform.localScale.x;

        minWalkableX = screenLeftWorld.x + padding;
        maxWalkableX = screenRightWorld.x - padding;
        // --- [수정된 부분 끝] ---

        // 바라보는 방향 결정 (좌/우 회전)
        float rotY = (moveDirection > 0) ? 90f : -90f;
        walkTargetRotation = Quaternion.Euler(0, rotY, 0);
    }

    private void TriggerRandomAnimation()
    {
        if (animator == null || randomTriggers == null || randomTriggers.Length == 0) return;

        // 랜덤 트리거 실행
        string triggerName = randomTriggers[UnityEngine.Random.Range(0, randomTriggers.Length)];
        animator.SetTrigger(triggerName);
        isPlayingRandom = true;
        idleTimer = 0f;
    }
    #endregion
}