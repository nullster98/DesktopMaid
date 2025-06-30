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
    private bool _isRefreshing = false;

    private AudioSource audioSource;
    // --- 내부 상태 관리 변수 ---
    private byte[] _pendingImageBytes = null;
    private string _pendingTextFileContent = null;
    private string _pendingTextFileName = null;

    private bool isGroupChat = false;
    public string OwnerID { get; private set; } // 현재 채팅창의 주인 (presetID 또는 groupID)

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

            // ChatFunction에게는 AI 반응만 요청합니다.
            if (!string.IsNullOrEmpty(userText) || fileContent != null)
            {
                geminiChat.OnUserSentMessage(OwnerID, userText, fileContent, fileType, fileName, fileSize);
            }
        }
        else
        {
            var userMessageData = new MessageData { textContent = userText, fileContent = fileContent, type = fileType, fileName = fileName, fileSize = fileSize };
            ChatDatabaseManager.Instance.InsertMessage(OwnerID, "user", JsonUtility.ToJson(userMessageData));
        
            // ChatFunction에게 AI 반응만 요청합니다.
            geminiChat.SendMessageToGemini(userText, fileContent, fileType, fileName, fileSize);
        }

        inputField.text = "";
        OnRemoveAttachmentClicked();
        inputField.ActivateInputField();
    }

    public void OnGeminiResponse(string response, CharacterPreset speaker)
    {
        bool isRelevantResponse = 
            (!isGroupChat && speaker != null && speaker.presetID == this.OwnerID) ||
            (isGroupChat && speaker != null && speaker.groupID == this.OwnerID);

        // 현재 채팅창과 관련 없는 응답이면 무시합니다.
        if (!isRelevantResponse)
        {
            return;
        }

        // [소리 재생 로직 이동]
        // 오직 새로운 응답을 받았을 때, 그리고 그 응답이 이 채팅창에 해당할 때만 소리를 재생합니다.
        if (audioSource != null && aiMessageSound != null && UserData.Instance != null && canvasGroup.alpha > 0)
        {
            audioSource.PlayOneShot(aiMessageSound, UserData.Instance.SystemVolume);
        }
        
        AddChatBubble(response, false, speaker);
    }

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
    // [수정] 시스템 메시지 또는 @everyone 안내 메시지 처리
    if (!isUser && speaker == null) 
    {
        // @everyone 미입력 안내 메시지 처리
        if (text.Contains("@Everyone"))
        {
            GameObject bubbleInstance = Instantiate(aiBubblePrefab, chatContent);
            AIBubble bubbleScript = bubbleInstance.GetComponent<AIBubble>();
            if (bubbleScript != null)
            {
                // 화자 정보를 "System"으로 하여 AI 버블을 사용
                bubbleScript.Initialize(null, "System", text);
                StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(bubbleScript.GetMessageTextComponent(), 10f, 350f));
            }
        }
        else // 일반적인 시스템 메시지 처리 (멤버 합류/탈퇴 등)
        {
            GameObject systemBubbleInstance = Instantiate(systemBubblePrefab, chatContent);
            TMP_Text messageText = systemBubbleInstance.GetComponentInChildren<TMP_Text>();
            if (messageText != null)
            {
                // 시스템 메시지는 단어 줄 바꿈 처리가 덜 중요하므로 ZWSP 삽입 생략 가능
                messageText.text = text;
            }
            StartCoroutine(FinalizeLayout());
        }
        return; // 시스템 관련 버블을 생성했으므로 함수 종료
    }

    // --- 이하 사용자 또는 AI 버블 생성 로직 (변수 이름 충돌 해결) ---
    GameObject chatBubbleInstance = Instantiate(isUser ? userBubblePrefab : aiBubblePrefab, chatContent);
    string processedText = InsertZeroWidthSpaces(text);

    if (isUser)
    {
        TMP_Text messageText = chatBubbleInstance.GetComponentInChildren<TMP_Text>();
        if (messageText != null)
        {
            messageText.text = processedText;
        }
        StartCoroutine(AdjustBubbleSizeAndFinalizeLayout(messageText, 10f, 350f));
    }
    else // AI 버블
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
        yield return null; // 1 프레임 대기하여 UI 요소가 초기화될 시간을 줍니다.

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
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
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
        if (string.IsNullOrEmpty(OwnerID))
        {
            return;
        }

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

        var messages = ChatDatabaseManager.Instance.GetRecentGroupMessages(OwnerID, 100);
        foreach (var messageRecord in messages)
        {
            string senderId = messageRecord.SenderID;
            string jsonContent = messageRecord.Message;
            
            // [수정] senderId가 "system"인 경우를 먼저 처리합니다.
            if (senderId.ToLower() == "system")
            {
                try
                {
                    MessageData data = JsonUtility.FromJson<MessageData>(jsonContent);
                    if (!string.IsNullOrEmpty(data.textContent))
                    {
                        // isUser=false, speaker=null로 AddChatBubble을 호출하면 시스템 버블이 생성됩니다.
                        AddChatBubble(data.textContent, false, null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"시스템 메시지 복원 중 오류: {e.Message}");
                }
                continue; // 시스템 메시지 처리가 끝났으므로 다음 메시지로 넘어갑니다.
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

        var messages = ChatDatabaseManager.Instance.GetRecentMessages(OwnerID, 100);
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
        // 1. 이 채팅창이 그룹 채팅창이 아니거나,
        // 2. 이벤트가 발생한 그룹ID가 내가 담당하는 그룹ID와 다르면 무시
        if (!isGroupChat || this.OwnerID != updatedGroupId)
        {
            return;
        }

        // 3. 내 그룹에 대한 이벤트가 맞다면, 채팅 목록을 새로고침
        //    UI 스레드에서 실행되도록 코루틴을 사용하거나, 바로 호출할 수 있습니다.
        //    이미 새로고침 중일 때 중복 실행을 막는 _isRefreshing 플래그를 존중해야 합니다.
        if (!_isRefreshing)
        {
            Debug.Log($"[ChatUI] 실시간 업데이트 수신: 그룹({updatedGroupId}) 채팅 목록을 새로고침합니다.");
            RefreshFromDatabase();
        }
    }
    
    private void HandlePersonalMessageAdded(string updatedPresetId)
    {
        // 이 채팅창이 1:1 채팅창이 아니거나, 
        // 이벤트가 발생한 ID가 내가 담당하는 ID와 다르면 무시
        if (isGroupChat || this.OwnerID != updatedPresetId)
        {
            return;
        }

        if (!_isRefreshing)
        {
            Debug.Log($"[ChatUI] 실시간 업데이트(개인): 프리셋({updatedPresetId}) 채팅 새로고침.");
            RefreshFromDatabase();
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