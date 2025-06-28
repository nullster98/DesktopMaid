using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragController : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private Vector2 offset;
    private RectTransform dragTarget; // 옮길 대상 (ManagementWrapper)

    void Start()
    {
        // 상위 부모 중 RectTransform 가져오기
        dragTarget = transform.parent.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragTarget.SetAsLastSibling();
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragTarget, eventData.position, eventData.pressEventCamera, out offset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 globalMousePos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragTarget.parent as RectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
        {
            dragTarget.localPosition = globalMousePos - offset;
        }
    }
}
