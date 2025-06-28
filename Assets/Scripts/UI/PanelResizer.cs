using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스 드래그 이벤트를 감지하기 위해 여러 인터페이스를 상속받습니다.
public class PanelResizer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("크기를 조절할 대상 패널의 Layout Element")]
    public LayoutElement targetLayoutElement;

    [Tooltip("조절 가능한 최소/최대 너비")]
    public float minWidth = 200f;
    public float maxWidth = 600f;

    [Tooltip("좌우 크기 조절 커서 이미지 (선택 사항)")]
    public Texture2D resizeCursorTexture;

    // 드래그가 시작될 때 호출됩니다. (지금은 특별히 할 일 없음)
    public void OnPointerDown(PointerEventData eventData)
    {
        // Debug.Log("Resize Handle Clicked!");
    }

    // 마우스를 드래그하는 동안 매 프레임 호출됩니다.
    public void OnDrag(PointerEventData eventData)
    {
        if (targetLayoutElement == null) return;

        // 현재 너비에 마우스의 수평 이동량(eventData.delta.x)을 더합니다.
        float newWidth = targetLayoutElement.preferredWidth + eventData.delta.x;

        // 계산된 너비가 최소/최대 너비 범위 안에 있도록 제한합니다.
        newWidth = Mathf.Clamp(newWidth, minWidth, maxWidth);

        // 최종적으로 계산된 너비를 Layout Element에 적용합니다.
        targetLayoutElement.preferredWidth = newWidth;
    }

    // 마우스가 핸들 위로 올라왔을 때 호출됩니다. (커서 모양 변경용)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (resizeCursorTexture != null)
        {
            Vector2 hotspot = new Vector2(16, 4); // ← 여기 수동 조정 필요

            Cursor.SetCursor(resizeCursorTexture, hotspot, CursorMode.Auto);
        }
    }

    // 마우스가 핸들 밖으로 나갔을 때 호출됩니다. (커서 모양 복원용)
    public void OnPointerExit(PointerEventData eventData)
    {
        // 커서를 기본 모양으로 되돌림
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}