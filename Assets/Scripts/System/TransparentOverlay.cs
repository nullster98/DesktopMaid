// --- START OF FILE TransparentOverlay.cs ---

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class TransparentOverlay : MonoBehaviour
{
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000;
    const int LWA_COLORKEY = 0x1;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_SHOWWINDOW = 0x0040;
    const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW;

    private IntPtr hwnd;
    private bool isAlwaysOnTop;
    public bool IsAlwaysOnTopState => isAlwaysOnTop; // SaveController 접근용

    [Header("버튼 이미지")]
    [SerializeField] private Sprite onButton;
    [SerializeField] private Sprite offButton;
    [SerializeField] private Image buttonImage;

    void Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        StartCoroutine(DelayedInit());
#endif
    }

    private System.Collections.IEnumerator DelayedInit()
    {
        yield return null; 

        hwnd = Process.GetCurrentProcess().MainWindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            yield break;
        }

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        SetLayeredWindowAttributes(hwnd, 0x000A000A, 0, LWA_COLORKEY);

        var config = SaveData.LoadAll()?.config;
        isAlwaysOnTop = config?.alwaysOnTop ?? true; // 기본값 true
        
        ApplyAlwaysOnTop(isAlwaysOnTop);
        UpdateToggleImage();
    }

    public void SetAlwaysOnTop()
    {
        isAlwaysOnTop = !isAlwaysOnTop;
        ApplyAlwaysOnTop(isAlwaysOnTop);
        UpdateToggleImage();
    }
    
    public void ApplyAlwaysOnTop(bool isTop)
    {
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, isTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
        }
    }

    private void UpdateToggleImage()
    {
        if (buttonImage != null)
            buttonImage.sprite = isAlwaysOnTop ? onButton : offButton;
    }
}