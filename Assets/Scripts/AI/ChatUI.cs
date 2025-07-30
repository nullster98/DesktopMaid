// --- START OF FILE ChatUI.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Text;
using System.IO;
using System.Linq;
using SFB;
using UnityEngine.Localization;

public class ChatUI : MonoBehaviour
{
    #region 변수 및 컴포넌트 참조

    [Header("채팅 대상 ID")]
    public string presetID;

    [Header("UI 요소")]
    [SerializeField] private TMP_Text headerText;
    public TMP_InputField inputField;
    public Button sendButton;
    public Button fileAttachButton;
    public Transform chatContent;
    public GameObject userBubblePrefab;
    public GameObject aiBubblePrefab;
    public GameObject userImageBubblePrefab;
    public GameObject userFileBubblePrefab;
    public GameObject systemBubblePrefab;
    public GameObject typingIndicatorPrefab;

    [Header("사운드")]
    [SerializeField] private AudioClip aiMessageSound;

    [Header("기능 참조")]
    public ScrollRect scrollRect;
    public ChatFunction geminiChat;

    [Header("파일 첨부 설정")]
    public float maxFileSizeMB = 4.0f;
    public int imageResizeDimension = 1920;

    [Header("파일 첨부 미리보기")]
    public GameObject attachmentPreviewPanel;
    public Image attachmentIcon;
    public TMP_Text attachmentFileName;
    public Button removeAttachmentButton;
    public Sprite textFileIcon;

    [Header("UI 제어")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private int maxMessagesToKeep = 100;
    
    private bool _isRefreshing = false;
    private AudioSource audioSource;
    private byte[] _pendingImageBytes = null;
    private string _pendingTextFileContent = null;
    private string _pendingTextFileName = null;
    private GameObject _currentTypingIndicator;
    private Coroutine _typingAnimationCoroutine;
    
    public bool isGroupChat { get; private set; } = false;
    public string OwnerID { get; private set; }

    private bool _isInitialLoad = true;
    private int _lastMessageId = 0;
    private float scrollThreshold = 0.05f;
    private bool shouldAutoScroll = true;

    #endregion

    #region Unity 생명주기 및 이벤트 핸들러

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendButtonClicked);
        inputField.onSubmit.AddListener(OnInputSubmit);
        fileAttachButton.onClick.AddListener(OnClickFileAttach);
        removeAttachmentButton?.onClick.AddListener(OnRemoveAttachmentClicked);
        attachmentPreviewPanel?.SetActive(false);
    }

    private void OnEnable()
    {
        SaveController.OnLoadComplete += RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded += HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded += HandlePersonalMessageAdded;
        // [해결] 특정 채팅방 초기화 이벤트를 구독합니다.
        ChatDatabaseManager.OnChatHistoryCleared += HandleChatHistoryCleared;
        ChatDatabaseManager.OnAllChatDataCleared += ClearChatDisplay;
    }

    private void OnDisable()
    {
        SaveController.OnLoadComplete -= RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded -= HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded -= HandlePersonalMessageAdded;
        // [해결] 구독을 해제합니다.
        ChatDatabaseManager.OnChatHistoryCleared -= HandleChatHistoryCleared;
        ChatDatabaseManager.OnAllChatDataCleared -= ClearChatDisplay;
        HideTypingIndicator();
    }
    
    // [해결] 특정 채팅방의 기록이 삭제되었을 때 호출되는 핸들러
    private void HandleChatHistoryCleared(string ownerId)
    {
        // 이 이벤트가 자신에게 해당하는 경우에만 채팅창을 비웁니다.
        if (this.OwnerID == ownerId)
        {
            ClearChatDisplay();
        }
    }

    #endregion

    #region 타이핑 효과 UI 제어

    public void ShowTypingIndicator(CharacterPreset speaker)
    {
        if (typingIndicatorPrefab == null) return;
        HideTypingIndicator();
        _currentTypingIndicator = Instantiate(typingIndicatorPrefab, chatContent);
        if (_currentTypingIndicator.GetComponent<AIBubble>() is { } bubbleScript && speaker != null)
        {
            bubbleScript.Initialize(speaker.characterImage.sprite, GetLocalizedCharacterName(speaker), "");
            _typingAnimationCoroutine = StartCoroutine(AnimateTypingIndicatorRoutine(bubbleScript));
        }
        StartCoroutine(FinalizeLayoutAfterOneFrame());
    }

    private IEnumerator AnimateTypingIndicatorRoutine(AIBubble indicatorBubble)
    {
        while (true)
        {
            indicatorBubble.SetMessage("·  "); yield return new WaitForSeconds(0.5f);
            indicatorBubble.SetMessage("·· "); yield return new WaitForSeconds(0.5f);
            indicatorBubble.SetMessage("···"); yield return new WaitForSeconds(0.5f);
        }
    }

    public void HideTypingIndicator()
    {
        if (_typingAnimationCoroutine != null) StopCoroutine(_typingAnimationCoroutine);
        if (_currentTypingIndicator != null) Destroy(_currentTypingIndicator);
        _typingAnimationCoroutine = null;
        _currentTypingIndicator = null;
    }

    #endregion

    #region ChatUI 모드 설정

    public void SetupForPresetChat(CharacterPreset preset)
    {
        if (preset == null) return;
        isGroupChat = false;
        OwnerID = preset.presetID;
        this.presetID = preset.presetID;
        if (headerText != null) headerText.text = GetLocalizedCharacterName(preset);
        RefreshFromDatabase();
    }

    public void SetupForGroupChat(CharacterGroup group)
    {
        if (group == null) return;
        isGroupChat = true;
        OwnerID = group.groupID;
        this.presetID = null;
        if (headerText != null) headerText.text = group.groupName;
        RefreshFromDatabase();
    }

    #endregion

    #region 사용자 입력 및 메시지 전송

    private void OnInputSubmit(string inputText)
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) TrySendMessage();
    }
    
    private void OnSendButtonClicked() => TrySendMessage();

    private void TrySendMessage()
    {
        string userText = inputField.text.Trim();
        bool hasImage = _pendingImageBytes != null;
        bool hasTextFile = _pendingTextFileContent != null;

        if (string.IsNullOrEmpty(userText) && !hasImage && !hasTextFile) return;

        // DB 저장을 위한 데이터 구성 먼저
        string fileContent = null, fileType = null, fileName = null;
        long fileSize = 0;

        if (hasImage)
        {
            fileContent = Convert.ToBase64String(_pendingImageBytes);
            fileType = "image";
        }
        else if (hasTextFile)
        {
            fileContent = _pendingTextFileContent;
            fileType = "text";
            fileName = _pendingTextFileName;
            fileSize = new UTF8Encoding().GetByteCount(_pendingTextFileContent);
        }

        var messageData = new MessageData { textContent = userText, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
        string messageJson = JsonUtility.ToJson(messageData);
        
        // UI에 즉시 표시
        var tempRecord = new ChatDatabase.ChatMessage { SenderID = "user", Message = messageJson };
        DisplayMessage(tempRecord, false);

        // DB 저장 및 AI 요청
        if (isGroupChat)
        {
            ChatDatabaseManager.Instance.InsertGroupMessage(OwnerID, "user", messageJson);
            geminiChat.OnUserSentMessage(OwnerID, userText, fileContent, fileType, fileName, fileSize);
        }
        else
        {
            ChatDatabaseManager.Instance.InsertMessage(OwnerID, "user", messageJson);
            geminiChat.SendMessageToGemini(userText, fileContent, fileType, fileName, fileSize);
        }

        inputField.text = "";
        OnRemoveAttachmentClicked();
        inputField.ActivateInputField();
    }

    #endregion
    
    #region 파일 첨부 관련 (이전과 동일, 생략하지 않음)
    private void OnClickFileAttach()
    {
        var extensions = new[] { new ExtensionFilter("Image and Text Files", "png", "jpg", "jpeg", "txt") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("파일 선택", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string path = paths[0];
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(path).ToLower();

            FileInfo fileInfo = new FileInfo(path);
            long fileSizeInBytes = fileInfo.Length;
            long maxSizeInBytes = (long)(maxFileSizeMB * 1024 * 1024);

            if (extension == ".txt")
            {
                if (fileSizeInBytes > maxSizeInBytes) { LocalizationManager.Instance.ShowWarning("텍스트파일 경고"); return; }
                _pendingTextFileContent = File.ReadAllText(path);
                _pendingTextFileName = fileName;
                _pendingImageBytes = null;
                ShowAttachmentPreview(fileName);
            }
            else
            {
                if (fileSizeInBytes > maxSizeInBytes)
                {
                    LocalizationManager.Instance.ShowWarning("이미지 리사이징");
                    StartCoroutine(ResizeAndAttachImageCoroutine(path, fileName));
                }
                else
                {
                    _pendingImageBytes = File.ReadAllBytes(path);
                    _pendingTextFileContent = null; _pendingTextFileName = null;
                    ShowAttachmentPreview(fileName, _pendingImageBytes);
                }
            }
        }
    }

    private void OnRemoveAttachmentClicked()
    {
        _pendingImageBytes = null; _pendingTextFileContent = null; _pendingTextFileName = null;
        if (attachmentPreviewPanel != null)
        {
            if (attachmentIcon.sprite != null && attachmentIcon.sprite != textFileIcon)
            {
                Destroy(attachmentIcon.sprite.texture);
                Destroy(attachmentIcon.sprite);
            }
            attachmentIcon.sprite = null;
            attachmentPreviewPanel.SetActive(false);
        }
    }

    private void ShowAttachmentPreview(string fileName, byte[] imageBytes = null)
    {
        if (attachmentPreviewPanel == null) return;
        if (attachmentIcon.sprite != null && attachmentIcon.sprite != textFileIcon)
        {
            Destroy(attachmentIcon.sprite.texture);
            Destroy(attachmentIcon.sprite);
        }
        attachmentFileName.text = fileName;
        if (imageBytes != null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            attachmentIcon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        else
        {
            attachmentIcon.sprite = textFileIcon;
        }
        attachmentPreviewPanel.SetActive(true);
    }
    
    private IEnumerator ResizeAndAttachImageCoroutine(string path, string fileName)
    {
        byte[] originalBytes = File.ReadAllBytes(path);
        Texture2D originalTexture = new Texture2D(2, 2);
        originalTexture.LoadImage(originalBytes);
        float ratio = 1.0f;
        if (originalTexture.width > imageResizeDimension || originalTexture.height > imageResizeDimension)
            ratio = Mathf.Min((float)imageResizeDimension / originalTexture.width, (float)imageResizeDimension / originalTexture.height);
        int newWidth = Mathf.RoundToInt(originalTexture.width * ratio);
        int newHeight = Mathf.RoundToInt(originalTexture.height * ratio);
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(originalTexture, rt);
        RenderTexture.active = rt;
        Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(originalTexture);
        _pendingImageBytes = resizedTexture.EncodeToPNG();
        Destroy(resizedTexture);
        _pendingTextFileContent = null;
        _pendingTextFileName = null;
        yield return null;
        ShowAttachmentPreview(fileName, _pendingImageBytes);
    }
    #endregion

    #region 채팅 버블 생성 및 UI 조작

    public void AddChatBubble(string text, bool isUser, bool playSound, CharacterPreset speaker = null)
    {
        if (scrollRect != null) shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        GameObject chatBubbleInstance = Instantiate(isUser ? userBubblePrefab : aiBubblePrefab, chatContent);
        string processedText = InsertZeroWidthSpaces(text);
        if (playSound && !isUser) PlayMessageSound();

        TMP_Text messageText;
        if (isUser)
        {
            messageText = chatBubbleInstance.GetComponentInChildren<TMP_Text>();
            if (messageText != null) messageText.text = processedText;
        }
        else
        {
            AIBubble bubbleScript = chatBubbleInstance.GetComponent<AIBubble>();
            if (bubbleScript == null) { Destroy(chatBubbleInstance); return; }
            var preset = speaker ?? CharacterPresetManager.Instance.GetPreset(this.OwnerID);
            if (preset != null) bubbleScript.Initialize(preset.characterImage.sprite, GetLocalizedCharacterName(preset), processedText);
            messageText = bubbleScript.GetMessageTextComponent();
        }
        // [해결] 말풍선 길이 조절 코루틴 호출을 복원합니다.
        StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(messageText, 10f, 350f));
    }
    
    // 이 함수들은 AddChatBubble 내부 로직과 중복되므로 간소화/제거하고, DisplayMessage에서 직접 처리합니다.
    private void AddImageBubble(byte[] imageBytes)
    {
        if (scrollRect != null) shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        GameObject bubbleInstance = Instantiate(userImageBubblePrefab, chatContent);
        Image contentImage = bubbleInstance.transform.Find("box/ContentImage")?.GetComponent<Image>();
        if (contentImage != null)
        {
            Texture2D tex = new Texture2D(2, 2); tex.LoadImage(imageBytes);
            contentImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            var layoutElement = contentImage.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                float ratio = (float)tex.width / tex.height;
                layoutElement.preferredHeight = Mathf.Clamp(350f / ratio, 100f, 500f);
            }
        }
        StartCoroutine(FinalizeLayout());
    }

    private void AddFileBubble(string fileName, long fileSize)
    {
        if (scrollRect != null) shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        GameObject bubbleInstance = Instantiate(userFileBubblePrefab, chatContent);
        TMP_Text fileInfoText = bubbleInstance.GetComponentInChildren<TMP_Text>();
        if (fileInfoText != null)
        {
            string sizeStr = (fileSize > 1024 * 1024) ? $"{fileSize / (1024.0 * 1024.0):F2} MB" : $"{fileSize / 1024.0:F2} KB";
            fileInfoText.text = $"{fileName}\n({sizeStr})";
        }
        StartCoroutine(FinalizeLayout());
    }
    
    private void AddSystemBubble(string text)
    {
        GameObject systemBubbleInstance = Instantiate(systemBubblePrefab, chatContent);
        systemBubbleInstance.GetComponentInChildren<TMP_Text>().text = text;
        StartCoroutine(FinalizeLayout());
    }

    private void PlayMessageSound()
    {
        if (audioSource != null && aiMessageSound != null && UserData.Instance != null && canvasGroup.alpha > 0)
            audioSource.PlayOneShot(aiMessageSound, UserData.Instance.SystemVolume);
    }

    private string GetLocalizedCharacterName(CharacterPreset preset) => preset?.characterName ?? "Unknown";

    private IEnumerator AdjustBubbleSizeAndFinalizeLayout(TMP_Text textComponent, float minWidth, float maxWidth)
    {
        if (textComponent == null) yield break;
        yield return new WaitForEndOfFrame();
        RectTransform bubbleRect = textComponent.transform.parent.GetComponent<RectTransform>();
        var layoutGroup = bubbleRect.GetComponent<VerticalLayoutGroup>();
        if (bubbleRect == null || layoutGroup == null) yield break;

        float preferredWidth = textComponent.GetPreferredValues().x;
        float finalWidth = Mathf.Clamp(preferredWidth + layoutGroup.padding.left + layoutGroup.padding.right, minWidth, maxWidth);
        textComponent.enableWordWrapping = finalWidth >= maxWidth;
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
        yield return null;
        if (scrollRect != null && shouldAutoScroll) scrollRect.verticalNormalizedPosition = 0f;
        CleanupOldMessages();
    }

    private IEnumerator FinalizeLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent.GetComponent<RectTransform>());
        yield return null;
        if (scrollRect != null && shouldAutoScroll) scrollRect.verticalNormalizedPosition = 0f;
        CleanupOldMessages();
    }

    private IEnumerator FinalizeLayoutAfterOneFrame()
    {
        yield return new WaitForEndOfFrame();
        if (chatContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent.GetComponent<RectTransform>());
        yield return null;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    private void CleanupOldMessages()
    {
        if (maxMessagesToKeep <= 0) return;
        while (chatContent.childCount > maxMessagesToKeep + (_currentTypingIndicator != null ? 1 : 0))
        {
            Transform childToRemove = chatContent.GetChild(0);
            if (childToRemove == _currentTypingIndicator) childToRemove = chatContent.GetChild(1);
            Destroy(childToRemove.gameObject);
        }
    }

    private string InsertZeroWidthSpaces(string originalText)
    {
        if (string.IsNullOrEmpty(originalText)) return "";
        const char ZWSP = '\u200B';
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < originalText.Length; i++)
        {
            sb.Append(originalText[i]);
            if (i < originalText.Length - 1 && !char.IsWhiteSpace(originalText[i]) && !char.IsWhiteSpace(originalText[i + 1]))
                sb.Append(ZWSP);
        }
        return sb.ToString();
    }

    #endregion

    #region 데이터베이스 연동 및 UI 상태 관리

    public void RefreshFromDatabase()
    {
        if (string.IsNullOrEmpty(OwnerID) || _isRefreshing) return;
        _isRefreshing = true;
        _isInitialLoad = true;
        HideTypingIndicator();
        StartCoroutine(RefreshRoutine());
    }

    private IEnumerator RefreshRoutine()
    {
        foreach (Transform child in chatContent) Destroy(child.gameObject);
        yield return null;

        var messages = isGroupChat
            ? ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, maxMessagesToKeep > 0 ? maxMessagesToKeep : 100)
            : ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, maxMessagesToKeep > 0 ? maxMessagesToKeep : 100);

        foreach (var msg in messages)
        {
            DisplayMessage(msg, false);
        }
        
        yield return StartCoroutine(FinalizeLayout());
        _isRefreshing = false;
        _isInitialLoad = false;
        _lastMessageId = messages.LastOrDefault()?.Id ?? 0;
    }

    private void HandlePersonalMessageAdded(string updatedPresetId, bool isUserMessage)
    {
        if (isGroupChat || OwnerID != updatedPresetId || isUserMessage) return;
        if (_isInitialLoad) RefreshFromDatabase(); else AppendNewMessage();
    }

    private void HandleGroupMessageAdded(string updatedGroupId, bool isUserMessage)
    {
        if (!isGroupChat || OwnerID != updatedGroupId || isUserMessage) return;
        if (_isInitialLoad) RefreshFromDatabase(); else AppendNewMessage();
    }

    private void AppendNewMessage()
    {
        var msg = (isGroupChat 
            ? ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 1) 
            : ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 1))
            .FirstOrDefault();
            
        if (msg == null || msg.Id <= _lastMessageId) return;
        
        DisplayMessage(msg, true);
        _lastMessageId = msg.Id;
    }

    private void DisplayMessage(ChatDatabase.ChatMessage messageRecord, bool playSound)
    {
        // [해결] 시스템 메시지를 가장 먼저 확인합니다.
        if (messageRecord.SenderID == "system")
        {
            var data = JsonUtility.FromJson<MessageData>(messageRecord.Message);
            AddSystemBubble(data.textContent);
            return;
        }

        var messageData = JsonUtility.FromJson<MessageData>(messageRecord.Message);
        bool isUser = messageRecord.SenderID == "user";
        
        // [해결] 메시지 타입에 따라 하나의 말풍선만 생성되도록 if-else if 구조로 변경합니다.
        if (messageData.type == "image" && !string.IsNullOrEmpty(messageData.fileContent))
        {
            AddImageBubble(Convert.FromBase64String(messageData.fileContent));
        }
        else if (messageData.type == "text" && messageData.fileSize > 0)
        {
            AddFileBubble(messageData.fileName, messageData.fileSize);
        }
        
        // 텍스트 내용이 있다면 별도로 텍스트 말풍선을 추가합니다. (이미지/파일과 텍스트 동시 전송 지원)
        if (!string.IsNullOrEmpty(messageData.textContent))
        {
            CharacterPreset speaker = isUser ? null : CharacterPresetManager.Instance.GetPreset(isGroupChat ? messageRecord.SenderID : OwnerID);
            AddChatBubble(messageData.textContent, isUser, playSound, speaker);
        }
    }

    private void ClearChatDisplay()
    {
        if (chatContent == null) return;
        HideTypingIndicator();
        foreach (Transform child in chatContent) Destroy(child.gameObject);
        _lastMessageId = 0;
        _isInitialLoad = true;
    }
    
    public void OnResetBtnClicked()
    {
        if (string.IsNullOrEmpty(OwnerID)) return;
        Action onConfirm = () => {
            if (isGroupChat) ChatDatabaseManager.Instance.ClearGroupHistoryAndMemories(OwnerID);
            else ChatDatabaseManager.Instance.ClearMessages(OwnerID);
        };
        LocalizationManager.Instance.ShowConfirmationPopup("Popup_Title_ChatReset", "Popup_Msg_ChatReset", onConfirm);
    }
    
    public void ShowChatUI(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1 : 0;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void TryDisableNotification()
    {
        if (isGroupChat)
        {
            if (CharacterGroupManager.Instance.GetGroup(OwnerID) is {} group) group.HasNotification = false;
        }
        else
        {
            if (CharacterPresetManager.Instance.GetPreset(OwnerID) is {} preset && preset.notifyImage != null)
                preset.notifyImage.SetActive(false);
        }
    }
    #endregion
}
// --- END OF FILE ChatUI.cs ---