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
    public List<WindowEntry> TaskbarEntries { get; private set; } = new List<WindowEntry>();
    
    public object CurrentTrackedTarget { get; private set; }
    #endregion

    #region Private Fields
    private float timer;
    private uint currentProcessId;
    private GameObject currentOcclusionQuad;
    private List<GameObject> taskbarOcclusionQuads = new List<GameObject>();
    private object trackedTarget; 
    private Camera mainCamera;
    #endregion

    #region Windows API Imports
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public override bool Equals(object obj) => obj is RECT other && Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom; public override int GetHashCode() => (Left, Top, Right, Bottom).GetHashCode(); }

    public class WindowEntry { public IntPtr hWnd; public RECT fullRect; public RECT headerRect; public string title; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, [MarshalAs(UnmanagedType.Bool)] out bool pvAttribute, int cbAttribute);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        mainCamera = Camera.main;
        RefreshWindowList();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            RefreshWindowList();
        }

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
        if (window.title.StartsWith("작업 표시줄")) return true;
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
        if (target is WindowEntry we && we.title.StartsWith("작업 표시줄"))
        {
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

        float fixedZ = mainCamera.transform.position.z + occlusionQuadZOffset;
        currentOcclusionQuad.transform.position = new Vector3(0, 0, fixedZ);
        
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
    
    private void UpdateTaskbarOcclusionQuads()
    {
        while (taskbarOcclusionQuads.Count < TaskbarEntries.Count)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"OcclusionQuad_Taskbar_{taskbarOcclusionQuads.Count}";
            Destroy(quad.GetComponent<Collider>());
            int layer = LayerMask.NameToLayer("TaskbarOcclusion");
            if (layer == -1) { Debug.LogError("'TaskbarOcclusion' 레이어를 찾을 수 없습니다!"); return; }
            quad.layer = layer;
            Material occlusionMat = Resources.Load<Material>("M_OcclusionMask");
            if (occlusionMat != null) { quad.GetComponent<Renderer>().material = occlusionMat; }
            taskbarOcclusionQuads.Add(quad);
        }
        while (taskbarOcclusionQuads.Count > TaskbarEntries.Count)
        {
            Destroy(taskbarOcclusionQuads[taskbarOcclusionQuads.Count - 1]);
            taskbarOcclusionQuads.RemoveAt(taskbarOcclusionQuads.Count - 1);
        }

        for (int i = 0; i < TaskbarEntries.Count; i++)
        {
            UpdateSingleOcclusionQuadPosition(taskbarOcclusionQuads[i], TaskbarEntries[i]);
        }
    }
    
    private void UpdateSingleOcclusionQuadPosition(GameObject quad, WindowEntry entry)
    {
        if (quad == null || entry == null || mainCamera == null) return;

        float distance = occlusionQuadZOffset;
        RECT screenSpaceRect = ToScreenSpace(entry.fullRect);

        Vector3 worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(screenSpaceRect.Left, Screen.height - screenSpaceRect.Bottom, distance));
        Vector3 worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(screenSpaceRect.Right, Screen.height - screenSpaceRect.Top, distance));

        quad.transform.position = (worldBottomLeft + worldTopRight) / 2;
        quad.transform.localScale = new Vector3(worldTopRight.x - worldBottomLeft.x, worldTopRight.y - worldBottomLeft.y, 1f);
    }
    
    private void UpdateOcclusionQuadPosition(GameObject quad, object target)
    {
        if (quad == null || target == null || mainCamera == null)
        {
            if (target == null) HideOcclusionMask();
            return;
        }

        float distance = Mathf.Abs(quad.transform.position.z - mainCamera.transform.position.z);
        
        Vector3 worldBottomLeft, worldTopRight;

        if (target is WindowEntry win)
        {
            GetWindowRectByHandle(win.hWnd, out RECT winRect);
            if (winRect.Left == 0 && winRect.Right == 0 && winRect.Top == 0 && winRect.Bottom == 0)
            {
                HideOcclusionMask();
                return;
            }
            
            RECT screenSpaceRect = ToScreenSpace(winRect);
            worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(screenSpaceRect.Left, Screen.height - screenSpaceRect.Bottom, distance));
            worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(screenSpaceRect.Right, Screen.height - screenSpaceRect.Top, distance));
        }
        else if(target is RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy)
            {
                HideOcclusionMask();
                return;
            }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 screenBottomLeft = mainCamera.WorldToScreenPoint(corners[0]);
            Vector2 screenTopRight = mainCamera.WorldToScreenPoint(corners[2]);
            worldBottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(screenBottomLeft.x, screenBottomLeft.y, distance));
            worldTopRight = mainCamera.ScreenToWorldPoint(new Vector3(screenTopRight.x, screenTopRight.y, distance));
        }
        else
        {
            HideOcclusionMask();
            return;
        }

        quad.transform.position = (worldBottomLeft + worldTopRight) / 2;
        quad.transform.localScale = new Vector3(Mathf.Abs(worldTopRight.x - worldBottomLeft.x), Mathf.Abs(worldTopRight.y - worldBottomLeft.y), 1f);
    }
    
    private void RefreshWindowList()
    {
        UpdateTaskbarEntries();
        UpdateTaskbarOcclusionQuads();
        CurrentWindows.Clear();
        EnumWindows(EnumWindowsCallback, IntPtr.Zero);
    }
    
    private void UpdateTaskbarEntries()
    {
        TaskbarEntries.Clear();
        int monitorIndex = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    RECT monitorRect = mi.rcMonitor;
                    RECT workAreaRect = mi.rcWork;

                    if (!monitorRect.Equals(workAreaRect))
                    {
                        RECT taskbarRect = new RECT();
                        if (monitorRect.Left < workAreaRect.Left) taskbarRect = new RECT { Left = monitorRect.Left, Top = monitorRect.Top, Right = workAreaRect.Left, Bottom = monitorRect.Bottom };
                        else if (monitorRect.Right > workAreaRect.Right) taskbarRect = new RECT { Left = workAreaRect.Right, Top = monitorRect.Top, Right = monitorRect.Right, Bottom = monitorRect.Bottom };
                        else if (monitorRect.Top < workAreaRect.Top) taskbarRect = new RECT { Left = monitorRect.Left, Top = monitorRect.Top, Right = monitorRect.Right, Bottom = workAreaRect.Top };
                        else if (monitorRect.Bottom > workAreaRect.Bottom) taskbarRect = new RECT { Left = monitorRect.Left, Top = workAreaRect.Bottom, Right = monitorRect.Right, Bottom = monitorRect.Bottom };
                        
                        TaskbarEntries.Add(new WindowEntry
                        {
                            hWnd = new IntPtr(-2 - monitorIndex),
                            fullRect = taskbarRect,
                            headerRect = taskbarRect,
                            title = $"작업 표시줄 {monitorIndex}"
                        });
                        monitorIndex++;
                    }
                }
                return true;
            }, IntPtr.Zero);
    }

    private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
    {
        if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0) return true;

        int result = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, Marshal.SizeOf(typeof(bool)));
        if (result == 0 && isCloaked)
        {
            return true;
        }

        GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == currentProcessId) return true;

        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf(typeof(RECT))) < 0)
        {
            if (!GetWindowRect(hWnd, out rect)) return true;
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