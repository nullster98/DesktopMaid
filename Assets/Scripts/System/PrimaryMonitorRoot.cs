using System;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class PrimaryMonitorRoot : MonoBehaviour
{
    // Win32 constants
    const int MONITORINFOF_PRIMARY = 1;
    const int SM_XVIRTUALSCREEN = 76;
    const int SM_YVIRTUALSCREEN = 77;

    // Win32 types
    delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, IntPtr lprc, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // Win32 imports
    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    void Start()
    {
        // 1. 주모니터 RECT 찾기
        MONITORINFO pri = default;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, hdc, lprc, data) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMon, ref mi) && (mi.dwFlags & MONITORINFOF_PRIMARY) != 0)
            {
                pri = mi;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        // 2. 가상 화면 오프셋
        int vX = GetSystemMetrics(SM_XVIRTUALSCREEN); // -1080 (예시)
        int vY = GetSystemMetrics(SM_YVIRTUALSCREEN); // -368  (예시)

        // 3. 주모니터 위치·크기
        int px = pri.rcMonitor.left;
        int py = pri.rcMonitor.top;
        int pw = pri.rcMonitor.right  - pri.rcMonitor.left;
        int ph = pri.rcMonitor.bottom - pri.rcMonitor.top;

        // 4. RectTransform 세팅
        var rt = GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); // 절대 좌상단 기준
        rt.pivot     = new Vector2(0, 1);

        // Unity는 y축이 반대라 - 로 뒤집음
        rt.anchoredPosition = new Vector2(px - vX, -(py - vY));
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pw);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   ph);

        Debug.Log($"[PrimaryMonitorRoot] virtual({vX},{vY})  primary({px},{py})  size({pw}×{ph})");
        Debug.Log($"ROOT rect=({rt.rect.xMin},{rt.rect.yMin})→({rt.rect.xMax},{rt.rect.yMax})");
    }
}
