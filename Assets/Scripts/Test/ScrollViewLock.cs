using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewLock : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RectTransform content;
    public RectTransform viewport;

    public void RefreshScroll()
    {
        float margin = 30f; // 여유 폭
        bool shouldEnableScroll = (content.rect.width + margin) > viewport.rect.width;
        scrollRect.horizontal = shouldEnableScroll;
        
        Debug.Log($"Content: {content.rect.width}, Viewport: {viewport.rect.width}, Diff: {content.rect.width - viewport.rect.width}");
    }
}
