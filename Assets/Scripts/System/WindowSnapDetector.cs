// 전제조건 정리용 구조 (물리 연산 없이 외부 창 인식 및 위치 겹침 판단)

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class WindowSnapDetector : MonoBehaviour
{
    [Serializable]
    public struct WindowEntry
    {
        public IntPtr hwnd;
        public RECT rect;
    }

    public Transform vrmTransform; // 이동시킬 VRM 캐릭터 트랜스폼
    public Camera mainCamera;      // 월드 ↔ 스크린 좌표 변환용 카메라
    public bool useMockWindow = true; // Mock 창 사용할지 여부

    private List<WindowEntry> cachedWindows = new();

    void Update()
    {
        UpdateWindowList();
        DetectIfVRMOverWindow();
    }

    void UpdateWindowList()
    {
        cachedWindows.Clear();

        if (useMockWindow)
        {
            // 가짜 윈도우 하나 추가 (좌표는 데스크탑 기준)
            cachedWindows.Add(new WindowEntry {
                hwnd = IntPtr.Zero,
                rect = new RECT { Left = 500, Top = 300, Right = 900, Bottom = 500 }
            });
        }
        else
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (!GetWindowRect(hWnd, out RECT r)) return true;
                cachedWindows.Add(new WindowEntry { hwnd = hWnd, rect = r });
                return true;
            }, IntPtr.Zero);
        }
    }

    void DetectIfVRMOverWindow()
    {
        if (vrmTransform == null || mainCamera == null) return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(vrmTransform.position);
        screenPos.y = Screen.currentResolution.height - screenPos.y; // 데스크탑 좌표계 보정

        Vector2 desktopPos = new(screenPos.x, screenPos.y);

        foreach (var win in cachedWindows)
        {
            if (IsPointInsideWindow(desktopPos, win.rect))
            {
                Debug.Log($"🪟 VRM은 창 위에 있습니다 → HWND: {win.hwnd}");
                return;
            }
        }
    }

    bool IsPointInsideWindow(Vector2 point, RECT rect)
    {
        return point.x >= rect.Left && point.x <= rect.Right &&
               point.y >= rect.Top && point.y <= rect.Bottom;
    }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
