using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Application = System.Windows.Forms.Application;

public class CommanderReceiver : MonoBehaviour
{
    public GameObject mainCanvas;
    public string commandFileName = "command.txt";
    
    private string commandFilePath;
    private float checkInterval = 1.0f;
    private float timer = 0f;
    
    private CanvasGroup mainCanvasGroup;
    void Start()
    {
        // CanvasGroup 컴포넌트를 찾아서 캐싱
        if (mainCanvas != null)
        {
            mainCanvasGroup = mainCanvas.GetComponent<CanvasGroup>();
            if (mainCanvasGroup == null)
            {
                UnityEngine.Debug.LogError("CommanderReceiver: mainCanvas에 CanvasGroup 컴포넌트가 없습니다!", mainCanvas);
            }
        }
        
        commandFilePath = Path.Combine(UnityEngine.Application.dataPath, "..", commandFileName);
        
        string trayPath = Path.Combine(UnityEngine.Application.dataPath, "..", "TrayHelper.exe");
        if (File.Exists(trayPath))
        {
            Process.Start(trayPath);
        }
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            CheckCommandFile();
        }
    }
    
    private void CheckCommandFile()
    {
        if (File.Exists(commandFilePath))
        {
            string command = File.ReadAllText(commandFilePath).Trim().ToUpper();

            switch (command)
            {
                case "HIDE_UI":
                    HideCanvas();
                    break;
                case "SHOW_UI":
                    ShowCanvas();
                    break;
                case "EXIT":
                    UnityEngine.Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    break;
            }

            File.Delete(commandFilePath); // 한 번만 처리
        }
    }
    
    public void HideCanvasFromUnityButton()
    {
        HideCanvas();
        File.WriteAllText(commandFilePath, "HIDE_UI");
    }

    private void HideCanvas()
    {
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
        }
    }

    // CanvasGroup을 이용해 UI를 표시하는 함수
    private void ShowCanvas()
    {
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;
        }
    }
}