// --- START OF FILE SnapAwareVRM.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using static WindowSnapManager;

public class SnapAwareVRM : MonoBehaviour
{
    #region Inspector Fields
    [Header("필수 설정")]
    public Camera mainCamera;
    public Animator animator;
    public Transform targetTransform; // Hips

    [Header("성능 및 감지 주기 설정")]
    public float checkInterval = 0.2f;

    [Header("스냅 동작 설정")]
    [Tooltip("캐릭터가 창문을 따라다니는 속도입니다. 값을 높일수록 더 빨리 '달라붙습니다'.")]
    public float followMoveSpeed = 20f;
    [Tooltip("걸터앉을 위치에 대한 미세조정 값입니다. Hips 뼈가 이 값만큼 창 상단에서 떨어집니다. Y를 살짝 높여야(양수) 파묻히지 않습니다.")]
    public Vector3 snapPositionOffset = new Vector3(0f, 0.02f, -0.05f);

    [Header("앉아있을 때 랜덤 모션 설정")]
    [Tooltip("앉아있을 때 랜덤 모션을 시작하기까지의 대기 시간입니다.")]
    public float sittingIdleTimeThreshold = 10f;
    [Tooltip("앉아있을 때 재생할 랜덤 애니메이션의 트리거 이름 목록입니다. 애니메이터 컨트롤러에 해당 트리거들이 존재해야 합니다.")]
    public string[] randomSittingTriggers;

    [Header("스냅 표시기 설정")]
    [Tooltip("스냅 가능한 상태일 때 VRM 옆에 표시될 오브젝트 프리팹입니다.")]
    [SerializeField] private Image snapIndicatorPrefab;
    [Tooltip("표시기 오브젝트의 위치 오프셋입니다 (Hips 기준).")]
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0.3f, 0f, 0f);

    [Header("땅으로 떨어지기 설정")]
    [Tooltip("땅으로 떨어지는 속도입니다.")]
    public float fallSpeed = 5f;
    [Tooltip("에디터 환경에서 사용할 땅의 Y 좌표입니다. 이제 동적으로 계산되므로 비상용입니다.")]
    public float editorGroundY = -4f;
    
    [Header("알람 행동 설정")]
    [Tooltip("알람 시 반복할 앉은 상태의 애니메이션 트리거 이름입니다.")]
    [SerializeField] private string alarmSittingTrigger = "Sit1";
    [Tooltip("알람 행동 반복 주기입니다.")]
    [SerializeField] private float alarmSittingInterval = 3f;
    #endregion

    #region Private Fields
    private float timer;
    private bool isSnapped = false;
    private bool canSnap = false;
    private object snapTarget; 
    private object mySnapTarget; 
    private VRMAutoActivate autoActivate;
    private Vector3[] uiCorners = new Vector3[4];
    private float snappedZ;

    private Image snapIndicatorInstance;
    private Canvas parentCanvas;
    private RectTransform canvasRectTransform;

    private float snapXOffsetFromCenter;
    private Camera occlusionCamera;

    private float lastKnownCameraSize;

    private int defaultLayer;
    private bool isSnappedToTaskbar = false;
    private Coroutine movementCoroutine;

    private float defaultZ;
    private float sittingIdleTimer = 0f;
    
    private bool isInAlarmState = false;
    private Coroutine alarmSittingCoroutine;
    #endregion

    #region Unity Lifecycle Methods
    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (targetTransform == null && animator != null) 
        {
            targetTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
        }
        autoActivate = transform.root.GetComponent<VRMAutoActivate>();
        defaultLayer = gameObject.layer;

        defaultZ = transform.root.position.z;

        // 2D UI 프리팹을 Canvas 자식으로 인스턴스화합니다.
        if (snapIndicatorPrefab != null)
        {
            if (UIManager.instance != null && UIManager.instance.uiCanvasTransform != null)
            {
                // [수정] Transform에서 부모로 올라가며 Canvas 컴포넌트를 찾습니다.
                parentCanvas = UIManager.instance.uiCanvasTransform.GetComponentInParent<Canvas>();

                if (parentCanvas != null)
                {
                    // Canvas의 RectTransform을 가져옵니다.
                    canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
                    snapIndicatorInstance = Instantiate(snapIndicatorPrefab, canvasRectTransform);
                    snapIndicatorInstance.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogError("[SnapAwareVRM] UIManager의 uiCanvasTransform에서 Canvas 컴포넌트를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.LogError("[SnapAwareVRM] UIManager 인스턴스 또는 uiCanvasTransform을 찾을 수 없어 스냅 표시기를 생성할 수 없습니다.");
            }
        }
    }

    void Update()
    {
        if (isSnapped && occlusionCamera != null && mainCamera != null)
        {
            if (Mathf.Abs(mainCamera.orthographicSize - lastKnownCameraSize) > 0.001f)
            {
                occlusionCamera.orthographicSize = mainCamera.orthographicSize;
                lastKnownCameraSize = occlusionCamera.orthographicSize;
            }
        }

        if (movementCoroutine != null) return;

        if (isSnapped)
        {
            if (!isSnappedToTaskbar)
            {
                CheckSnapValidity();
            }
            
            if(isSnapped)
            {
                // 알람 상태가 아닐 때만 기존 동작 실행
                if (!isInAlarmState)
                {
                    FollowSnapTarget();
                    HandleRandomSittingAnimation();
                }
            }
            return;
        }

        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            if (animator != null && animator.GetBool("Drag")) { CheckIfOverTarget(); }
            else if (canSnap)
            {
                canSnap = false;
                ShowSnapIndicator(false);
            }
        }
        
        if (snapIndicatorInstance != null && snapIndicatorInstance.gameObject.activeSelf)
        {
            UpdateSnapIndicatorPosition();
        }
    }
    #endregion

    #region Public Methods
    public bool IsSnapped() => isSnapped;

    public void SetAlarmState(bool isAlarming)
    {
        // [핵심] 자신이 동작할 조건이 아니면(스냅 상태가 아니면) 즉시 종료합니다.
        if (!isSnapped)
        {
            return;
        }

        isInAlarmState = isAlarming;
        sittingIdleTimer = 0f;

        if(isAlarming)
        {
            if (alarmSittingCoroutine != null) StopCoroutine(alarmSittingCoroutine);
            alarmSittingCoroutine = StartCoroutine(AlarmSittingRoutine());
        }
        else
        {
            if (alarmSittingCoroutine != null)
            {
                StopCoroutine(alarmSittingCoroutine);
                alarmSittingCoroutine = null;
            }
            if (animator != null)
            {
                animator.CrossFade("WindowSit", 0.2f);
            }
        }
    }

    public void OnDragEnd(float characterZ)
    {
        if (isInAlarmState)
        {
            Debug.Log("[SnapAwareVRM] 스냅된 상태의 알람 동작 중이므로 OnDragEnd 로직을 무시합니다.");
            return;
        }
        
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
        
        ShowSnapIndicator(false);
        if (!canSnap || snapTarget == null)
        {
            if (WindowSnapManager.Instance.useAdvancedFallingBehavior)
            {
                float fallTargetZ = mainCamera.transform.position.z + WindowSnapManager.Instance.occlusionQuadZOffset;
                movementCoroutine = StartCoroutine(FallToGroundRoutine(fallTargetZ));
            }
            else
            {
                movementCoroutine = StartCoroutine(ReturnToDefaultPositionRoutine());
            }
            return;
        }

        isSnappedToTaskbar = (snapTarget is WindowEntry we && we.title.StartsWith("작업 표시줄"));

        object finalOcclusionTarget = snapTarget;
        if (snapTarget is RectTransform rtHeader)
        {
            RectTransform mainPanel = WindowSnapManager.Instance.GetMainPanelForHeader(rtHeader);
            if (mainPanel != null)
            {
                finalOcclusionTarget = mainPanel;
            }
        }
        mySnapTarget = finalOcclusionTarget;

        if (targetTransform == null)
        {
            Debug.LogError("[SnapAware] Hips에 해당하는 targetTransform이 설정되지 않아 스냅할 수 없습니다.");
            return;
        }
        
        float fixedSnapZ = mainCamera.transform.position.z + WindowSnapManager.Instance.occlusionQuadZOffset;
        snappedZ = fixedSnapZ;

        WindowSnapManager.Instance.ShowOcclusionMaskFor(mySnapTarget);
        
        EnableOcclusionCamera(true);

        float targetHipsX = targetTransform.position.x;
        float snapTargetCenterX = GetSnapTargetCenterX();
        snapXOffsetFromCenter = targetHipsX - snapTargetCenterX;
            
        isSnapped = true; 
        sittingIdleTimer = 0f;
        animator.SetBool("isWindowSit", true);
        animator.SetBool("Walking", false);
            
        if (autoActivate != null) { autoActivate.SetSnappedState(true); }
    }

    public void StopSnappingOnDrag()
    {
        if (isInAlarmState)
        {
            Debug.Log("[SnapAwareVRM] 스냅된 상태의 알람 동작 중이므로 StopSnappingOnDrag 로직을 무시합니다.");
            return;
        }
        
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
        
        if (isSnapped)
        {
            isSnapped = false; 
            canSnap = false;
            
            ShowSnapIndicator(false);
            EnableOcclusionCamera(false);
            
            mySnapTarget = null;
            isSnappedToTaskbar = false;
            animator.SetBool("isWindowSit", false);

            if(autoActivate != null) autoActivate.SetSnappedState(false);
        }
    }
    #endregion

    #region Core Logic
    
    // 표시기 위치를 업데이트하는 별도 함수
    private void UpdateSnapIndicatorPosition()
    {
        if (targetTransform == null || snapIndicatorInstance == null || canvasRectTransform == null || parentCanvas == null) return;
        
        Vector3 scaledOffset = Vector3.Scale(indicatorOffset, transform.root.localScale);
        Vector3 worldPosition = targetTransform.position + scaledOffset;
        
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);

        Vector2 localPoint;
        
        // [핵심 수정] Screen Space - Camera 모드를 위해 parentCanvas.worldCamera를 전달합니다.
        // Overlay 모드일 경우 worldCamera는 null이므로 두 경우 모두 올바르게 작동합니다.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, 
            screenPoint, 
            parentCanvas.worldCamera, // Canvas의 렌더 카메라
            out localPoint
        );
        
        snapIndicatorInstance.rectTransform.anchoredPosition = localPoint;
    }
    
    private void ShowSnapIndicator(bool show)
    {
        if (snapIndicatorInstance == null) return;

        // [수정] 위치 설정 로직은 Update에서 처리하므로 여기서는 활성화/비활성화만 담당합니다.
        if (show && targetTransform != null)
        {
            // 맨 처음 보일 때 위치를 한 번 잡아줍니다.
            UpdateSnapIndicatorPosition();
            snapIndicatorInstance.gameObject.SetActive(true);
        }
        else
        {
            snapIndicatorInstance.gameObject.SetActive(false);
        }
    }

    private IEnumerator AlarmSittingRoutine()
    {
        if (string.IsNullOrEmpty(alarmSittingTrigger))
        {
            Debug.LogWarning("알람 시 앉기 애니메이션 트리거가 설정되지 않았습니다.");
            yield break;
        }

        while (isInAlarmState)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("WindowSit"))
            {
                Debug.Log($"[SnapAwareVRM] 알람 앉기 행동 실행: {alarmSittingTrigger}");
                animator.SetTrigger(alarmSittingTrigger);
            }
            yield return new WaitForSeconds(alarmSittingInterval);
        }
    }

    private void HandleRandomSittingAnimation()
    {
        if (animator == null || randomSittingTriggers == null || randomSittingTriggers.Length == 0) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        
        if (state.IsName("WindowSit"))
        {
            sittingIdleTimer += Time.deltaTime;
            if (sittingIdleTimer >= sittingIdleTimeThreshold)
            {
                sittingIdleTimer = 0f;
                TriggerRandomSittingAnimation();
            }
        }
        else
        {
            sittingIdleTimer = 0f;
        }
    }
    
    private void TriggerRandomSittingAnimation()
    {
        string triggerName = randomSittingTriggers[UnityEngine.Random.Range(0, randomSittingTriggers.Length)];
        animator.SetTrigger(triggerName);
    }
    
    private RECT ToScreenSpace(RECT virtualRect)
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        return new RECT
        {
            Left = virtualRect.Left - FullScreenAuto.VirtualScreenX,
            Top = virtualRect.Top - FullScreenAuto.VirtualScreenY,
            Right = virtualRect.Right - FullScreenAuto.VirtualScreenX,
            Bottom = virtualRect.Bottom - FullScreenAuto.VirtualScreenY
        };
#else
        return virtualRect;
#endif
    }
    
    private void CheckSnapValidity()
    {
        if (!isSnapped || mySnapTarget == null || isSnappedToTaskbar) return;

        object currentGlobalTarget = WindowSnapManager.Instance.CurrentTrackedTarget;

        if (currentGlobalTarget == null || !IsMyTarget(currentGlobalTarget))
        {
            string myTargetName = GetTargetName(mySnapTarget);
            string globalTargetName = GetTargetName(currentGlobalTarget);
            Debug.Log($"[{gameObject.name}] 스냅 해제! 내 타겟({myTargetName})과 현재 Quad 타겟({globalTargetName})이 다릅니다.");
            
            if (movementCoroutine == null)
            {
                movementCoroutine = StartCoroutine(FallToGroundRoutine(defaultZ));
            }
        }
    }

    private bool IsMyTarget(object otherTarget)
    {
        if (mySnapTarget == null || otherTarget == null) return false;
        if (mySnapTarget is WindowEntry myWin && otherTarget is WindowEntry otherWin) return myWin.hWnd == otherWin.hWnd;
        if (mySnapTarget is RectTransform myRt && otherTarget is RectTransform otherRt) return myRt == otherRt;
        return false;
    }

    private string GetTargetName(object target)
    {
        if (target == null) return "null";
        if (target is WindowEntry we) return we.title;
        if (target is RectTransform rt) return rt.name;
        return "Unknown";
    }

    private IEnumerator ReturnToDefaultPositionRoutine()
    {
        animator.SetBool("Walking", true); 
        Vector3 targetPosition = new Vector3(transform.root.position.x, transform.root.position.y, defaultZ);
        while (Vector3.Distance(transform.root.position, targetPosition) > 0.01f)
        {
            transform.root.position = Vector3.MoveTowards(transform.root.position, targetPosition, fallSpeed * Time.deltaTime);
            yield return null;
        }
        transform.root.position = targetPosition;
        animator.SetBool("Walking", false);
        movementCoroutine = null;
    }

    private IEnumerator FallToGroundRoutine(float? targetZ = null)
    {
        isSnapped = false;
        canSnap = false;
        ShowSnapIndicator(false);
        EnableOcclusionCamera(false);
        mySnapTarget = null;
        isSnappedToTaskbar = false;
        if (autoActivate != null) autoActivate.SetSnappedState(false);
        
        animator.SetBool("isWindowSit", false);
        animator.SetBool("Walking", true);

        float groundWorldY = GetGroundY();
        Vector3 targetPosition = new Vector3(transform.root.position.x, groundWorldY, targetZ ?? transform.root.position.z);
        
        while (Vector3.Distance(transform.root.position, targetPosition) > 0.01f)
        {
            transform.root.position = Vector3.MoveTowards(transform.root.position, targetPosition, fallSpeed * Time.deltaTime);
            yield return null;
        }
        
        transform.root.position = targetPosition;
        animator.SetBool("Walking", false);
        movementCoroutine = null;
    }



    private float GetGroundY()
    {
        if (mainCamera != null && mainCamera.orthographic)
        {
            return mainCamera.transform.position.y - mainCamera.orthographicSize;
        }
        return editorGroundY;
    }
    
    private void EnableOcclusionCamera(bool enable)
    {
        if (enable)
        {
            string charLayerName = isSnappedToTaskbar ? "SnappedToTaskbar" : "SnappedCharacter";
            string occLayerName = isSnappedToTaskbar ? "TaskbarOcclusion" : "Occlusion";
            string camName = isSnappedToTaskbar ? "TaskbarOcclusionCam" : "WindowOcclusionCam";
            SetCharacterLayer(charLayerName);
            if (occlusionCamera != null && occlusionCamera.name.StartsWith(camName)) 
            {
                occlusionCamera.gameObject.SetActive(true);
            }
            else
            {
                if (occlusionCamera != null) Destroy(occlusionCamera.gameObject);
                GameObject camObj = new GameObject(camName + " (for " + gameObject.name + ")");
                camObj.transform.SetParent(mainCamera.transform, false);
                occlusionCamera = camObj.AddComponent<Camera>();
                occlusionCamera.CopyFrom(mainCamera);
            }
            int characterLayer = 1 << LayerMask.NameToLayer(charLayerName);
            int occlusionLayer = 1 << LayerMask.NameToLayer(occLayerName);
            occlusionCamera.cullingMask = characterLayer | occlusionLayer;
            occlusionCamera.clearFlags = CameraClearFlags.Nothing;
            occlusionCamera.depth = mainCamera.depth + 1;
            if(occlusionCamera != null) lastKnownCameraSize = occlusionCamera.orthographicSize;
        }
        else
        {
            SetCharacterLayer(LayerMask.LayerToName(defaultLayer));
            if (occlusionCamera != null) { Destroy(occlusionCamera.gameObject); occlusionCamera = null; }
        }
    }

    private void SetCharacterLayer(string layerName)
    {
        int newLayer = LayerMask.NameToLayer(layerName);
        if (newLayer == -1) { Debug.LogError($"'{layerName}' 레이어가 존재하지 않습니다! Project Settings에서 추가해주세요."); return; }
        Stack<Transform> transforms = new Stack<Transform>();
        transforms.Push(this.transform.root);
        while(transforms.Count > 0)
        {
            Transform current = transforms.Pop();
            current.gameObject.layer = newLayer;
            foreach(Transform child in current) { transforms.Push(child); }
        }
    }

    private void FollowSnapTarget()
    {
        if (mySnapTarget == null || targetTransform == null) { StopSnappingOnDrag(); return; }
        if (!isSnappedToTaskbar && mySnapTarget is WindowEntry win && !WindowSnapManager.Instance.IsWindowValidForSnapping(win))
        {
            if (movementCoroutine == null) movementCoroutine = StartCoroutine(FallToGroundRoutine(defaultZ));
            return;
        }
        float snapTargetCenterX = GetSnapTargetCenterX();
        float finalTargetX = snapTargetCenterX + snapXOffsetFromCenter;
        float distanceToCamera = Mathf.Abs(snappedZ - mainCamera.transform.position.z);
        Vector2 minMaxX = GetSnapTargetWorldMinMaxX(distanceToCamera);
        finalTargetX = Mathf.Clamp(finalTargetX, minMaxX.x, minMaxX.y);
        Vector3 finalRootPosition = CalculateFinalRootPosition(snappedZ, finalTargetX);
        transform.root.position = Vector3.Lerp(transform.root.position, finalRootPosition, Time.deltaTime * followMoveSpeed);
    }

    private Vector2 GetSnapTargetWorldMinMaxX(float distance)
    {
        float minScreenX = 0, maxScreenX = 0;
        if (mySnapTarget is WindowEntry win)
        {
            WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out RECT virtualRect);
            if (win.title.StartsWith("작업 표시줄")) { virtualRect = win.fullRect; }
            RECT screenSpaceRect = ToScreenSpace(virtualRect);
            minScreenX = screenSpaceRect.Left;
            maxScreenX = screenSpaceRect.Right;
        }
        else if (mySnapTarget is RectTransform rt)
        {
            rt.GetWorldCorners(uiCorners);
            minScreenX = mainCamera.WorldToScreenPoint(uiCorners[0]).x;
            maxScreenX = mainCamera.WorldToScreenPoint(uiCorners[2]).x;
        }
        float minWorldX = mainCamera.ScreenToWorldPoint(new Vector3(minScreenX, 0, distance)).x;
        float maxWorldX = mainCamera.ScreenToWorldPoint(new Vector3(maxScreenX, 0, distance)).x;
        float padding = 0.1f * transform.root.localScale.x;
        return new Vector2(minWorldX + padding, maxWorldX - padding);
    }

    private float GetSnapTargetCenterX()
    {
        float screenX = 0;
        if (mySnapTarget is WindowEntry win)
        {
            WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out RECT virtualRect);
            if (win.title.StartsWith("작업 표시줄")) { virtualRect = win.fullRect; }
            RECT screenSpaceRect = ToScreenSpace(virtualRect);
            screenX = (screenSpaceRect.Left + screenSpaceRect.Right) / 2.0f;
        }
        else if (mySnapTarget is RectTransform rt)
        {
            rt.GetWorldCorners(uiCorners);
            Vector3 centerWorld = (uiCorners[0] + uiCorners[2]) * 0.5f;
            screenX = mainCamera.WorldToScreenPoint(centerWorld).x;
        }
        float distanceToCamera = Mathf.Abs(snappedZ - mainCamera.transform.position.z);
        return mainCamera.ScreenToWorldPoint(new Vector3(screenX, 0, distanceToCamera)).x;
    }
    
    private Vector3 CalculateFinalRootPosition(float characterZ, float targetHipsX)
    {
        Vector3 snapBasePosition = CalculateSnapPosition(characterZ);
        float presetOffsetY = 0f;
        if (autoActivate != null)
        {
            CharacterPreset preset = autoActivate.GetPreset();
            if (preset != null) presetOffsetY = preset.sittingOffsetY;
        }
        Vector3 finalOffset = snapPositionOffset + new Vector3(0f, presetOffsetY, 0f);
        Vector3 finalHipsPosition = snapBasePosition + (finalOffset * transform.root.localScale.y);
        finalHipsPosition.x = targetHipsX;
        Vector3 rootToHipsWorldVector = targetTransform.position - transform.root.position;
        Vector3 targetRootPosition = finalHipsPosition - rootToHipsWorldVector;
        return targetRootPosition;
    }

    private Vector3 CalculateSnapPosition(float characterZ)
    {
        if (mySnapTarget is RectTransform rt)
        {
            rt.GetWorldCorners(uiCorners);
            Vector3 headerTopCenterWorld = (uiCorners[1] + uiCorners[2]) * 0.5f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(headerTopCenterWorld);
            float distanceToCamera = Mathf.Abs(characterZ - mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceToCamera));
        }
        else if (mySnapTarget is WindowEntry win)
        {
            RECT virtualRect;
            if (win.title.StartsWith("작업 표시줄")) { virtualRect = win.fullRect; }
            else { WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out virtualRect); }
            if(!isSnappedToTaskbar && (virtualRect.Left == 0 && virtualRect.Right == 0))
            {
                if (movementCoroutine == null) movementCoroutine = StartCoroutine(FallToGroundRoutine(defaultZ));
                return Vector3.zero;
            }
            RECT screenSpaceRect = ToScreenSpace(virtualRect);
            float snapY = Screen.height - screenSpaceRect.Top; 
            float screenX = (screenSpaceRect.Left + screenSpaceRect.Right) / 2.0f;
            float distanceToCamera = Mathf.Abs(characterZ - mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(new Vector3(screenX, snapY, distanceToCamera));
        }
        return Vector3.zero;
    }

    private void CheckIfOverTarget()
    {
        bool previousCanSnap = canSnap;
        canSnap = false;
        snapTarget = null;
        if (mainCamera == null || targetTransform == null) return;
        Vector3 targetWorldPos = targetTransform.position;
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);
        if (WindowSnapManager.Instance != null && WindowSnapManager.Instance.uiTargets.Count > 0)
        {
            foreach (var uiTarget in WindowSnapManager.Instance.uiTargets)
            {
                if (uiTarget.header == null || !uiTarget.header.gameObject.activeInHierarchy) continue;
                CanvasGroup cg = uiTarget.header.GetComponentInParent<CanvasGroup>();
                if (cg != null && (!cg.blocksRaycasts || cg.alpha < 0.1f)) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(uiTarget.header, screenPoint, mainCamera))
                {
                    canSnap = true; snapTarget = uiTarget.header; break;
                }
            }
        }
        if (!canSnap && WindowSnapManager.Instance != null)
        {
            Vector2 desktopPos = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            desktopPos = new Vector2(screenPoint.x + FullScreenAuto.VirtualScreenX, (Screen.height - screenPoint.y) + FullScreenAuto.VirtualScreenY);
#endif
            foreach (var taskbar in WindowSnapManager.Instance.TaskbarEntries)
            {
                if (IsPointInsideWindow(desktopPos, taskbar.headerRect)) { canSnap = true; snapTarget = taskbar; break; }
            }
            if (!canSnap)
            {
                foreach (var win in WindowSnapManager.Instance.CurrentWindows)
                {
                    if (IsPointInsideWindow(desktopPos, win.headerRect) && WindowSnapManager.Instance.IsWindowValidForSnapping(win))
                    {
                        canSnap = true; snapTarget = win; break;
                    }
                }
            }
        }
        if (previousCanSnap != canSnap)
        {
            ShowSnapIndicator(canSnap);
        }
    }
    
    private bool IsPointInsideWindow(Vector2 point, RECT rect)
    {
        return point.x >= rect.Left && point.x <= rect.Right && point.y >= rect.Top && point.y <= rect.Bottom;
    }
    #endregion
}