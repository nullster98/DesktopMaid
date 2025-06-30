// --- START OF FILE UserData.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SFB;

// [추가] 사용자의 3가지 상태 정의
public enum UserMode
{
    On,     // 활성 상태
    Sleep,  // 수면(방해금지) 상태
    Off     // AI 기능 끔
}

public class UserData : MonoBehaviour
{
    public static UserData Instance { get; private set; }

    private string apiKey;
    
    [Header("User Profile")] 
    [SerializeField] private TMP_Text userName;
    [SerializeField] private TMP_Text userProfileMessage;
    [SerializeField] private Image userProfileImage;
    [SerializeField] private Image conditionImage;
    
    [Header("User Setting")] 
    [SerializeField] private TMP_InputField userNameField;
    [SerializeField] private TMP_InputField onMessageField;
    [SerializeField] private TMP_InputField sleepMessageField;
    [SerializeField] private TMP_InputField offMessageField;
    [SerializeField] private Image userSettingImage;
    [SerializeField] private TMP_InputField userPromptField;
    
    public float SystemVolume { get; set; } = 1.0f;

    // [추가] 현재 사용자 모드 프로퍼티
    public UserMode CurrentUserMode { get; private set; } = UserMode.On;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (string.IsNullOrWhiteSpace(userNameField.text) &&
            string.IsNullOrWhiteSpace(onMessageField.text) &&
            string.IsNullOrWhiteSpace(sleepMessageField.text) &&
            string.IsNullOrWhiteSpace(offMessageField.text) &&
            string.IsNullOrWhiteSpace(userPromptField.text))
        {
            userNameField.text = "Name";
            onMessageField.text = "Message";
            sleepMessageField.text = "SleepMessage";
            offMessageField.text = "OffMessage";
        }
        
        userName.text = userNameField.text;
        userProfileImage.sprite = userSettingImage.sprite;
        
        // [수정] 시작 시 현재 모드에 맞게 UI 업데이트
        UpdateConditionUI();
    }

    public void SetAPIKey(string key)
    {
        apiKey = key;
    }

    public string GetAPIKey()
    {
        return apiKey;
    }

    public void ApplyBtn()
    {
        userName.text = userNameField.text;
        if (userSettingImage.sprite != null)
        {
            userProfileImage.sprite = userSettingImage.sprite;
        }
        
        // [수정] Apply 시 현재 상태 메시지를 다시 업데이트
        UpdateConditionUI();
        UIManager.instance.TriggerWarning("적용 완료");
    }

    // [수정] UserConditionBtn 함수를 모드 순환 방식으로 변경
    public void UserConditionBtn()
    {
        // On -> Sleep -> Off -> On 순으로 순환
        int nextMode = ((int)CurrentUserMode + 1) % 3;
        CurrentUserMode = (UserMode)nextMode;
        
        UpdateConditionUI();
    }

    // [신규] 현재 모드에 따라 UI(메시지, 아이콘)를 업데이트하는 함수
    private void UpdateConditionUI()
    {
        switch (CurrentUserMode)
        {
            case UserMode.On:
                userProfileMessage.text = onMessageField.text;
                conditionImage.sprite = UIManager.instance.modeOnSprite;
                break;
            case UserMode.Sleep:
                userProfileMessage.text = sleepMessageField.text;
                conditionImage.sprite = UIManager.instance.modeSleepSprite;
                break;
            case UserMode.Off:
                userProfileMessage.text = offMessageField.text;
                conditionImage.sprite = UIManager.instance.modeOffSprite;
                break;
        }
        Debug.Log($"[UserData] 사용자 모드가 '{CurrentUserMode}'로 변경되었습니다.");
    }
    
    public void LoadUserImage()
    {
        var extensions = new[]
        {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel("이미지 선택", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            StartCoroutine(LoadImageCoroutine(paths[0]));
        }
    }

    private IEnumerator LoadImageCoroutine(string path)
    {
        string url = "file://" + path;

        using (WWW www = new WWW(url))
        {
            yield return www;

            Texture2D tex = www.texture;
            if (tex != null)
            {
                Sprite newSprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                
                userSettingImage.sprite = newSprite;
            }
        }
    }
    
    public UserSaveData GetUserSaveData()
    {
        string imageBase64 = "";
        if (userSettingImage.sprite != null && userSettingImage.sprite.texture.isReadable)
        {
            Texture2D tex = userSettingImage.sprite.texture;
            byte[] bytes = tex.EncodeToPNG();
            imageBase64 = System.Convert.ToBase64String(bytes);
        }

        return new UserSaveData
        {
            userName = userNameField.text,
            onMessage = onMessageField.text,
            sleepMessage = sleepMessageField.text,
            offMessage = offMessageField.text,
            profileImageBase64 = imageBase64,
            userPrompt = userPromptField.text,
            // [수정] conditionIndex 대신 CurrentUserMode 저장
            conditionIndex = (int)this.CurrentUserMode,
            apiKey = apiKey
        };
    }
    
    public void ApplyUserSaveData(UserSaveData data)
    {
        userNameField.text = data.userName;
        onMessageField.text = data.onMessage;
        sleepMessageField.text = data.sleepMessage;
        offMessageField.text = data.offMessage;
        userPromptField.text = data.userPrompt;
        
        // [수정] 저장된 인덱스로 CurrentUserMode 설정
        this.CurrentUserMode = (UserMode)data.conditionIndex;
        apiKey = data.apiKey;

        if (!string.IsNullOrEmpty(data.profileImageBase64))
        {
            byte[] imageBytes = System.Convert.FromBase64String(data.profileImageBase64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            userSettingImage.sprite = sprite;
        }
        
        // [수정] ApplyBtn() 대신 개별 함수 호출로 순서 명확화
        userName.text = userNameField.text;
        if (userSettingImage.sprite != null) userProfileImage.sprite = userSettingImage.sprite;
        UpdateConditionUI();
    }
    
    public void ApplyAppConfigData(AppConfigData config)
    {
        if (config == null) return;
        this.SystemVolume = config.systemVolume;
        Debug.Log($"[UserData] Config 데이터 적용 완료. 시스템 볼륨: {this.SystemVolume}");
    }
}