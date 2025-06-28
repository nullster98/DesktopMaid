// --- START OF FILE WindowAutoStart.cs ---

#if UNITY_STANDALONE_WIN
using Microsoft.Win32;
#endif
using UnityEngine;
using UnityEngine.UI;

public static class AutoStart
{
#if UNITY_STANDALONE_WIN
    private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public static void SetAutoStart(string appName, string exePath, bool enable) { /* ... */ }
    public static bool IsAutoStartEnabled(string appName, string exePath) { /* ... */ return false; }
#else
    public static void SetAutoStart(string appName, string exePath, bool enable) { }
    public static bool IsAutoStartEnabled(string appName, string exePath) => false;
#endif
}

public class WindowAutoStart : MonoBehaviour
{
    public Button toggleButton;

    private string appName = "MyUnityApp";
    private string exePath;
    private bool isAutoStart;
    public bool IsAutoStartEnabled => isAutoStart; // SaveController 접근용

    public static bool WasStartedByAutoRun { get; private set; } = false;

    void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

        var config = SaveData.LoadAll()?.config;
        isAutoStart = config?.autoStartEnabled ?? false;

        AutoStart.SetAutoStart(appName, exePath, isAutoStart);

        if (isAutoStart)
        {
            WasStartedByAutoRun = true;
            Debug.Log("[WindowAutoStart] 자동 시작으로 프로그램이 실행되었습니다.");
        }
        
        UpdateBtnUI();
        toggleButton.onClick.AddListener(ToggleAutoStart);
#endif
    }

    void ToggleAutoStart()
    {
#if UNITY_STANDALONE_WIN
        isAutoStart = !isAutoStart;
        AutoStart.SetAutoStart(appName, exePath, isAutoStart);
        UpdateBtnUI();
        Debug.Log($"[AutoStart] 자동 실행 상태: {isAutoStart}");
#endif
    }

    void UpdateBtnUI()
    {
        if (toggleButton != null)
        {
            Image buttonImage = toggleButton.GetComponent<Image>();
            if (buttonImage != null && UIManager.instance != null)
            {
                buttonImage.sprite = isAutoStart
                    ? UIManager.instance.toggleOnSprite
                    : UIManager.instance.toggleOffSprite;
            }
        }
    }
}