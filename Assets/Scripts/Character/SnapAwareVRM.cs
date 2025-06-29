// --- START OF FILE SnapAwareVRM.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static WindowSnapManager;

public class SnapAwareVRM : MonoBehaviour
{
    #region Windows API Imports
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT Point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    private const uint GA_ROOT = 2;
    #endregion

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

    [Header("감지 시각화 설정")]
    [Tooltip("감지 영역에 들어갔을 때 덮어씌울 색상")]
    public Color detectionTintColor = new Color(1f, 0.75f, 0.8f, 1f);
    [Tooltip("색상 변경에 사용될 셰이더 프로퍼티 이름")]
    public string colorPropertyName = "_Color";
    
    [Header("땅으로 떨어지기 설정")]
    [Tooltip("땅으로 떨어지는 속도입니다.")]
    public float fallSpeed = 5f;
    [Tooltip("에디터 환경에서 사용할 땅의 Y 좌표입니다. 이제 동적으로 계산되므로 비상용입니다.")]
    public float editorGroundY = -4f;
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

    private List<Renderer> characterRenderers = new List<Renderer>();
    private List<Color> originalColors = new List<Color>();
    private bool isTinted = false;

    private float snapXOffsetFromCenter;
    private Camera occlusionCamera;

    private float lastKnownCameraSize;

    private int defaultLayer;
    private bool isSnappedToTaskbar = false;
    private Coroutine movementCoroutine;

    private float defaultZ;
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

        GetComponentsInChildren<Renderer>(true, characterRenderers);
        foreach (var rend in characterRenderers)
        {
            if (rend.material != null && rend.material.HasProperty(colorPropertyName))
            {
                originalColors.Add(rend.material.GetColor(colorPropertyName));
            }
            else
            {
                originalColors.Add(Color.white);
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
                FollowSnapTarget();
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
                SetDetectionTint(false);
            }
        }
    }
    #endregion

    #region Public Methods
    public void OnDragEnd(float characterZ)
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

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

        isSnappedToTaskbar = (snapTarget is WindowEntry we && we.title == "작업 표시줄");

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
            
        SetDetectionTint(false);
        isSnapped = true; 
        animator.SetBool("isWindowSit", true);
        animator.SetBool("Walking", false);
            
        if (autoActivate != null) { autoActivate.SetSnappedState(true); }
    }

    public void StopSnappingOnDrag()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
        
        if (isSnapped)
        {
            isSnapped = false; 
            canSnap = false;
            
            EnableOcclusionCamera(false);
            
            mySnapTarget = null;
            isSnappedToTaskbar = false;
            animator.SetBool("isWindowSit", false);
            SetDetectionTint(false);

            if(autoActivate != null) autoActivate.SetSnappedState(false);
        }
    }
    #endregion

    #region Core Logic
    
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
        Debug.Log($"[{gameObject.name}] 원래 Z 위치({defaultZ})로 복귀 시작.");
        
        animator.SetBool("Walking", true); 

        Vector3 currentPos = transform.root.position;
        Vector3 targetPosition = new Vector3(currentPos.x, currentPos.y, defaultZ);

        while (Vector3.Distance(transform.root.position, targetPosition) > 0.01f)
        {
            transform.root.position = Vector3.MoveTowards(transform.root.position, targetPosition, fallSpeed * Time.deltaTime);
            yield return null;
        }

        transform.root.position = targetPosition;
        animator.SetBool("Walking", false);

        Debug.Log($"[{gameObject.name}] 원래 위치로 복귀 완료.");
        movementCoroutine = null;
    }

    private IEnumerator FallToGroundRoutine(float? targetZ = null)
    {
        Debug.Log($"[{gameObject.name}] 땅으로 떨어지기 시작.");

        isSnapped = false;
        canSnap = false;
        EnableOcclusionCamera(false);
        mySnapTarget = null;
        isSnappedToTaskbar = false;
        SetDetectionTint(false);
        if (autoActivate != null) autoActivate.SetSnappedState(false);
        
        animator.SetBool("isWindowSit", false);
        animator.SetBool("Walking", true);

        float groundWorldY = GetGroundY();
        float finalZ = targetZ ?? transform.root.position.z;
        Vector3 targetPosition = new Vector3(transform.root.position.x, groundWorldY, finalZ);
        
        while (Vector3.Distance(transform.root.position, targetPosition) > 0.01f)
        {
            transform.root.position = Vector3.MoveTowards(transform.root.position, targetPosition, fallSpeed * Time.deltaTime);
            yield return null;
        }
        
        transform.root.position = targetPosition;
        animator.SetBool("Walking", false);
        
        Debug.Log($"[{gameObject.name}] 땅에 도착.");
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

            if (characterLayer == 0) Debug.LogError($"'{charLayerName}' 레이어를 찾을 수 없습니다!");
            if (occlusionLayer == 0) Debug.LogError($"'{occLayerName}' 레이어를 찾을 수 없습니다!");

            occlusionCamera.cullingMask = characterLayer | occlusionLayer;
            occlusionCamera.clearFlags = CameraClearFlags.Nothing;
            occlusionCamera.depth = mainCamera.depth + 1;
            
            if(occlusionCamera != null)
            {
                lastKnownCameraSize = occlusionCamera.orthographicSize;
            }
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
    
    private void SetDetectionTint(bool active)
    {
        if (isTinted == active) return;
        isTinted = active;
        for (int i = 0; i < characterRenderers.Count; i++)
        {
            var rend = characterRenderers[i];
            if (rend != null && rend.material != null && rend.material.HasProperty(colorPropertyName))
            {
                rend.material.SetColor(colorPropertyName, active ? originalColors[i] * detectionTintColor : originalColors[i]);
            }
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
            RECT currentRect;
            if (win.title == "작업 표시줄") { currentRect = WindowSnapManager.Instance.TaskbarEntry.fullRect; }
            else { WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out currentRect); }
            minScreenX = currentRect.Left;
            maxScreenX = currentRect.Right;
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
            RECT currentRect;
            if (win.title == "작업 표시줄") { currentRect = WindowSnapManager.Instance.TaskbarEntry.fullRect; }
            else { WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out currentRect); }
            screenX = (currentRect.Left + currentRect.Right) / 2.0f;
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
            if (preset != null)
            {
                presetOffsetY = preset.sittingOffsetY;
            }
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
        if (mySnapTarget is RectTransform)
        {
            RectTransform headerRt = null;
            if(snapTarget is RectTransform rt) headerRt = rt;

            if (headerRt == null)
            {
                headerRt = mySnapTarget as RectTransform;
                if (headerRt == null) { if (movementCoroutine == null) movementCoroutine = StartCoroutine(FallToGroundRoutine(defaultZ)); return Vector3.zero; }
            }

            headerRt.GetWorldCorners(uiCorners);
            Vector3 headerTopCenterWorld = (uiCorners[1] + uiCorners[2]) * 0.5f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(headerTopCenterWorld);
            float distanceToCamera = Mathf.Abs(characterZ - mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceToCamera));
        }
        else if (mySnapTarget is WindowEntry win)
        {
            RECT currentRect;
            if (win.title == "작업 표시줄") { currentRect = WindowSnapManager.Instance.TaskbarEntry.fullRect; }
            else { WindowSnapManager.Instance.GetWindowRectByHandle(win.hWnd, out currentRect); }
            if(!isSnappedToTaskbar && (currentRect.Left == 0 && currentRect.Right == 0))
            {
                if (movementCoroutine == null) movementCoroutine = StartCoroutine(FallToGroundRoutine(defaultZ));
                return Vector3.zero;
            }
            float snapY = Screen.height - currentRect.Top; 
            float screenX = (currentRect.Left + currentRect.Right) / 2.0f;
            float distanceToCamera = Mathf.Abs(characterZ - mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(new Vector3(screenX, snapY, distanceToCamera));
        }
        return Vector3.zero;
    }

    // --- [수정된 메서드] ---
    private void CheckIfOverTarget()
    {
        bool previousCanSnap = canSnap;
        canSnap = false;
        snapTarget = null;
        if (mainCamera == null || targetTransform == null) return;

        // 감지 기준을 '캐릭터의 Hips' 위치로 통일
        Vector3 targetWorldPos = targetTransform.position;
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);

        // 1. UI(Canvas) 타겟 감지
        if (WindowSnapManager.Instance != null && WindowSnapManager.Instance.uiTargets.Count > 0)
        {
            foreach (var uiTarget in WindowSnapManager.Instance.uiTargets)
            {
                if (uiTarget.header == null || !uiTarget.header.gameObject.activeInHierarchy) continue;
                CanvasGroup cg = uiTarget.header.GetComponentInParent<CanvasGroup>();
                if (cg != null && (!cg.blocksRaycasts || cg.alpha < 0.1f)) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(uiTarget.header, screenPoint, mainCamera))
                {
                    canSnap = true;
                    snapTarget = uiTarget.header;
                    break;
                }
            }
        }

        // 2. 외부 창 및 작업 표시줄 감지 (UI 타겟을 못 찾았을 경우에만 실행)
        if (!canSnap && WindowSnapManager.Instance != null)
        {
            Vector2 desktopPos = new Vector2(screenPoint.x, Screen.height - screenPoint.y);

            WindowEntry taskbar = WindowSnapManager.Instance.TaskbarEntry;
            if (taskbar != null && IsPointInsideWindow(desktopPos, taskbar.headerRect))
            {
                canSnap = true;
                snapTarget = taskbar;
            }
            else
            {
                foreach (var win in WindowSnapManager.Instance.CurrentWindows)
                {
                    if (IsPointInsideWindow(desktopPos, win.headerRect))
                    {
                        if (WindowSnapManager.Instance.IsWindowValidForSnapping(win))
                        {
                            canSnap = true;
                            snapTarget = win;
                            break;
                        }
                    }
                }
            }
        }

        // 3. 최종 감지 결과에 따라 시각적 피드백 처리 (메서드 마지막으로 이동)
        if (previousCanSnap != canSnap)
        {
            SetDetectionTint(canSnap);
            if (canSnap)
            {
                string targetName = (snapTarget is WindowEntry we) ? we.title : (snapTarget as RectTransform)?.name ?? "Unknown";
                Debug.Log($"[SnapAware] 감지 영역 진입: {targetName}");
            }
            else
            {
                Debug.Log("[SnapAware] 감지 영역에서 벗어남");
            }
        }
    }
    
    // --- [새로 추가된 클래스 메서드] ---
    private bool IsPointInsideWindow(Vector2 point, RECT rect)
    {
        return point.x >= rect.Left && point.x <= rect.Right && point.y >= rect.Top && point.y <= rect.Bottom;
    }
    #endregion
}