using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class AlignToPrimaryMonitor : MonoBehaviour
{
    #region Windows API Imports
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    #endregion

    private RectTransform rectTransform;
    
    void Awake() => rectTransform = GetComponent<RectTransform>();

    void Start()
    {
        AlignCanvas();
    }
    
#if UNITY_EDITOR        // 에디터에서만 실행
    void OnDrawGizmos()
    {
        if (!rectTransform) return;
        // 월드 좌표로 변환
        Vector3 bl = rectTransform.TransformPoint(rectTransform.rect.min);          // bottom-left
        Vector3 tr = rectTransform.TransformPoint(rectTransform.rect.max);          // top-right
        Vector3 br = new Vector3(tr.x, bl.y, 0);
        Vector3 tl = new Vector3(bl.x, tr.y, 0);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
#endif


    private void AlignCanvas()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        IntPtr primaryMonitorHandle = MonitorFromWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle, MONITOR_DEFAULTTOPRIMARY);
        
        MONITORINFOEX monitorInfo = new MONITORINFOEX();
        monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

        if (GetMonitorInfo(primaryMonitorHandle, ref monitorInfo))
        {
            // 주 모니터의 가상 데스크탑 기준 좌표
            RECT monitorRect = monitorInfo.rcMonitor;

            // 전체 가상 화면의 크기
            int virtualScreenWidth = GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
            int virtualScreenHeight = GetSystemMetrics(79); // SM_CYVIRTUALSCREEN

            // RectTransform을 Stretch-Stretch 모드로 설정
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // --- [수정된 최종 계산 로직] ---
            // offsetMin = (왼쪽 여백, 아래쪽 여백)
            // offsetMax = -(오른쪽 여백, 위쪽 여백)

            // 왼쪽 여백 = 주 모니터의 왼쪽 경계 - 가상화면의 왼쪽 경계
            float leftOffset = monitorRect.Left - FullScreenAuto.VirtualScreenX;

            // 오른쪽 여백 = 전체 가상화면 오른쪽 경계 - 주 모니터의 오른쪽 경계
            float rightOffset = (FullScreenAuto.VirtualScreenX + virtualScreenWidth) - monitorRect.Right;
            
            // 아래쪽 여백 = 주 모니터의 아래쪽 경계 - 가상화면의 아래쪽 경계
            // Windows Y좌표는 위가 0, Unity Y좌표는 아래가 0이므로 변환이 필요.
            // (가상화면 높이 - 주 모니터 Bottom) - (가상화면 높이 - 가상화면 Bottom)
            // = 가상화면 Bottom - 주 모니터 Bottom
            float bottomOffset = (FullScreenAuto.VirtualScreenY + virtualScreenHeight) - monitorRect.Bottom;
            
            // 위쪽 여백 = 주 모니터의 위쪽 경계 - 가상화면의 위쪽 경계
            float topOffset = monitorRect.Top - FullScreenAuto.VirtualScreenY;

            rectTransform.offsetMin = new Vector2(leftOffset, bottomOffset);
            rectTransform.offsetMax = new Vector2(-rightOffset, -topOffset);
            // --- [수정된 최종 계산 로직 끝] ---

            Debug.Log($"[AlignToPrimaryMonitor] 주 모니터에 맞게 Canvas 조정 완료. Offsets(L,B): {rectTransform.offsetMin}, Offsets(R,T): {rectTransform.offsetMax}");
        }
        else
        {
             Debug.LogError("[AlignToPrimaryMonitor] 주 모니터 정보를 가져오는 데 실패했습니다.");
        }
#else
        // 에디터에서는 전체 화면을 채우도록 둡니다.
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        Debug.LogWarning("[AlignToPrimaryMonitor] 에디터에서는 주 모니터 정렬을 건너뜁니다.");
#endif
    }
}