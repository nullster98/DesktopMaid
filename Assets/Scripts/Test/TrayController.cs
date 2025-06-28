// #if UNITY_STANDALONE_WIN
// using System;
// using System.Windows.Forms;
// using System.Runtime.InteropServices;
// using UnityEngine;
// using UnityApp = UnityEngine.Application;
// using WinFormsApp = System.Windows.Forms.Application;
// using System.IO;
//
// public class TrayController : MonoBehaviour
// {
//     private NotifyIcon trayIcon;
//
//     [Header("UI 숨김/복원용 Canvas")]
//     [SerializeField] public GameObject mainCanvas;
//
//     [DllImport("user32.dll")]
//     private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
//
//     [DllImport("user32.dll")]
//     private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
//
//     private const int SW_HIDE = 0;
//     private const int SW_SHOW = 5;
//
//     private IntPtr windowHandle;
//
//     void Start()
//     {
//         // Unity 창 핸들 가져오기
//         windowHandle = FindWindow(null, UnityEngine.Application.productName);
//
//         // 트레이 아이콘 설정
//         trayIcon = new NotifyIcon();
//         trayIcon.Text = "Unity Tray App";
//
//         // 아이콘 설정 (없으면 생략 가능)
//         trayIcon.Icon = null; // 또는 SystemIcons.Application;
//
//         // 메뉴 구성
//         var menu = new ContextMenuStrip();
//         var openItem = new ToolStripMenuItem("열기");
//         openItem.Click += OnShow;
//
//         var exitItem = new ToolStripMenuItem("종료");
//         exitItem.Click += OnExit;
//         trayIcon.ContextMenuStrip = menu;
//
//         trayIcon.Visible = true;
//
//         // 더블 클릭 → 열기
//         trayIcon.DoubleClick += (s, e) => OnShow(s, e);
//     }
//
//     // 외부에서 호출 (버튼용)
//     public void HideToTray()
//     {
//         if (mainCanvas != null)
//             mainCanvas.SetActive(false);
//
//         if (windowHandle != IntPtr.Zero)
//             ShowWindow(windowHandle, SW_HIDE);
//     }
//
//     private void OnShow(object sender, EventArgs e)
//     {
//         if (mainCanvas != null)
//             mainCanvas.SetActive(true);
//
//         if (windowHandle != IntPtr.Zero)
//             ShowWindow(windowHandle, SW_SHOW);
//     }
//
//     private void OnExit(object sender, EventArgs e)
//     {
//         trayIcon.Visible = false;
//         UnityEngine.Application.Quit();
//     }
//
//     private void OnApplicationQuit()
//     {
//         trayIcon.Visible = false;
//     }
// }
// #endif