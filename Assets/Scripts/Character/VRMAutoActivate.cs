// --- START OF FILE VRMAutoActivate.cs ---

using System;
using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

public class VRMAutoActivate : MonoBehaviour
{
    #region Windows API Imports
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern System.IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    #endregion

    #region Inspector Fields
    [Header("자동 행동 설정")]
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private float idleTimeThreshold = 10f;
    [SerializeField] private string[] randomTriggers;
    [SerializeField] private float maxWalkDistance = 1f;

    [Tooltip("true로 설정하면 캐릭터가 현재 자신이 있는 모니터의 경계 안에서만 걷습니다.")]
    [SerializeField] private bool restrictToCurrentMonitor = false; 

    [Header("회전 설정")]
    [Tooltip("자동 행동 시의 부드러운 회전 속도")]
    [SerializeField] private float autoRotationSpeed = 180f;
    
    [Header("알람 행동 설정")]
    [Tooltip("알람 시 재생할 댄스 애니메이션 트리거 이름 목록입니다.")]
    [SerializeField] private string[] alarmDanceTriggers;
    [Tooltip("알람 댄스 사이의 대기 시간입니다.")]
    [SerializeField] private float alarmActionInterval = 5f;
    
    #endregion

    #region Private Fields
    private Animator animator;
    private float moveDirection;
    private float idleTimer = 0f;
    private Vector3 walkStartPos;
    private Quaternion userTargetRotation;
    private Quaternion walkTargetRotation;
    private Camera mainCamera;
    
    private float minWalkableX;
    private float maxWalkableX;
    
    private bool isInAlarmState = false;
    private Coroutine alarmCoroutine;

    [SerializeField] private bool isUnderUserRotation = false;
    [SerializeField] private bool isSnapped = false;
    [SerializeField] private CharacterPreset preset;
    #endregion

    #region Unity Lifecycle Methods
    private void OnEnable()
    {
        CameraManager.OnCameraZoom += HandleCameraZoom;
    }

    private void OnDisable()
    {
        CameraManager.OnCameraZoom -= HandleCameraZoom;
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
        if (animator != null) animator.speed = 0.7f;
        userTargetRotation = transform.rotation;
    }

    void Update()
    {
        if (preset == null || isSnapped || isInAlarmState) return;

        if (preset.isAutoMoveEnabled)
        {
            HandleAutoActions();
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
        idleTimer = 0f;
    }

    public void SetAlarmState(bool isAlarming)
    {
        // [핵심] 자신이 동작할 조건이 아니면(스냅 상태이면) 즉시 종료합니다.
        if (isSnapped)
        {
            return;
        }
        
        isInAlarmState = isAlarming;
        if (animator == null) return;

        if (isAlarming)
        {
            StopWalking();
            isUnderUserRotation = true;
            
            if(alarmCoroutine != null) StopCoroutine(alarmCoroutine);
            alarmCoroutine = StartCoroutine(StartAlarmSequenceRoutine());
        }
        else
        {
            if(alarmCoroutine != null)
            {
                StopCoroutine(alarmCoroutine);
                alarmCoroutine = null;
            }
            
            animator.SetTrigger("ForceIdle");
            isUnderUserRotation = false;
            idleTimer = 0f;
        }
    }
    #endregion

    #region Core Logic

    private IEnumerator StartAlarmSequenceRoutine()
    {
        Debug.Log($"[{preset.characterName}] 현재 행동 중단 및 Idle 상태로 전환 시도...");
        animator.SetTrigger("ForceIdle");
        
        yield return new WaitForEndOfFrame();
        
        Debug.Log($"[{preset.characterName}] Idle 상태 확인. 댄스 루틴 시작.");
        yield return StartCoroutine(AlarmDanceRoutine());
    }

    private IEnumerator AlarmDanceRoutine()
    {
        if (alarmDanceTriggers == null || alarmDanceTriggers.Length == 0)
        {
            Debug.LogWarning($"[{preset.characterName}] 알람 댄스 애니메이션이 설정되지 않았습니다.");
            yield break;
        }
        
        string firstTrigger = alarmDanceTriggers[UnityEngine.Random.Range(0, alarmDanceTriggers.Length)];
        Debug.Log($"[{preset.characterName}] 첫 알람 댄스 재생: {firstTrigger}");
        animator.SetTrigger(firstTrigger);

        while (isInAlarmState)
        {
            yield return new WaitForSeconds(alarmActionInterval);
            if (!isInAlarmState) yield break;
            
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
            {
                string nextTrigger = alarmDanceTriggers[UnityEngine.Random.Range(0, alarmDanceTriggers.Length)];
                Debug.Log($"[{preset.characterName}] 다음 알람 댄스 재생: {nextTrigger}");
                animator.SetTrigger(nextTrigger);
            }
            else
            {
                Debug.LogWarning($"[{preset.characterName}] 다음 댄스를 위해 Idle 상태를 기다리는 중...");
            }
        }
    }

    private void HandleCameraZoom(float oldSize, float newSize)
    {
        if (isSnapped || (animator != null && animator.GetBool("Drag")) || mainCamera == null)
        {
            return;
        }
        
        StopWalking();

        Vector3 offsetFromCenter = transform.position - mainCamera.transform.position;
        float ratio = newSize / oldSize;
        Vector3 newOffset = offsetFromCenter * ratio;
        Vector3 newPosition = mainCamera.transform.position + newOffset;
        newPosition.z = transform.position.z;
        transform.position = newPosition;
    }
    
    private void HandleAutoActions()
    {
        if (animator == null || isUnderUserRotation) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        
        if (state.IsName("Walking"))
        {
            idleTimer = 0f;
            Vector3 nextPos = transform.position + new Vector3(moveDirection * moveSpeed * transform.localScale.x * Time.deltaTime, 0f, 0f);
            if (nextPos.x < minWalkableX || nextPos.x > maxWalkableX || Vector3.Distance(walkStartPos, nextPos) >= maxWalkDistance * transform.localScale.x)
            {
                StopWalking();
            }
            else
            {
                transform.position = nextPos;
            }
        }
        else if (state.IsName("Idle"))
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleTimeThreshold)
            {
                PlayRandomIdleAction();
                idleTimer = 0f;
            }
        }
        else
        {
            idleTimer = 0f;
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

        Vector3 euler = finalTargetRotation.eulerAngles;
        euler.z = 0;
        finalTargetRotation = Quaternion.Euler(euler);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, finalTargetRotation, autoRotationSpeed * Time.deltaTime);
    }

    private void PlayRandomIdleAction()
    {
        if (UnityEngine.Random.value < 0.3f)
            StartWalking();
        else
            TriggerRandomAnimation();
    }

    private void StartWalking()
    {
        if (animator == null || mainCamera == null) return;

        animator.SetBool("Walking", true);
        walkStartPos = transform.position;
        moveDirection = (UnityEngine.Random.value < 0.5f) ? 1f : -1f;

        float distance = Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
        float padding = 0.7f * transform.localScale.x;

        if (restrictToCurrentMonitor)
        {
            Vector2 screenPoint = mainCamera.WorldToScreenPoint(transform.position);
            POINT desktopPoint = new POINT { x = (int)screenPoint.x + FullScreenAuto.VirtualScreenX, y = (int)(Screen.height - screenPoint.y) + FullScreenAuto.VirtualScreenY };
            IntPtr monitorHandle = MonitorFromPoint(desktopPoint, MONITOR_DEFAULTTOPRIMARY);
            MONITORINFO monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(monitorHandle, ref monitorInfo);
            
            float monitorLeftScreenX = monitorInfo.rcMonitor.Left - FullScreenAuto.VirtualScreenX;
            float monitorRightScreenX = monitorInfo.rcMonitor.Right - FullScreenAuto.VirtualScreenX;

            minWalkableX = mainCamera.ScreenToWorldPoint(new Vector3(monitorLeftScreenX, 0, distance)).x + padding;
            maxWalkableX = mainCamera.ScreenToWorldPoint(new Vector3(monitorRightScreenX, 0, distance)).x - padding;
        }
        else
        {
            Vector3 screenLeftWorld = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, distance));
            Vector3 screenRightWorld = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, 0, distance));

            minWalkableX = screenLeftWorld.x + padding;
            maxWalkableX = screenRightWorld.x - padding;
        }

        float rotY = (moveDirection > 0) ? 90f : -90f;
        walkTargetRotation = Quaternion.Euler(0, rotY, 0);
    }

    private void TriggerRandomAnimation()
    {
        if (animator == null || randomTriggers == null || randomTriggers.Length == 0) return;
        string triggerName = randomTriggers[UnityEngine.Random.Range(0, randomTriggers.Length)];
        animator.SetTrigger(triggerName);
    }
    #endregion
}