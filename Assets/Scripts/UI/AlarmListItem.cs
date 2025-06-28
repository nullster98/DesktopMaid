// --- START OF FILE AlarmListItem.cs ---

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Image 사용을 위해 추가

// 이 컴포넌트가 붙은 게임오브젝트에 Image 컴포넌트가 반드시 있도록 강제합니다.
[RequireComponent(typeof(Image))]
public class AlarmListItem : MonoBehaviour
{
    public AlarmData AlarmData => _alarmData;

    [Header("UI")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image backgroundImage;

    [Header("선택 효과")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.8f, 0.9f, 1f); // 연한 파란색

    private AlarmData _alarmData;
    private AlarmUiController _uiController;

    private void Awake()
    {
        // backgroundImage가 Inspector에서 할당되지 않았을 경우를 대비
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        
        Deselect(); // 기본 색상으로 시작
    }

    public void Init(AlarmData data, AlarmUiController controller)
    {
        _alarmData = data;
        _uiController = controller;
        timeText.text = data.time.ToString(@"hh\:mm");
    }

    public void OnClick()
    {
        _uiController.LoadAlarm(this);
    }
    
    /// <summary>
    /// 이 항목을 선택 상태로 표시합니다.
    /// </summary>
    public void Select()
    {
        backgroundImage.color = selectedColor;
    }

    /// <summary>
    /// 이 항목을 기본 상태로 되돌립니다.
    /// </summary>
    public void Deselect()
    {
        backgroundImage.color = defaultColor;
    }
}