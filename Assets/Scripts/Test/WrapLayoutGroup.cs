using UnityEngine;

public class WrapLayoutGroup : MonoBehaviour
{
    public float itemWidth = 50f;
    public float itemHeight = 50f;
    public float spacingX = 10f;
    public float spacingY = 10f;

    private RectTransform rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        UpdateLayout();
    }

    private void OnTransformChildrenChanged()
    {
        UpdateLayout();
    }

    public void UpdateLayout()
    {
        float width = rect.rect.width;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + spacingX) / (itemWidth + spacingX)));

        for (int i = 0; i < rect.childCount; i++)
        {
            RectTransform child = rect.GetChild(i) as RectTransform;
            if (child == null) continue;

            int row = i / columns;
            int col = i % columns;

            float x = col * (itemWidth + spacingX);
            float y = -row * (itemHeight + spacingY);

            child.anchoredPosition = new Vector2(x, y);
        }

        float totalHeight = Mathf.Ceil((float)rect.childCount / columns) * (itemHeight + spacingY);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, totalHeight);
    }
}