// --- START OF FILE SaveController.cs ---

using System;
using System.Linq;
using UnityEngine;

public class SaveController : MonoBehaviour
{
    public static event Action OnLoadComplete;
    public static bool isLoadCompleted { get; private set; } = false;
    
    public float saveInterval = 60f;
    private float timer;

    void Start()
    {
        LoadEverything();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= saveInterval)
        {
            SaveEverything();
            timer = 0f;
        }
    }

    public void SaveEverything()
    {
        var aisObserver = FindObjectOfType<AIScreenObserver>();
        var transparentOverlay = FindObjectOfType<TransparentOverlay>();
        var windowAutoStart = FindObjectOfType<WindowAutoStart>();
        
        AppConfigData configData = new AppConfigData();

        if (transparentOverlay != null) configData.alwaysOnTop = transparentOverlay.IsAlwaysOnTopState;
        if (windowAutoStart != null) configData.autoStartEnabled = windowAutoStart.IsAutoStartEnabled;
        if (aisObserver != null)
        {
            configData.screenCaptureModuleEnabled = aisObserver.screenCaptureModuleEnabled;
            configData.selfAwarenessModuleEnabled = aisObserver.selfAwarenessModuleEnabled;
        }
        if (CameraManager.Instance != null) configData.cameraZoomLevel = CameraManager.Instance.CurrentCameraSize;
        
        SaveData.SaveAll(
            UserData.Instance.GetUserSaveData(),
            CharacterPresetManager.Instance.GetAllPresetData(),
            CharacterGroupManager.Instance.allGroups,
            configData
        );
    }

    public void LoadEverything()
    {
        isLoadCompleted = false;
        
        var data = SaveData.LoadAll();
        if(data == null)
        {
            OnLoadComplete?.Invoke();
            return;
        }

        UserData.Instance.ApplyUserSaveData(data.userData);
        CharacterPresetManager.Instance.LoadPresetsFromData(data.presets);
        
        if (CharacterGroupManager.Instance != null && data.groups != null)
        {
            CharacterGroupManager.Instance.allGroups = data.groups;
            // 로드된 그룹 정보와 프리셋의 groupID를 동기화
            foreach(var group in CharacterGroupManager.Instance.allGroups)
            {
                foreach(var memberId in group.memberPresetIDs)
                {
                    var preset = CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == memberId);
                    if(preset != null)
                    {
                        preset.groupID = group.groupID;
                    }
                }
            }
        }
        
        isLoadCompleted = true;
        
        // 각 컴포넌트의 Start()에서 자신의 설정을 로드하므로, 여기서 config를 적용할 필요는 없습니다.
        Debug.Log("전체 데이터 로드 완료.");
        OnLoadComplete?.Invoke();
    }

    public void OnClickSave()
    {
        SaveEverything();
        UIManager.instance.TriggerWarning("저장 완료");
    }
}