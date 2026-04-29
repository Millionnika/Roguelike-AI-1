using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SlotUI : MonoBehaviour
{
    [Tooltip("Inspector: icon image")]
    [SerializeField] private Image iconImage;
    [Tooltip("Inspector: key bind text")]
    [SerializeField] private TMP_Text keyBindText;
    [Tooltip("Inspector: cooldown overlay")]
    [SerializeField] private Image cooldownOverlay;
    [Tooltip("Inspector: background image")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("Inspector: placeholder icon")]
    [SerializeField] private Sprite placeholderIcon;
    [Tooltip("Inspector: equipped icon color")]
    [SerializeField] private Color equippedIconColor = Color.white;
    [Tooltip("Inspector: empty icon color")]
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("Inspector: equipped background color")]
    [SerializeField] private Color equippedBackgroundColor = new Color(0.09f, 0.19f, 0.28f, 0.95f);
    [Tooltip("Inspector: empty background color")]
    [SerializeField] private Color emptyBackgroundColor = new Color(0.05f, 0.1f, 0.14f, 0.55f);

    private void Awake()
    {
        EnsureTextReference();
    }

    public void AssignReferences(Image icon, TMP_Text keyBind, Image cooldown, Image background)
    {
        iconImage = icon;
        keyBindText = keyBind;
        cooldownOverlay = cooldown;
        backgroundImage = background;
    }

    public void Setup(Sprite icon, string keyBind)
    {
        Setup(icon, keyBind, icon != null);
    }

    public void Setup(Sprite icon, string keyBind, bool hasItem)
    {
        EnsureTextReference();

        if (iconImage != null)
        {
            iconImage.sprite = hasItem ? icon : placeholderIcon;
            iconImage.color = hasItem ? equippedIconColor : emptyIconColor;
            iconImage.enabled = hasItem || iconImage.sprite != null;
        }

        if (keyBindText != null)
        {
            keyBindText.text = keyBind ?? string.Empty;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = hasItem ? equippedBackgroundColor : emptyBackgroundColor;
        }

        SetCooldown01(0f);
    }

    private void EnsureTextReference()
    {
        if (keyBindText != null)
        {
            return;
        }

        keyBindText = GetComponentInChildren<TMP_Text>(true);
        if (keyBindText != null)
        {
            return;
        }

        GameObject keyObject = new GameObject("KeyBindTMP", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyObject.transform.SetParent(transform, false);
        RectTransform keyRect = keyObject.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -4f);
        keyRect.sizeDelta = new Vector2(0f, 18f);

        keyBindText = keyObject.GetComponent<TMP_Text>();
        keyBindText.fontSize = 12f;
        keyBindText.alignment = TextAlignmentOptions.Center;
        keyBindText.color = Color.white;
        keyBindText.raycastTarget = false;
    }

    public void SetCooldown01(float cooldown01)
    {
        if (cooldownOverlay == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(cooldown01);
        bool isVisible = normalized > 0.001f;
        cooldownOverlay.gameObject.SetActive(isVisible);
        if (!isVisible)
        {
            return;
        }

        if (cooldownOverlay.type == Image.Type.Filled)
        {
            cooldownOverlay.fillAmount = normalized;
            return;
        }

        Color overlayColor = cooldownOverlay.color;
        overlayColor.a = 0.75f * normalized;
        cooldownOverlay.color = overlayColor;
    }
}
