using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// 모든 현지화된 메시지(경고, 확인 창 등)를 중앙에서 관리하고 표시하는 싱글턴 클래스입니다.
/// 다른 스크립트는 이 매니저에게 간단한 string key만 전달하여 메시지를 띄울 수 있습니다.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // 인스펙터에서 Key와 LocalizedString을 연결하기 위한 도우미 클래스
    [System.Serializable]
    public class LocalizedMessageMapping
    {
        public string key;
        public LocalizedString message;
    }

    [Header("메시지 매핑 리스트")]
    [Tooltip("여기에 프로젝트에서 사용할 모든 경고/확인 메시지 키를 등록합니다.")]
    [SerializeField] private List<LocalizedMessageMapping> messageMappings;

    // 빠른 조회를 위해 런타임에 사용할 딕셔너리
    private Dictionary<string, LocalizedString> _messageDictionary = new Dictionary<string, LocalizedString>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 리스트를 딕셔너리로 변환하여 초기화
        foreach (var mapping in messageMappings)
        {
            if (!_messageDictionary.ContainsKey(mapping.key))
            {
                _messageDictionary.Add(mapping.key, mapping.message);
            }
            else
            {
                Debug.LogWarning($"[LocalizationManager] 중복된 키를 감지했습니다: {mapping.key}");
            }
        }
    }

    /// <summary>
    /// 등록된 키에 해당하는 자동 소멸 경고 메시지를 표시합니다.
    /// </summary>
    /// <param name="messageKey">표시할 메시지의 고유 키</param>
    /// <param name="duration">표시 시간 (초)</param>
    public void ShowWarning(string messageKey, float duration = 2.0f)
    {
        if (UIManager.instance == null) return;

        if (_messageDictionary.TryGetValue(messageKey, out LocalizedString localizedString))
        {
            UIManager.instance.TriggerWarning(localizedString, duration);
        }
        else
        {
            Debug.LogError($"[LocalizationManager] '{messageKey}'에 해당하는 메시지 키를 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// Smart String 인자를 포함하는 자동 소멸 경고 메시지를 표시합니다.
    /// </summary>
    public void ShowWarning(string messageKey, IDictionary<string, object> arguments, float duration = 2.0f)
    {
        if (UIManager.instance == null) return;
        if (_messageDictionary.TryGetValue(messageKey, out LocalizedString localizedString))
        {
            UIManager.instance.TriggerWarning(localizedString, arguments, duration);
        }
        else
        {
            Debug.LogError($"[LocalizationManager] '{messageKey}' 키를 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// 등록된 키를 사용하여 현지화된 확인/취소 팝업을 표시합니다.
    /// </summary>
    /// <param name="titleKey">팝업 제목의 고유 키</param>
    /// <param name="messageKey">팝업 내용의 고유 키</param>
    /// <param name="onConfirm">확인 버튼을 눌렀을 때 실행될 함수</param>
    /// <param name="onCancel">취소 버튼을 눌렀을 때 실행될 함수 (선택 사항)</param>
    /// <param name="messageArguments">내용(message)에 들어갈 Smart String 인자 (선택 사항)</param>
    public void ShowConfirmationPopup(string titleKey, string messageKey, Action onConfirm, Action onCancel = null, IDictionary<string, object> messageArguments = null, float? messageFontSize = null)
    {
        if (UIManager.instance == null) return;

        // 제목과 내용에 대한 LocalizedString을 딕셔너리에서 찾습니다.
        if (_messageDictionary.TryGetValue(titleKey, out LocalizedString localizedTitle) &&
            _messageDictionary.TryGetValue(messageKey, out LocalizedString localizedMessage))
        {
            // UIManager에게 찾은 LocalizedString 객체와 액션을 전달하여 팝업 표시를 위임합니다.
            UIManager.instance.ShowLocalizedConfirmationPopup(localizedTitle, localizedMessage, onConfirm, onCancel, messageArguments, messageFontSize);
        }
        else
        {
            Debug.LogError($"[LocalizationManager] '{titleKey}' 또는 '{messageKey}'에 해당하는 메시지 키를 찾을 수 없습니다!");
        }
    }
}