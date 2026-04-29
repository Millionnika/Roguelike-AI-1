using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class SpaceCombatUiFactory : ISpaceCombatUiFactory
{
    public Image CreateImage(string objectName, Transform parent, Sprite squareSprite, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = squareSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        return image;
    }

    public RawImage CreateRawImage(string objectName, Transform parent, Texture texture, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        return image;
    }

    public TMP_Text CreateText(string objectName, Transform parent, Font uiFont, string content, int fontSize, FontStyle fontStyle, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public void AddOutline(GameObject target, Color color)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    public Image CreateBar(Transform parent, Sprite squareSprite, Vector2 anchoredPosition, Color fillColor)
    {
        Image background = CreateImage("BarBackground", parent, squareSprite, new Color(0.1f, 0.16f, 0.2f, 1f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 1f);
        backgroundRect.anchorMax = new Vector2(0f, 1f);
        backgroundRect.pivot = new Vector2(0f, 1f);
        backgroundRect.sizeDelta = new Vector2(252f, 10f);
        backgroundRect.anchoredPosition = anchoredPosition;

        Image fill = CreateImage("Fill", background.transform, squareSprite, fillColor);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(252f, 0f);
        fillRect.anchoredPosition = Vector2.zero;
        return fill;
    }

    public Image CreateLabeledBar(Transform parent, Sprite squareSprite, Font uiFont, string label, Vector2 anchoredPosition, Color fillColor)
    {
        TMP_Text text = CreateText(label + "Label", parent, uiFont, label, 13, FontStyle.Bold, new Color(0.88f, 0.94f, 1f));
        SetAnchoredRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, anchoredPosition + new Vector2(52f, -16f));

        Image background = CreateImage(label + "Bg", parent, squareSprite, new Color(0.12f, 0.17f, 0.2f, 1f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 1f);
        backgroundRect.anchorMax = new Vector2(0f, 1f);
        backgroundRect.pivot = new Vector2(0f, 1f);
        backgroundRect.sizeDelta = new Vector2(180f, 12f);
        backgroundRect.anchoredPosition = anchoredPosition + new Vector2(64f, 0f);

        Image fill = CreateImage(label + "Fill", background.transform, squareSprite, fillColor);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(180f, 0f);
        fillRect.anchoredPosition = Vector2.zero;
        return fill;
    }

    public void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        bool isTopAnchored = Mathf.Approximately(anchorMin.y, anchorMax.y) && Mathf.Approximately(anchorMax.y, 1f);
        rect.offsetMin = new Vector2(offsetMin.x, isTopAnchored ? offsetMax.y : offsetMin.y);
        rect.offsetMax = new Vector2(offsetMax.x, isTopAnchored ? offsetMin.y : offsetMax.y);
    }

    public void SetFillWidth(RectTransform rect, float percent, float maxWidth)
    {
        rect.sizeDelta = new Vector2(Mathf.Clamp01(percent) * maxWidth, rect.sizeDelta.y);
    }

    public void SetFillHeight(RectTransform rect, float percent, float maxHeight)
    {
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Clamp01(percent) * maxHeight);
    }
}
