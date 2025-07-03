// --- START OF FILE VrmToggleIconHandler.cs ---

using UnityEngine;
using UnityEngine.EventSystems;

public class VrmToggleIconHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("캐릭터 이미지 위에 마우스를 올렸을 때 표시될 VRM 토글 아이콘 (눈 모양 아이콘 등)")]
    public GameObject vrmToggleIcon;

    private void Start()
    {
        // 시작할 때 아이콘을 비활성화 상태로 초기화
        if (vrmToggleIcon != null)
        {
            vrmToggleIcon.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스가 캐릭터 이미지 위로 들어오면 아이콘을 활성화
        if (vrmToggleIcon != null)
        {
            vrmToggleIcon.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스가 캐릭터 이미지 밖으로 나가면 아이콘을 비활성화
        if (vrmToggleIcon != null)
        {
            vrmToggleIcon.SetActive(false);
        }
    }
}