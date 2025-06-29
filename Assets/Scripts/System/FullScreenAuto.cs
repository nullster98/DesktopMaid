// #if UNITY_STANDALONE_WIN // Windows 전용 기능이므로 유지
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class FullScreenAuto : MonoBehaviour
{
    private static Mutex mutex;

    // --- Win32 API Constants ---
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;

    const int WS_BORDER = 0x00800000;
    const int WS_DLGFRAME = 0x00400000;
    const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
    const int WS_SYSMENU = 0x00080000;
    const int WS_MINIMIZEBOX = 0x00020000;
    const int WS_MAXIMIZEBOX = 0x00010000;
    const int WS_THICKFRAME = 0x00040000;

    const int WS_EX_TOOLWINDOW = 0x00000080; // 작업 표시줄에서 숨김
    const int WS_EX_APPWINDOW = 0x00040000;  // 일반 앱 창 스타일 (제거 대상)

    const uint SWP_SHOWWINDOW = 0x0040;
    const uint SWP_FRAMECHANGED = 0x0020; // 프레임 변경 알림
    static readonly IntPtr HWND_TOP = IntPtr.Zero;

    // --- 추가된 부분: 가상 화면 크기를 얻기 위한 상수 ---
    const int SM_XVIRTUALSCREEN = 76;
    const int SM_YVIRTUALSCREEN = 77;
    const int SM_CXVIRTUALSCREEN = 78;
    const int SM_CYVIRTUALSCREEN = 79;

    // --- Win32 API DllImports ---
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- 추가된 부분: 가상 화면 크기를 얻기 위한 DllImport ---
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);


    public string commandFileName = "command.txt"; // CommanderReceiver와 동일한 파일명 사용
    
    // --- 추가된 부분: 가상 화면 정보를 저장할 변수 ---
    public static int VirtualScreenX { get; private set; }
    public static int VirtualScreenY { get; private set; }
    private int virtualScreenWidth;
    private int virtualScreenHeight;


    void Awake()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        bool isNewInstance;
        mutex = new Mutex(true, UnityEngine.Application.productName, out isNewInstance);

        if (!isNewInstance)
        {
            UnityEngine.Debug.LogWarning($"[FullScreenAuto] ⚠ 이미 실행 중인 '{UnityEngine.Application.productName}' 인스턴스가 있습니다. 새 인스턴스를 종료합니다.");
            UnityEngine.Application.Quit();
            return;
        }
        UnityEngine.Debug.Log($"[FullScreenAuto] '{UnityEngine.Application.productName}' 새 인스턴스 시작됨.");
#endif
    }

    void Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        ClearCommandFileOnStart();

        // 1. 가상 화면의 크기와 위치를 가져와서 static 프로퍼티에 할당
        VirtualScreenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        VirtualScreenY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        virtualScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        virtualScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        
        UnityEngine.Debug.Log($"[FullScreenAuto] 🖥️ 가상 화면 감지됨: Pos({VirtualScreenX},{VirtualScreenY}) Size({virtualScreenWidth}x{virtualScreenHeight})");

        // 2. 창을 가상 화면 전체 크기로 설정
        Screen.SetResolution(virtualScreenWidth, virtualScreenHeight, FullScreenMode.Windowed);
        UnityEngine.Debug.Log($"[FullScreenAuto] ✅ 창모드 설정됨: {virtualScreenWidth}x{virtualScreenHeight} (테두리 제거 및 위치 조정 예정)");

        StartCoroutine(DelayedApplyWindowChanges());
#else
        UnityEngine.Debug.LogWarning("[FullScreenAuto] 에디터 또는 지원되지 않는 플랫폼에서는 창 스타일 변경을 건너뜁니다.");
#endif
    }

    private void ClearCommandFileOnStart()
    {
        string exeDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        string path = Path.Combine(exeDir, commandFileName);
        if (File.Exists(path))
        {
            try
            {
                File.WriteAllText(path, "");
                UnityEngine.Debug.Log($"[FullScreenAuto] 🧹 '{commandFileName}' 초기화 완료 (내용 비움)");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[FullScreenAuto] '{commandFileName}' 초기화 실패: {e.Message}");
            }
        }
    }

    private IEnumerator DelayedApplyWindowChanges()
    {
        yield return null; // 1 프레임 대기 (창 핸들 안정화)
        ApplyBorderlessAndHideFromTaskbar();
    }

    private void ApplyBorderlessAndHideFromTaskbar()
    {
        IntPtr hwnd = FindWindow(null, UnityEngine.Application.productName);
        if (hwnd == IntPtr.Zero) {
            hwnd = FindWindow("UnityWndClass", UnityEngine.Application.productName);
        }

        if (hwnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogError("[FullScreenAuto] ❌ 윈도우 핸들을 찾지 못했습니다. 작업 표시줄 숨김 및 테두리 제거 실패.");
            return;
        }
        UnityEngine.Debug.Log($"[FullScreenAuto] 창 핸들: {hwnd} (스타일 변경 시도)");

        // 테두리 제거
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_THICKFRAME);
        SetWindowLong(hwnd, GWL_STYLE, style);

        // 작업 표시줄 아이콘 숨기기
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        UnityEngine.Debug.Log($"[FullScreenAuto] 스타일 변경 후 GWL_STYLE: 0x{GetWindowLong(hwnd, GWL_STYLE):X}, GWL_EXSTYLE: 0x{GetWindowLong(hwnd, GWL_EXSTYLE):X}");

        // --- 수정된 부분: 창 위치와 크기를 가상 화면에 맞게 설정 ---
        bool success = SetWindowPos(hwnd, HWND_TOP, VirtualScreenX, VirtualScreenY, virtualScreenWidth, virtualScreenHeight, SWP_SHOWWINDOW | SWP_FRAMECHANGED);

        if (!success)
        {
            UnityEngine.Debug.LogError($"[FullScreenAuto] SetWindowPos 실패: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            UnityEngine.Debug.Log("[FullScreenAuto] ✅ SetWindowPos 성공. 창이 모든 모니터를 덮도록 재배치되었습니다.");
        }
    }

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        mutex?.Close();
#endif
    }
}
// #endif