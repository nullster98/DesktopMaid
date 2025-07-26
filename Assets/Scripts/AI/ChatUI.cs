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

/// <summary>
/// 채팅창의 모든 UI 요소와 사용자 입력을 관리하는 컨트롤러.
/// ChatFunction과 연동하여 메시지 표시, 파일 첨부, DB 동기화 등의 기능을 수행합니다.
/// </summary>
public class ChatUI : MonoBehaviour, IPointerDownHandler
{
    #region Variables & Component References

    [Header("채팅 대상 ID")]
    [Tooltip("레거시 1:1 채팅 ID. 현재는 OwnerID로 통합 관리되지만, 외부 참조를 위해 유지됩니다.")]
    public string presetID; 
    
    /// <summary>
    /// 현재 채팅창의 소유자 ID (1:1 채팅 시 presetID, 그룹 채팅 시 groupID).
    /// </summary>
    public string OwnerID { get; private set; }

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
    
    [Header("기능 참조 및 설정")]
    public ScrollRect scrollRect;
    public ChatFunction geminiChat;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] [Tooltip("화면에 유지할 최대 메시지 개수. 0 이하는 무제한입니다.")] 
    private int maxMessagesToKeep = 100;
    
    [Header("파일 첨부")]
    public GameObject attachmentPreviewPanel;
    public Image attachmentIcon;
    public TMP_Text attachmentFileName;
    public Button removeAttachmentButton;
    public Sprite textFileIcon;
    [Tooltip("첨부 파일의 최대 용량 (MB 단위)")]
    public float maxFileSizeMB = 4.0f;
    [Tooltip("용량 초과 시 리사이징될 이미지의 최대 가로/세로 크기 (픽셀)")]
    public int imageResizeDimension = 1920;

    [Header("사운드")]
    [SerializeField] private AudioClip aiMessageSound;

    // --- 내부 상태 변수 ---
    private bool isGroupChat = false;
    private AudioSource audioSource;
    private Coroutine _typingAnimationCoroutine;
    private GameObject _currentTypingIndicator;
    private bool _isRefreshing = false;
    
    // 파일 첨부 데이터
    private byte[] _pendingImageBytes = null;
    private string _pendingTextFileContent = null;
    private string _pendingTextFileName = null;
    
    // DB 실시간 동기화를 위한 변수
    private bool _isInitialPersonalLoad = true;
    private int _lastPersonalMessageId = 0;
    private bool _isInitialGroupLoad = true;
    private int _lastGroupMessageId = 0;
    private bool shouldAutoScroll = true;
    private float scrollThreshold = 0.01f;

    #endregion

    #region Unity Lifecycle & Event Handlers

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendButtonClicked);
        inputField.onSubmit.AddListener(OnInputSubmit);
        fileAttachButton.onClick.AddListener(OnClickFileAttach);
        if (removeAttachmentButton != null)
        {
            removeAttachmentButton.onClick.AddListener(OnRemoveAttachmentClicked);
        }
        if (attachmentPreviewPanel != null)
        {
            attachmentPreviewPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // DB 이벤트 구독
        SaveController.OnLoadComplete += RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded += HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded += HandlePersonalMessageAdded;
        ChatDatabaseManager.OnAllChatDataCleared += ClearChatDisplay;
    }

    private void OnDisable()
    {
        // DB 이벤트 구독 해제
        SaveController.OnLoadComplete -= RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded -= HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded -= HandlePersonalMessageAdded;
        ChatDatabaseManager.OnAllChatDataCleared -= ClearChatDisplay;
        
        HideTypingIndicator(); // 비활성화 시 타이핑 UI 정리
    }

    #endregion
    
    #region Setup

    /// <summary>
    /// 이 채팅 UI를 특정 캐릭터와의 1:1 채팅 모드로 설정합니다.
    /// </summary>
    public void SetupForPresetChat(CharacterPreset preset)
    {
        if (preset == null) return;
        this.isGroupChat = false;
        this.OwnerID = preset.presetID;
        this.presetID = preset.presetID;

        if (headerText != null)
        {
            headerText.text = GetLocalizedCharacterName(preset);
        }

        RefreshFromDatabase();
    }

    /// <summary>
    /// 이 채팅 UI를 특정 그룹 채팅 모드로 설정합니다.
    /// </summary>
    public void SetupForGroupChat(CharacterGroup group)
    {
        if (group == null) return;
        this.isGroupChat = true;
        this.OwnerID = group.groupID;
        this.presetID = null;

        if (headerText != null)
        {
            headerText.text = group.groupName;
        }

        RefreshFromDatabase();
    }

    #endregion

    #region User Input & Message Sending

    private void OnSendButtonClicked()
    {
        TrySendMessage();
    }

    private void OnInputSubmit(string inputText)
    {
        // Shift + Enter는 줄바꿈으로 처리하고, Enter만 눌렀을 때 전송
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            TrySendMessage();
        }
    }

    private void TrySendMessage()
    {
        string userText = inputField.text.Trim();
        bool hasImage = _pendingImageBytes != null;
        bool hasTextFile = _pendingTextFileContent != null;

        if (string.IsNullOrEmpty(userText) && !hasImage && !hasTextFile) return;

        // 1. 사용자 메시지를 UI에 즉시 표시
        if (hasImage) AddImageBubble(_pendingImageBytes);
        if (hasTextFile) AddFileBubble(_pendingTextFileName, new UTF8Encoding().GetByteCount(_pendingTextFileContent));
        if (!string.IsNullOrEmpty(userText)) AddChatBubble(userText, true);

        // 2. 전송할 파일 데이터 준비
        string fileContent = null;
        string fileType = null;
        string fileName = null;
        long fileSize = 0;

        if (hasImage)
        {
            fileContent = System.Convert.ToBase64String(_pendingImageBytes);
            fileType = "image";
        }
        else if (hasTextFile)
        {
            fileContent = _pendingTextFileContent;
            fileType = "text";
            fileName = _pendingTextFileName;
            fileSize = new UTF8Encoding().GetByteCount(_pendingTextFileContent);
        }

        // 3. DB에 저장하고 ChatFunction에 AI 요청 위임
        var userMessageData = new MessageData { textContent = userText, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
        string jsonMessage = JsonUtility.ToJson(userMessageData);
        
        if (isGroupChat)
        {
            ChatDatabaseManager.Instance.InsertGroupMessage(OwnerID, "user", jsonMessage);
            geminiChat.OnUserSentMessage(OwnerID, userText, fileContent, fileType, fileName, fileSize);
        }
        else
        {
            ChatDatabaseManager.Instance.InsertMessage(OwnerID, "user", jsonMessage);
            geminiChat.SendMessageToGemini(userText, fileContent, fileType, fileName, fileSize);
        }

        // 4. 입력 필드 및 첨부파일 상태 초기화
        inputField.text = "";
        OnRemoveAttachmentClicked();
        inputField.ActivateInputField();
    }
    
    #endregion

    #region File Attachment

    private void OnClickFileAttach()
    {
        var extensions = new[] { new ExtensionFilter("Image and Text Files", "png", "jpg", "jpeg", "txt") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("파일 선택", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string path = paths[0];
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(path).ToLower();
            long fileSizeInBytes = new FileInfo(path).Length;
            long maxSizeInBytes = (long)(maxFileSizeMB * 1024 * 1024);

            if (extension == ".txt")
            {
                if (fileSizeInBytes > maxSizeInBytes)
                {
                    LocalizationManager.Instance.ShowWarning("텍스트파일 경고");
                    return;
                }
                _pendingTextFileContent = File.ReadAllText(path);
                _pendingTextFileName = fileName;
                _pendingImageBytes = null;
                ShowAttachmentPreview(fileName);
            }
            else // 이미지 파일
            {
                if (fileSizeInBytes > maxSizeInBytes)
                {
                    LocalizationManager.Instance.ShowWarning("이미지 리사이징");
                    StartCoroutine(ResizeAndAttachImageCoroutine(path, fileName));
                }
                else
                {
                    _pendingImageBytes = File.ReadAllBytes(path);
                    _pendingTextFileContent = null;
                    _pendingTextFileName = null;
                    ShowAttachmentPreview(fileName, _pendingImageBytes);
                }
            }
        }
    }
    
    private void OnRemoveAttachmentClicked()
    {
        _pendingImageBytes = null;
        _pendingTextFileContent = null;
        _pendingTextFileName = null;

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
        
        // 이전 프리뷰 이미지 리소스 정리
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
        // 이미지를 리사이징하여 _pendingImageBytes에 할당하는 로직 (원본과 동일)
        byte[] originalBytes = File.ReadAllBytes(path);
        Texture2D originalTexture = new Texture2D(2, 2);
        originalTexture.LoadImage(originalBytes);

        float ratio = 1.0f;
        if (originalTexture.width > imageResizeDimension || originalTexture.height > imageResizeDimension)
        {
            float widthRatio = (float)imageResizeDimension / originalTexture.width;
            float heightRatio = (float)imageResizeDimension / originalTexture.height;
            ratio = Mathf.Min(widthRatio, heightRatio);
        }
        
        int newWidth = Mathf.RoundToInt(originalTexture.width * ratio);
        int newHeight = Mathf.RoundToInt(originalTexture.height * ratio);

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(originalTexture, rt);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(originalTexture);

        _pendingImageBytes = resizedTexture.EncodeToPNG();
        Destroy(resizedTexture);

        _pendingTextFileContent = null;
        _pendingTextFileName = null;

        yield return null; // 한 프레임 대기 후 UI 업데이트
        ShowAttachmentPreview(fileName, _pendingImageBytes);
    }

    #endregion

    #region Chat Bubble Creation & UI Manipulation

    public void AddChatBubble(string text, bool isUser, CharacterPreset speaker = null)
    {
        // 스크롤 위치를 먼저 확인하여 자동 스크롤 여부 결정
        if (scrollRect != null)
        {
            shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        }
        
        GameObject bubbleInstance;
        string processedText = InsertZeroWidthSpaces(text); // 긴 영단어/URL 줄바꿈 처리

        if (!isUser && speaker == null) // 시스템 메시지
        {
            bubbleInstance = Instantiate(systemBubblePrefab, chatContent);
            TMP_Text messageText = bubbleInstance.GetComponentInChildren<TMP_Text>();
            if (messageText != null) messageText.text = processedText;
        }
        else if (isUser) // 사용자 메시지
        {
            bubbleInstance = Instantiate(userBubblePrefab, chatContent);
            TMP_Text messageText = bubbleInstance.GetComponentInChildren<TMP_Text>();
            if (messageText != null) messageText.text = processedText;
        }
        else // AI 메시지
        {
            bubbleInstance = Instantiate(aiBubblePrefab, chatContent);
            AIBubble bubbleScript = bubbleInstance.GetComponent<AIBubble>();
            if (bubbleScript != null && speaker != null)
            {
                bubbleScript.Initialize(speaker.characterImage.sprite, GetLocalizedCharacterName(speaker), processedText);
            }
        }
        
        StartCoroutine(FinalizeLayoutAfterCreation());
    }
    
    public void AddImageBubble(byte[] imageBytes)
    {
        if (scrollRect != null) shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        
        GameObject bubbleInstance = Instantiate(userImageBubblePrefab, chatContent);
        Image contentImage = bubbleInstance.transform.Find("box/ContentImage")?.GetComponent<Image>();
        if (contentImage != null)
        {
            // 이미지 설정 및 크기 조절 로직 (원본과 동일)
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            contentImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            LayoutElement layoutElement = contentImage.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                float aspectRatio = (float)tex.width / tex.height;
                layoutElement.preferredWidth = 350f;
                layoutElement.preferredHeight = Mathf.Clamp(350f / aspectRatio, 100f, 500f);
            }
        }
        StartCoroutine(FinalizeLayoutAfterCreation());
    }

    public void AddFileBubble(string fileName, long fileSize)
    {
        if (scrollRect != null) shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;

        GameObject bubbleInstance = Instantiate(userFileBubblePrefab, chatContent);
        TMP_Text fileInfoText = bubbleInstance.GetComponentInChildren<TMP_Text>();
        if (fileInfoText != null)
        {
            string sizeStr = (fileSize > 1024 * 1024) 
                ? $"{fileSize / (1024.0 * 1024.0):F2} MB" 
                : $"{fileSize / 1024.0:F2} KB";
            fileInfoText.text = $"{fileName}\n({sizeStr})";
        }
        StartCoroutine(FinalizeLayoutAfterCreation());
    }

    private IEnumerator FinalizeLayoutAfterCreation()
    {
        // UI 요소가 생성된 후 레이아웃을 강제로 재계산하고 스크롤을 조정합니다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent.GetComponent<RectTransform>());
        yield return null; 
        if (scrollRect != null && shouldAutoScroll)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
        CleanupOldMessages();
    }
    
    private void CleanupOldMessages()
    {
        if (maxMessagesToKeep <= 0) return;
        
        // 타이핑 인디케이터를 제외한 실제 메시지 버블 수 계산
        int messageCount = chatContent.childCount;
        if (_currentTypingIndicator != null) messageCount--;
        
        // 유지할 최대 메시지 수를 초과하면 가장 오래된 것부터 삭제
        while (messageCount > maxMessagesToKeep)
        {
            Transform childToRemove = chatContent.GetChild(0);
            if(childToRemove.gameObject == _currentTypingIndicator)
            {
                childToRemove = chatContent.GetChild(1);
            }
            Destroy(childToRemove.gameObject);
            messageCount--;
        }
    }

    #endregion

    #region Typing Indicator
    
    public void ShowTypingIndicator(CharacterPreset speaker)
    {
        if (typingIndicatorPrefab == null) return;
        HideTypingIndicator(); // 기존 인디케이터가 있다면 제거

        _currentTypingIndicator = Instantiate(typingIndicatorPrefab, chatContent);
        AIBubble bubbleScript = _currentTypingIndicator.GetComponent<AIBubble>();
        
        if (bubbleScript != null && speaker != null)
        {
            bubbleScript.Initialize(speaker.characterImage.sprite, GetLocalizedCharacterName(speaker), "");
            _typingAnimationCoroutine = StartCoroutine(AnimateTypingIndicatorRoutine(bubbleScript));
        }
        
        StartCoroutine(FinalizeLayoutAfterOneFrame()); // 레이아웃 즉시 업데이트
    }

    public void HideTypingIndicator()
    {
        if (_typingAnimationCoroutine != null)
        {
            StopCoroutine(_typingAnimationCoroutine);
            _typingAnimationCoroutine = null;
        }
        if (_currentTypingIndicator != null)
        {
            Destroy(_currentTypingIndicator);
            _currentTypingIndicator = null;
        }
    }

    private IEnumerator AnimateTypingIndicatorRoutine(AIBubble indicatorBubble)
    {
        while (true)
        {
            indicatorBubble.SetMessage("·");
            yield return new WaitForSeconds(0.5f);
            indicatorBubble.SetMessage("··");
            yield return new WaitForSeconds(0.5f);
            indicatorBubble.SetMessage("···");
            yield return new WaitForSeconds(0.5f);
        }
    }

    // 타이핑 인디케이터처럼 즉각적인 레이아웃 업데이트가 필요할 때 사용
    private IEnumerator FinalizeLayoutAfterOneFrame()
    {
        yield return new WaitForEndOfFrame();
        if (chatContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent.GetComponent<RectTransform>());
        yield return null;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    #endregion

    #region Database Sync & UI State
    
    public void RefreshFromDatabase()
    {
        if (string.IsNullOrEmpty(OwnerID) || _isRefreshing) return;
        
        _isRefreshing = true;
        HideTypingIndicator();
        ClearChatDisplayInternal(); // 기존 버블 모두 제거

        StartCoroutine(RefreshRoutine());
    }

    private IEnumerator RefreshRoutine()
    {
        yield return null; // 한 프레임 대기하여 UI 파괴가 반영되도록 함

        int fetchLimit = (maxMessagesToKeep > 0) ? maxMessagesToKeep : 100;
        List<ChatDatabase.ChatMessage> messages;
        
        if(isGroupChat)
            messages = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, fetchLimit);
        else
            messages = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, fetchLimit);
        
        foreach (var messageRecord in messages)
        {
            DisplayMessageFromRecord(messageRecord);
        }

        yield return StartCoroutine(FinalizeLayoutAfterCreation());
        _isRefreshing = false;
    }

    private void HandlePersonalMessageAdded(string updatedPresetId)
    {
        // 이 채팅창이 해당 1:1 채팅이 아니면 무시
        if (isGroupChat || this.OwnerID != updatedPresetId) return;

        // 사용자가 보낸 메시지는 TrySendMessage에서 이미 UI에 추가했으므로 무시
        var lastMsg = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 1).FirstOrDefault();
        if (lastMsg != null && lastMsg.SenderID == "user") return;

        // DB에서 최신 메시지 하나를 가져와서 UI에 추가
        AppendNewMessage();
    }
    
    private void HandleGroupMessageAdded(string updatedGroupId)
    {
        // 이 채팅창이 해당 그룹 채팅이 아니면 무시
        if (!isGroupChat || this.OwnerID != updatedGroupId) return;

        // 사용자가 보낸 메시지는 이미 UI에 추가했으므로 무시
        var lastMsg = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 1).FirstOrDefault();
        if (lastMsg != null && lastMsg.SenderID == "user") return;

        AppendNewMessage();
    }

    private void AppendNewMessage()
    {
        // 마지막으로 표시한 메시지 ID보다 새로운 메시지가 있는지 확인하고 추가
        var msgs = isGroupChat 
            ? ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 1)
            : ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 1);
        
        if (msgs.Count == 0) return;
        var newMsg = msgs[0];
        int lastId = isGroupChat ? _lastGroupMessageId : _lastPersonalMessageId;

        if (newMsg.Id > lastId)
        {
            DisplayMessageFromRecord(newMsg);
            if (isGroupChat) _lastGroupMessageId = newMsg.Id;
            else _lastPersonalMessageId = newMsg.Id;
        }
    }
    
    /// <summary>
    /// ChatMessage 레코드 하나를 받아 적절한 버블을 생성하는 헬퍼 함수.
    /// </summary>
    private void DisplayMessageFromRecord(ChatDatabase.ChatMessage messageRecord)
    {
        bool isUser = messageRecord.SenderID.ToLower() == "user";
        var data = JsonUtility.FromJson<MessageData>(messageRecord.Message);
        
        CharacterPreset speaker = null;
        if (!isUser)
        {
            speaker = isGroupChat 
                ? CharacterPresetManager.Instance.GetPreset(messageRecord.SenderID) 
                : CharacterPresetManager.Instance.GetPreset(this.OwnerID);
            
            if (audioSource != null && aiMessageSound != null && UserData.Instance != null && canvasGroup.alpha > 0)
                audioSource.PlayOneShot(aiMessageSound, UserData.Instance.SystemVolume);
        }

        if (messageRecord.SenderID.ToLower() == "system")
        {
            AddChatBubble(data.textContent, false, null);
        }
        else
        {
            if (data.type == "image" && isUser) AddImageBubble(System.Convert.FromBase64String(data.fileContent));
            else if (data.type == "text" && data.fileSize > 0 && isUser) AddFileBubble(data.fileName, data.fileSize);
            if (!string.IsNullOrEmpty(data.textContent)) AddChatBubble(data.textContent, isUser, speaker);
        }
    }
    
    private void ClearChatDisplay()
    {
        ClearChatDisplayInternal();
        StartCoroutine(FinalizeLayoutAfterCreation());
    }
    
    private void ClearChatDisplayInternal()
    {
        if (chatContent == null) return;
        HideTypingIndicator();
        foreach (Transform child in chatContent)
        {
            Destroy(child.gameObject);
        }
    }
    
    public void OnResetBtnClicked()
    {
        if (string.IsNullOrEmpty(OwnerID)) return;
        
        Action onConfirm = () =>
        {
            if (isGroupChat) ChatDatabaseManager.Instance.ClearGroupHistoryAndMemories(OwnerID);
            else ChatDatabaseManager.Instance.ClearMessages(OwnerID);
        };

        LocalizationManager.Instance.ShowConfirmationPopup("Popup_Title_ChatReset", "Popup_Msg_ChatReset", onConfirm);
    }
    
    #endregion
    
    #region Misc Helpers

    public void OnPointerDown(PointerEventData eventData)
    {
        // 1:1 채팅창을 클릭하면 알림 아이콘을 끔
        if (!isGroupChat)
        {
            var preset = CharacterPresetManager.Instance.GetPreset(this.OwnerID);
            if (preset != null && preset.notifyImage != null)
            {
                preset.notifyImage.SetActive(false);
            }
        }
    }
    
    public void ShowChatUI(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1 : 0;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private string GetLocalizedCharacterName(CharacterPreset preset)
    {
        if (preset == null) return "Unknown";
        return preset.characterName;
    }

    // 긴 영어 단어나 URL이 말풍선을 뚫고 나가는 것을 방지하기 위해 보이지 않는 공백(ZWSP)을 삽입
    private string InsertZeroWidthSpaces(string originalText)
    {
        if (string.IsNullOrEmpty(originalText)) return originalText;
        const char ZWSP = '\u200B';
        var sb = new StringBuilder();
        foreach (char c in originalText)
        {
            sb.Append(c);
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(ZWSP);
            }
        }
        return sb.ToString();
    }
    
    #endregion
}