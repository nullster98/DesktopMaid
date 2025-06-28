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

    // 원하는 해상도 (필요시 프로젝트 설정 또는 다른 방식으로 관리)
    private const int TargetWidth = 2560;
    private const int TargetHeight = 1440;

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

    // --- Win32 API DllImports ---
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public string commandFileName = "command.txt"; // CommanderReceiver와 동일한 파일명 사용

    void Awake() // Start보다 먼저 실행되도록 Awake 사용 가능
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN // 에디터가 아니고 Windows 빌드일 때만 실행
        bool isNewInstance;
        // Product Name은 Unity Player Settings와 일치해야 함
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

        // 해상도 설정 (프로젝트 요구사항에 맞게 조절)
        // FullScreenMode.Windowed로 해야 SetWindowLong으로 스타일 변경이 용이함
        // FullScreenMode.FullScreenWindow는 종종 창 스타일 변경을 무시할 수 있음
        Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.Windowed);
        UnityEngine.Debug.Log($"[FullScreenAuto] ✅ 창모드 설정됨: {TargetWidth}x{TargetHeight} (테두리 제거 및 작업표시줄 숨김 적용 예정)");

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
                File.WriteAllText(path, ""); // 파일 내용을 비움
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
        IntPtr hwnd = IntPtr.Zero;
        // 먼저 Product Name으로 창 찾기
        hwnd = FindWindow(null, UnityEngine.Application.productName);
        
        // 못 찾으면 Unity 기본 클래스 이름으로 시도 (가끔 Product Name이 바로 적용 안될 때)
        if (hwnd == IntPtr.Zero) {
            hwnd = FindWindow("UnityWndClass", UnityEngine.Application.productName);
        }

        if (hwnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogError("[FullScreenAuto] ❌ 윈도우 핸들을 찾지 못했습니다. 작업 표시줄 숨김 및 테두리 제거 실패.");
            return;
        }
        UnityEngine.Debug.Log($"[FullScreenAuto] 창 핸들: {hwnd} (스타일 변경 시도)");

        // 테두리 제거 (선택 사항)
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_THICKFRAME);
        SetWindowLong(hwnd, GWL_STYLE, style);

        // 작업 표시줄 아이콘 숨기기
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;  // 도구 창 스타일 추가
        exStyle &= ~WS_EX_APPWINDOW; // 일반 애플리케이션 창 스타일 제거
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        UnityEngine.Debug.Log($"[FullScreenAuto] 스타일 변경 후 GWL_STYLE: 0x{GetWindowLong(hwnd, GWL_STYLE):X}, GWL_EXSTYLE: 0x{GetWindowLong(hwnd, GWL_EXSTYLE):X}");

        // 변경 사항 적용 및 창 위치/크기 설정 (현재 해상도 사용)
        bool success = SetWindowPos(hwnd, HWND_TOP, 0, 0, Screen.width, Screen.height, SWP_SHOWWINDOW | SWP_FRAMECHANGED);

        if (!success)
        {
            UnityEngine.Debug.LogError($"[FullScreenAuto] SetWindowPos 실패: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            UnityEngine.Debug.Log("[FullScreenAuto] ✅ SetWindowPos 성공. 작업표시줄 아이콘 숨김 및 테두리 제거 적용 시도됨.");
        }
    }

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        mutex?.Close(); // Mutex 해제
#endif
    }
}
// #endif