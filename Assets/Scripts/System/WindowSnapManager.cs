// --- START OF FILE WindowSnapManager.cs ---
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class WindowSnapManager : MonoBehaviour
{
    #region Singleton
    public static WindowSnapManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Public Struct for UI Targets
    [System.Serializable]
    public struct UITarget
    {
        public string name;
        public RectTransform header;
        public RectTransform mainPanel;
    }
    #endregion

    #region Public Properties & Fields
    [Header("설정")]
    [Tooltip("얼마나 자주 창 목록을 갱신할지(초)")]
    public float updateInterval = 1.0f;
    [Tooltip("외부 창 헤더로 인식할 영역의 높이(픽셀)입니다. 클수록 감지가 쉬워집니다.")]
    public int headerDetectionHeight = 80;
    [Tooltip("카메라로부터 얼마나 앞에 투명 벽(Occlusion Quad)을 생성할지 결정합니다. 이 값은 항상 고정됩니다.")]
    public float occlusionQuadZOffset = 3.2f;


    [Header("동작 모드 설정")]
    [Tooltip("True: 스냅되지 않은 위치에 캐릭터를 놓으면 땅으로 떨어집니다.\nFalse: 드래그 시작 전의 원래 Z 위치로 돌아갑니다.")]
    public bool useAdvancedFallingBehavior = false;
    
    [Header("UI 스냅 타겟")]
    public List<UITarget> uiTargets = new List<UITarget>();

    public List<WindowEntry> CurrentWindows { get; private set; } = new List<WindowEntry>();
    public WindowEntry TaskbarEntry { get; private set; }
    
    public object CurrentTrackedTarget { get; private set; }
    #endregion

    #region Private Fields
    private float timer;
    private uint currentProcessId;
    private GameObject currentOcclusionQuad;
    private GameObject taskbarOcclusionQuad;
    private object trackedTarget; 
    private Camera mainCamera;
    #endregion

    #region Windows API Imports
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public class WindowEntry { public IntPtr hWnd; public RECT fullRect; public RECT headerRect; public string title; }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA { public uint cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public int lParam; }
    private const int ABM_GETTASKBARPOS = 5;

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        mainCamera = Camera.main;
        RefreshWindowList();
        CreateTaskbarOcclusionQuad();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            RefreshWindowList();
            UpdateTaskbarOcclusionPosition();
        }

        // [수정] Update에서 trackedTarget을 계속 추적하여 Quad 위치를 갱신하도록 보장
        if (currentOcclusionQuad != null && trackedTarget != null)
        {
            UpdateOcclusionQuadPosition(currentOcclusionQuad, trackedTarget);
        }
    }
    #endregion

    #region Public Methods
    public bool GetWindowRectByHandle(IntPtr hWnd, out RECT rect)
    {
        int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT)));
        if (result >= 0)
        {
            return true;
        }
        return GetWindowRect(hWnd, out rect);
    }
    
    public bool IsWindowValidForSnapping(WindowEntry window)
    {
        if (window == null) return false;
        if (window.title == "작업 표시줄") return true;
        if (window.hWnd == IntPtr.Zero) return false;
        GetWindowRectByHandle(window.hWnd, out RECT rect);
        if (rect.Left == 0 && rect.Right == 0) return false;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        if (width >= screenWidth - 20 && height >= screenHeight - 20) return false;
        return true;
    }
    
    public void ShowOcclusionMaskFor(object target)
    {
        if (target is WindowEntry we && we.title == "작업 표시줄")
        {
            // 작업 표시줄은 별도의 Quad를 사용하므로 여기서는 아무것도 하지 않음
            return;
        }

        if (currentOcclusionQuad == null)
        {
            currentOcclusionQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            currentOcclusionQuad.name = "OcclusionQuad_Window";
            Destroy(currentOcclusionQuad.GetComponent<Collider>());
            currentOcclusionQuad.layer = LayerMask.NameToLayer("Occlusion");
            Material occlusionMat = Resources.Load<Material>("M_OcclusionMask");
            if (occlusionMat != null) { currentOcclusionQuad.GetComponent<Renderer>().material = occlusionMat; }
            else { Debug.LogError("Resources 폴더에서 'M_OcclusionMask' 재질을 찾을 수 없습니다!"); }
        }
        
        trackedTarget = target;
        CurrentTrackedTarget = target;

        // Quad의 Z위치는 한 번 고정
        float fixedZ = mainCamera.transform.position.z + occlusionQuadZOffset;
        currentOcclusionQuad.transform.position = new Vector3(0, 0, fixedZ);
        
        // 첫 위치 업데이트
        UpdateOcclusionQuadPosition(currentOcclusionQuad, trackedTarget);
    }

    public void HideOcclusionMask()
    {
        if (currentOcclusionQuad != null) { Destroy(currentOcclusionQuad); }
        currentOcclusionQuad = null; 
        trackedTarget = null;
        CurrentTrackedTarget = null;
    }

    public RectTransform GetMainPanelForHeader(RectTransform header)
    {
        foreach (var target in uiTargets)
        {
            if (target.header == header)
            {
                return target.mainPanel;
            }
        }
        return null;
    }
    #endregion

    #region Core Logic
    
    private void CreateTaskbarOcclusionQuad()
    {
        if (taskbarOcclusionQuad != null) return;
        
        taskbarOcclusionQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        taskbarOcclusionQuad.name = "OcclusionQuad_Taskbar";
        Destroy(taskbarOcclusionQuad.GetComponent<Collider>());
        
        int layer = LayerMask.NameToLayer("TaskbarOcclusion");
        if (layer == -1) { Debug.LogError("'TaskbarOcclusion' 레이어를 찾을 수 없습니다! Project Settings에서 추가해주세요."); return; }
        taskbarOcclusionQuad.layer = layer;

        Material occlusionMat = Resources.Load<Material>("M_OcclusionMask");
        if (occlusionMat != null) { taskbarOcclusionQuad.GetComponent<Renderer>().material = occlusionMat; }
        else { Debug.LogError("Resources 폴더에서 'M_OcclusionMask' 재질을 찾을 수 없습니다!"); }

        UpdateTaskbarOcclusionPosition();
    }
    
    private void UpdateTaskbarOcclusionPosition()
    {
        if (taskbarOcclusionQuad == null || TaskbarEntry == null || mainCamera == null) return;
        
        float distance = occlusionQuadZOffset;
        
        Vector3 worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(TaskbarEntry.fullRect.Left, Screen.height - TaskbarEntry.fullRect.Bottom, distance));
        Vector3 worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(TaskbarEntry.fullRect.Right, Screen.height - TaskbarEntry.fullRect.Top, distance));

        taskbarOcclusionQuad.transform.position = (worldBottomLeft + worldTopRight) / 2;
        taskbarOcclusionQuad.transform.localScale = new Vector3(worldTopRight.x - worldBottomLeft.x, worldTopRight.y - worldBottomLeft.y, 1f);
    }
    
    // --- [수정된 메서드] ---
    private void UpdateOcclusionQuadPosition(GameObject quad, object target)
    {
        if (quad == null || target == null || mainCamera == null)
        {
            // 유효하지 않은 타겟이면 마스크 숨기기 시도
            if (target == null) HideOcclusionMask();
            return;
        }

        // Quad와 카메라 사이의 거리는 고정값 사용
        float distance = Mathf.Abs(quad.transform.position.z - mainCamera.transform.position.z);
        
        // 월드 좌표를 담을 변수를 if문 바깥에 선언
        Vector3 worldBottomLeft, worldTopRight;

        if (target is WindowEntry win)
        {
            GetWindowRectByHandle(win.hWnd, out RECT winRect);
            // 창이 사라졌거나 유효하지 않은 경우 (최소화 등) 마스크를 숨김
            if (winRect.Left == 0 && winRect.Right == 0 && winRect.Top == 0 && winRect.Bottom == 0)
            {
                HideOcclusionMask();
                return;
            }
            
            // 스크린 좌표를 월드 좌표로 변환
            worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(winRect.Left, Screen.height - winRect.Bottom, distance));
            worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(winRect.Right, Screen.height - winRect.Top, distance));
        }
        else if(target is RectTransform rt)
        {
            // RectTransform이 비활성화되었거나 유효하지 않은 경우 마스크를 숨김
            if (rt == null || !rt.gameObject.activeInHierarchy)
            {
                HideOcclusionMask();
                return;
            }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners); // corners[0] = BottomLeft, corners[2] = TopRight
            
            // 월드 좌표를 스크린 좌표로 변환
            Vector2 screenBottomLeft = mainCamera.WorldToScreenPoint(corners[0]);
            Vector2 screenTopRight = mainCamera.WorldToScreenPoint(corners[2]);
            
            // 변환된 스크린 좌표를 다시 Quad의 Z 깊이에 맞는 월드 좌표로 변환
            // 이 과정을 통해 UI가 3D 공간에 있더라도 올바르게 2D Quad로 투영됨
            worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(screenBottomLeft.x, screenBottomLeft.y, distance));
            worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(screenTopRight.x, screenTopRight.y, distance));
        }
        else
        {
            // 알 수 없는 타입의 타겟이면 마스크를 숨김
            HideOcclusionMask();
            return;
        }

        // 계산된 월드 좌표를 바탕으로 Quad의 위치와 크기를 최종 설정
        quad.transform.position = (worldBottomLeft + worldTopRight) / 2;
        quad.transform.localScale = new Vector3(Mathf.Abs(worldTopRight.x - worldBottomLeft.x), Mathf.Abs(worldTopRight.y - worldBottomLeft.y), 1f);
    }
    
    private void RefreshWindowList()
    {
        UpdateTaskbarEntry();
        CurrentWindows.Clear();
        EnumWindows(EnumWindowsCallback, IntPtr.Zero);
    }
    
    private void UpdateTaskbarEntry()
    {
        APPBARDATA data = new APPBARDATA();
        data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) != IntPtr.Zero)
        {
            TaskbarEntry = new WindowEntry
            {
                hWnd = new IntPtr(-1),
                fullRect = data.rc,
                headerRect = data.rc,
                title = "작업 표시줄"
            };
        }
    }

    private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
    {
        if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0) return true;

        GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == currentProcessId) return true;

        int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf(typeof(RECT)));
        if (result < 0) 
        {
            return true;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width < 100 || height < 100) return true;

        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        if (width >= screenWidth - 20 && height >= screenHeight - 20) return true;

        RECT headerRect = new RECT { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Top + headerDetectionHeight };
        StringBuilder title = new StringBuilder(GetWindowTextLength(hWnd) + 1);
        GetWindowText(hWnd, title, title.Capacity);
        CurrentWindows.Add(new WindowEntry { hWnd = hWnd, fullRect = rect, headerRect = headerRect, title = title.ToString() });
        return true;
    }
    #endregion
}