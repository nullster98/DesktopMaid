using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;

public class ResizeUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public List<RectTransform> targetRects; // 크기 조절할 대상들
    public float minHeight;
    public float maxHeight = 1000f;

    private float originalHeight;
    private Vector2 originalMousePos;

    public Texture2D verticalCursorTexture;
    
    [Header("함께 이동할 자식 오브젝트 (예: ResizeHandle, SettingBtn 등)")]
    public List<RectTransform> moveTargets;
    // 자식들의 원래 위치 저장
    private Dictionary<RectTransform, Vector2> originalChildPositions = new();

    private void Start()
    {
        if (targetRects == null || targetRects.Count == 0) return;

        // 현재 Height를 minHeight로 설정
        minHeight = targetRects[0].sizeDelta.y;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRects[0], eventData.position, eventData.pressEventCamera, out originalMousePos
        );
        originalHeight = targetRects[0].sizeDelta.y;
        
        // 자식 위치 저장
        originalChildPositions.Clear();
        foreach (var child in moveTargets)
        {
            originalChildPositions[child] = child.anchoredPosition;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Cursor.SetCursor(verticalCursorTexture,
            new Vector2(verticalCursorTexture.width / 2, verticalCursorTexture.height / 2),
            CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localMousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRects[0], eventData.position, eventData.pressEventCamera, out localMousePos
        );

        float offsetY = originalMousePos.y - localMousePos.y; // 아래로 드래그 시 offsetY가 양수
        float newHeight = Mathf.Clamp(originalHeight + offsetY, minHeight, maxHeight);
        float delta = newHeight - originalHeight;

        foreach (var rect in targetRects)
        {
            Vector2 size = rect.sizeDelta;
            size.y = newHeight;
            rect.sizeDelta = size;
        }
        
        // 자식 오브젝트 위치 정확히 보정
        foreach (var child in moveTargets)
        {
            if (child != null && originalChildPositions.ContainsKey(child))
            {
                Vector2 basePos = originalChildPositions[child];
                basePos.y -= delta;
                child.anchoredPosition = basePos;
            }
        }
    }
}