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

public class ChatUI : MonoBehaviour, IPointerDownHandler
{
    #region 변수 및 컴포넌트 참조

    [Header("채팅 대상 ID")]
    [Tooltip("1:1 채팅 시 사용되는 레거시 ID. 이제 ownerID로 통합 관리됩니다.")]
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
    public GameObject systemBubblePrefab; // [추가] 시스템 메시지용 버블 프리팹
    
    [Header("사운드")]
    [SerializeField] private AudioClip aiMessageSound;

    [Header("기능 참조")]
    public ScrollRect scrollRect;
    public ChatFunction geminiChat;

    [Header("파일 첨부 설정")]
    [Tooltip("첨부 파일의 최대 용량 (MB 단위)")]
    public float maxFileSizeMB = 4.0f;
    [Tooltip("용량 초과 시 리사이징될 이미지의 최대 가로/세로 크기 (픽셀)")]
    public int imageResizeDimension = 1920;

    [Header("파일 첨부 미리보기")]
    public GameObject attachmentPreviewPanel;
    public Image attachmentIcon;
    public TMP_Text attachmentFileName;
    public Button removeAttachmentButton;
    public Sprite textFileIcon;

    [Header("UI 제어")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] [Tooltip("화면에 유지할 최대 메시지 개수. 0 이하는 무제한입니다.")] 
    private int maxMessagesToKeep = 100;
    private bool _isRefreshing = false;

    private AudioSource audioSource;
    // --- 내부 상태 관리 변수 ---
    private byte[] _pendingImageBytes = null;
    private string _pendingTextFileContent = null;
    private string _pendingTextFileName = null;

    private bool isGroupChat = false;
    public string OwnerID { get; private set; } // 현재 채팅창의 주인 (presetID 또는 groupID)
    
    private bool _isInitialPersonalLoad = true;
    private int _lastPersonalMessageId = 0;
    private bool _isInitialGroupLoad = true;
    private int _lastGroupMessageId = 0;
    private float scrollThreshold = 0.01f; // 얼마나 바닥에 가까워야 바닥으로 인식할지
    private bool shouldAutoScroll = true;

    #endregion

    #region Unity 생명주기 및 이벤트 핸들러

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
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
        SaveController.OnLoadComplete += RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded += HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded += HandlePersonalMessageAdded;
    }

    private void OnDisable()
    {
        SaveController.OnLoadComplete -= RefreshFromDatabase;
        ChatDatabaseManager.OnGroupMessageAdded -= HandleGroupMessageAdded;
        ChatDatabaseManager.OnPersonalMessageAdded -= HandlePersonalMessageAdded;
    }

    #endregion

    #region ChatUI 모드 설정

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

    #region 사용자 입력 및 메시지 전송

    private void OnSendButtonClicked()
    {
        TrySendMessage();
    }

    private void OnInputSubmit(string inputText)
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            TrySendMessage();
        }
    }

    private void TrySendMessage()
    {
        string userText = inputField.text.Trim();
        if (string.IsNullOrEmpty(userText) && _pendingImageBytes == null && _pendingTextFileContent == null) return;

        string fileContent = null;
        string fileType = null;
        string fileName = null;
        long fileSize = 0;

        if (_pendingImageBytes != null)
        {
            fileContent = System.Convert.ToBase64String(_pendingImageBytes);
            fileType = "image";
        }
        else if (_pendingTextFileContent != null)
        {
            fileContent = _pendingTextFileContent;
            fileType = "text";
            fileName = _pendingTextFileName;
            fileSize = new UTF8Encoding().GetByteCount(_pendingTextFileContent);
        }

        if (isGroupChat)
        {
            var userMessageData = new MessageData { textContent = userText, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
            ChatDatabaseManager.Instance.InsertGroupMessage(OwnerID, "user", JsonUtility.ToJson(userMessageData));

            if (!string.IsNullOrEmpty(userText) || fileContent != null)
            {
                geminiChat.OnUserSentMessage(OwnerID, userText, fileContent, fileType, fileName, fileSize);
            }
        }
        else
        {
            var userMessageData = new MessageData { textContent = userText, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
            ChatDatabaseManager.Instance.InsertMessage(OwnerID, "user", JsonUtility.ToJson(userMessageData));
        
            geminiChat.SendMessageToGemini(userText, fileContent, fileType, fileName, fileSize);
        }

        inputField.text = "";
        OnRemoveAttachmentClicked();
        inputField.ActivateInputField();
    }
    
    // [삭제] 이 메소드는 더 이상 사용되지 않으므로 삭제합니다.
    // public void OnGeminiResponse(string response, CharacterPreset speaker) { ... }

    #endregion

    #region 파일 첨부 관련 (생략 없음)

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
                if (fileSizeInBytes > maxSizeInBytes)
                {
                    UIManager.instance.TriggerWarning($"텍스트 파일 용량이 너무 큽니다. (최대 {maxFileSizeMB}MB)");
                    return;
                }
                _pendingTextFileContent = File.ReadAllText(path);
                _pendingTextFileName = fileName;
                _pendingImageBytes = null;
                ShowAttachmentPreview(fileName);
            }
            else
            {
                if (fileSizeInBytes > maxSizeInBytes)
                {
                    UIManager.instance.TriggerWarning("이미지 용량이 커서 리사이징합니다. 잠시만 기다려주세요...");
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

        int originalWidth = originalTexture.width;
        int originalHeight = originalTexture.height;
        float ratio = 1.0f;

        if (originalWidth > imageResizeDimension || originalHeight > imageResizeDimension)
        {
            float widthRatio = (float)imageResizeDimension / originalWidth;
            float heightRatio = (float)imageResizeDimension / originalHeight;
            ratio = Mathf.Min(widthRatio, heightRatio);
        }

        int newWidth = Mathf.RoundToInt(originalWidth * ratio);
        int newHeight = Mathf.RoundToInt(originalHeight * ratio);

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

        yield return null;
        ShowAttachmentPreview(fileName, _pendingImageBytes);
    }

    #endregion

    #region 채팅 버블 생성 및 UI 조작 (생략 없음)

    public void AddFileBubble(string fileName, long fileSize)
    {
        if (scrollRect != null)
        {
            shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        }
        GameObject bubbleInstance = Instantiate(userFileBubblePrefab, chatContent);
        TMP_Text fileInfoText = bubbleInstance.GetComponentInChildren<TMP_Text>();
        if (fileInfoText != null)
        {
            string sizeStr;
            if (fileSize > 1024 * 1024)
                sizeStr = $"{fileSize / (1024.0 * 1024.0):F2} MB";
            else
                sizeStr = $"{fileSize / 1024.0:F2} KB";
            fileInfoText.text = $"{fileName}\n({sizeStr})";
        }
        StartCoroutine(FinalizeLayout());
    }

    public void AddImageBubble(byte[] imageBytes)
    {
        if (scrollRect != null)
        {
            shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        }
        GameObject bubbleInstance = Instantiate(userImageBubblePrefab, chatContent);
        Image contentImage = bubbleInstance.transform.Find("box/ContentImage")?.GetComponent<Image>();
        if (contentImage != null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            contentImage.sprite = sprite;
            LayoutElement layoutElement = contentImage.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                float aspectRatio = (float)tex.width / tex.height;
                float preferredWidth = 350f;
                float preferredHeight = preferredWidth / aspectRatio;
                preferredHeight = Mathf.Clamp(preferredHeight, 100f, 500f);
                layoutElement.preferredWidth = preferredWidth;
                layoutElement.preferredHeight = preferredHeight;
            }
        }
        StartCoroutine(FinalizeLayout());
    }

    public void AddChatBubble(string text, bool isUser, CharacterPreset speaker = null)
    {
        if (!isUser && speaker == null) 
        {
            if (text.Contains("@Everyone"))
            {
                GameObject bubbleInstance = Instantiate(aiBubblePrefab, chatContent);
                AIBubble bubbleScript = bubbleInstance.GetComponent<AIBubble>();
                if (bubbleScript != null)
                {
                    bubbleScript.Initialize(null, "System", text);
                    StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(bubbleScript.GetMessageTextComponent(), 10f, 350f));
                }
            }
            else
            {
                GameObject systemBubbleInstance = Instantiate(systemBubblePrefab, chatContent);
                TMP_Text messageText = systemBubbleInstance.GetComponentInChildren<TMP_Text>();
                if (messageText != null)
                {
                    messageText.text = text;
                }
                StartCoroutine(FinalizeLayout());
            }
            return;
        }
        
        if (scrollRect != null)
        {
            shouldAutoScroll = scrollRect.verticalNormalizedPosition <= scrollThreshold;
        }

        GameObject chatBubbleInstance = Instantiate(isUser ? userBubblePrefab : aiBubblePrefab, chatContent);
        string processedText = InsertZeroWidthSpaces(text);

        if (isUser)
        {
            TMP_Text messageText = chatBubbleInstance.GetComponentInChildren<TMP_Text>();
            if (messageText != null) messageText.text = processedText;
            StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(messageText, 10f, 350f));
        }
        else 
        {
            AIBubble bubbleScript = chatBubbleInstance.GetComponent<AIBubble>();
            if (bubbleScript == null)
            {
                Debug.LogError("aiBubblePrefab에 AIBubble 스크립트가 없습니다!", chatBubbleInstance);
                Destroy(chatBubbleInstance);
                return;
            }

            var preset = speaker;
            if (preset == null && !isGroupChat)
            {
                var manager = FindObjectOfType<CharacterPresetManager>();
                preset = manager?.presets.Find(p => p.presetID == this.OwnerID);
            }
            
            if (preset != null)
            {
                bubbleScript.Initialize(preset.characterImage.sprite, GetLocalizedCharacterName(preset), processedText);
            }
            
            StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(bubbleScript.GetMessageTextComponent(), 10f, 350f));
        }
    }

    private string GetLocalizedCharacterName(CharacterPreset preset)
    {
        if (preset == null) return "Unknown";
        if (!string.IsNullOrEmpty(preset.characterName)) return preset.characterName;

        if (preset.presetID == "DefaultPreset" && !preset.localizedName.IsEmpty)
        {
            return preset.localizedName.GetLocalizedString();
        }

        return "Unknown";
    }

    private IEnumerator AdjustBubbleSizeAndFinalizeLayout(TMP_Text textComponent, float minWidth, float maxWidth)
    {
        if (textComponent == null) yield break;
        yield return null; 

        RectTransform bubbleRect = textComponent.transform.parent.GetComponent<RectTransform>();
        VerticalLayoutGroup layoutGroup = bubbleRect.GetComponent<VerticalLayoutGroup>();
        if (bubbleRect == null || layoutGroup == null) yield break;

        int horizontalPadding = layoutGroup.padding.left + layoutGroup.padding.right;
        float preferredWidth = textComponent.GetPreferredValues().x;
        float finalWidth = Mathf.Clamp(preferredWidth + horizontalPadding, minWidth, maxWidth);
        textComponent.enableWordWrapping = finalWidth >= maxWidth;
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textComponent.rectTransform);
        yield return null;

        int verticalPadding = layoutGroup.padding.top + layoutGroup.padding.bottom;
        float preferredHeight = textComponent.GetPreferredValues().y;
        float finalHeight = preferredHeight + verticalPadding;
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);

        yield return StartCoroutine(FinalizeLayout());
    }

    private IEnumerator FinalizeLayout()
    {
        yield return new WaitForEndOfFrame();
        if (chatContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent.GetComponent<RectTransform>());
        }
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
        while (chatContent.childCount > maxMessagesToKeep)
        {
            Destroy(chatContent.GetChild(0).gameObject);
        }
    }

    private string InsertZeroWidthSpaces(string originalText)
    {
        if (string.IsNullOrEmpty(originalText)) return originalText;
        const char ZWSP = '\u200B';
        var sb = new StringBuilder();
        for (int i = 0; i < originalText.Length; i++)
        {
            char currentChar = originalText[i];
            sb.Append(currentChar);
            if (i < originalText.Length - 1)
            {
                if (char.IsHighSurrogate(currentChar)) continue;
                if (!char.IsWhiteSpace(currentChar) && !char.IsWhiteSpace(originalText[i + 1]))
                {
                    sb.Append(ZWSP);
                }
            }
        }
        return sb.ToString();
    }

    #endregion

    #region 데이터베이스 연동 및 UI 상태 관리 (생략 없음)
    
    public void RefreshFromDatabase()
    {
        if (string.IsNullOrEmpty(OwnerID)) return;
        if (_isRefreshing) return;
        _isRefreshing = true;

        if (isGroupChat)
        {
            StartCoroutine(RefreshGroupRoutine());
        }
        else
        {
            StartCoroutine(RefreshRoutine());
        }
    }

    private IEnumerator RefreshGroupRoutine()
    {
        foreach (Transform child in chatContent) { Destroy(child.gameObject); }
        yield return null;

        int fetchLimit = (maxMessagesToKeep > 0) ? maxMessagesToKeep : 100;
        var messages = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, fetchLimit);

        foreach (var messageRecord in messages)
        {
            string senderId = messageRecord.SenderID;
            string jsonContent = messageRecord.Message;
            
            if (senderId.ToLower() == "system")
            {
                try
                {
                    MessageData data = JsonUtility.FromJson<MessageData>(jsonContent);
                    if (!string.IsNullOrEmpty(data.textContent))
                    {
                        AddChatBubble(data.textContent, false, null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"시스템 메시지 복원 중 오류: {e.Message}");
                }
                continue; 
            }

            bool isUser = senderId.ToLower() == "user";
            CharacterPreset speaker = null;

            if (!isUser)
            {
                speaker = CharacterPresetManager.Instance.presets.Find(p => p.presetID == senderId);
            }

            try
            {
                MessageData data = JsonUtility.FromJson<MessageData>(jsonContent);
                
                if (data.type == "image" && isUser)
                {
                    byte[] imageBytes = System.Convert.FromBase64String(data.fileContent);
                    AddImageBubble(imageBytes);
                }
                else if (data.type == "text" && data.fileSize > 0 && isUser)
                {
                    AddFileBubble(data.fileName, data.fileSize);
                }

                if (!string.IsNullOrEmpty(data.textContent))
                {
                    AddChatBubble(data.textContent, isUser, speaker);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"메시지 복원 중 오류: {e.Message}");
                AddChatBubble(jsonContent, isUser, speaker);
            }
        }

        yield return StartCoroutine(FinalizeLayout());
        _isRefreshing = false;
    }

    private IEnumerator RefreshRoutine()
    {
        foreach (Transform child in chatContent) { Destroy(child.gameObject); }
        yield return null;

        int fetchLimit = (maxMessagesToKeep > 0) ? maxMessagesToKeep : 100;
        var messages = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, fetchLimit);
        
        foreach (var messageRecord in messages)
        {
            string senderId = messageRecord.SenderID;
            string jsonContent = messageRecord.Message;
            bool isUser = senderId == "user";

            CharacterPreset speaker = null;
            if (!isUser)
            {
                speaker = CharacterPresetManager.Instance.presets.Find(p => p.presetID == this.OwnerID);
            }

            try
            {
                MessageData data = JsonUtility.FromJson<MessageData>(jsonContent);

                if (data.type == "image" && isUser)
                {
                    byte[] imageBytes = System.Convert.FromBase64String(data.fileContent);
                    AddImageBubble(imageBytes);
                }
                else if (data.type == "text" && data.fileSize > 0 && isUser)
                {
                    AddFileBubble(data.fileName, data.fileSize);
                }

                if (!string.IsNullOrEmpty(data.textContent))
                {
                    AddChatBubble(data.textContent, isUser, speaker);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"메시지 복원 중 오류: {e.Message}");
                AddChatBubble(jsonContent, isUser, speaker);
            }
        }

        yield return StartCoroutine(FinalizeLayout());
        _isRefreshing = false;
    }
    
    private void HandleGroupMessageAdded(string updatedGroupId)
    {
        if (!isGroupChat || this.OwnerID != updatedGroupId) return;

        if (_isInitialGroupLoad)
        {
            RefreshFromDatabase();
            _isInitialGroupLoad = false;
            var all = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 100);
            if (all.Count > 0) _lastGroupMessageId = all.Last().Id;
        }
        else
        {
            AppendNewGroupMessage();
        }
    }
    
    private void AppendNewGroupMessage()
    {
        var msgs = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 1);
        if (msgs.Count == 0) return;

        var msg = msgs[0];
        if (msg.Id <= _lastGroupMessageId) return;

        DisplayGroupMessage(msg);
        _lastGroupMessageId = msg.Id;
    }
    
    private void DisplayGroupMessage(ChatDatabase.ChatMessage messageRecord)
    {
        var data = JsonUtility.FromJson<MessageData>(messageRecord.Message);
        bool isUser = messageRecord.SenderID == "user";

        // [수정] 사운드 재생 로직 추가 (AI가 보낸 메시지일 경우)
        if (!isUser)
        {
            Debug.Log($"[DisplayGroupMessage] AI({messageRecord.SenderID}) 응답 표시 시도. 사운드 재생.");
            if (audioSource != null && aiMessageSound != null && UserData.Instance != null && canvasGroup.alpha > 0)
            {
                audioSource.PlayOneShot(aiMessageSound, UserData.Instance.SystemVolume);
            }
        }
        
        CharacterPreset speaker = isUser ? null :
            CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == messageRecord.SenderID);

        if (data.type == "image" && isUser)
            AddImageBubble(Convert.FromBase64String(data.fileContent));
        else if (data.type == "text" && data.fileSize > 0 && isUser)
            AddFileBubble(data.fileName, data.fileSize);

        if (!string.IsNullOrEmpty(data.textContent))
            AddChatBubble(data.textContent, isUser, speaker);
    }
    
    private void HandlePersonalMessageAdded(string updatedPresetId)
    {
        if (isGroupChat || this.OwnerID != updatedPresetId) return;

        if (_isInitialPersonalLoad)
        {
            RefreshFromDatabase();
            _isInitialPersonalLoad = false;
            var all = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 100);
            if (all.Count > 0) _lastPersonalMessageId = all.Last().Id;
        }
        else
        {
            AppendNewPersonalMessage();
        }
    }
    
    private void AppendNewPersonalMessage()
    {
        var msgs = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 1);
        if (msgs.Count == 0) return;

        var msg = msgs[0];
        if (msg.Id <= _lastPersonalMessageId) return;

        DisplayMessage(msg);
        _lastPersonalMessageId = msg.Id;
    }
    
    private void DisplayMessage(ChatDatabase.ChatMessage messageRecord)
    {
        var data = JsonUtility.FromJson<MessageData>(messageRecord.Message);
        bool isUser = messageRecord.SenderID == "user";

        // [수정] 사운드 재생 로직 추가 (AI가 보낸 메시지일 경우)
        if (!isUser)
        {
            Debug.Log($"[DisplayMessage] AI({messageRecord.SenderID}) 응답 표시 시도. 사운드 재생.");
            if (audioSource != null && aiMessageSound != null && UserData.Instance != null && canvasGroup.alpha > 0)
            {
                audioSource.PlayOneShot(aiMessageSound, UserData.Instance.SystemVolume);
            }
        }
        
        CharacterPreset speaker = isUser ? null :
            CharacterPresetManager.Instance.presets.FirstOrDefault(p => p.presetID == this.OwnerID);

        if (data.type == "image" && isUser)
        {
            byte[] bytes = Convert.FromBase64String(data.fileContent);
            AddImageBubble(bytes);
        }
        else if (data.type == "text" && data.fileSize > 0 && isUser)
        {
            AddFileBubble(data.fileName, data.fileSize);
        }

        if (!string.IsNullOrEmpty(data.textContent))
        {
            AddChatBubble(data.textContent, isUser, speaker);
        }
    }

    public void OnResetBtnClicked()
    {
        if (string.IsNullOrEmpty(OwnerID)) return;
        
        if (isGroupChat)
        {
            ChatDatabaseManager.Instance.ClearGroupMessages(OwnerID);
        }
        else
        {
            ChatDatabaseManager.Instance.ClearMessages(OwnerID);
        }
        foreach (Transform child in chatContent) { Destroy(child.gameObject); }
    }

    public void ShowChatUI(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1 : 0;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TryDisableNotification();
    }

    private void TryDisableNotification()
    {
        if (isGroupChat) return;

        var manager = FindObjectOfType<CharacterPresetManager>();
        var preset = manager?.presets.Find(p => p.presetID == this.OwnerID);
        if (preset != null && preset.notifyImage != null)
        {
            preset.notifyImage.SetActive(false);
        }
    }

    #endregion
}
// --- END OF FILE ChatUI.cs ---