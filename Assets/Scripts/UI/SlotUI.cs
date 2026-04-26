using UnityEngine;
using UnityEngine.UI;

public sealed class SlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text keyBindText;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite placeholderIcon;
    [SerializeField] private Color equippedIconColor = Color.white;
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color equippedBackgroundColor = new Color(0.09f, 0.19f, 0.28f, 0.95f);
    [SerializeField] private Color emptyBackgroundColor = new Color(0.05f, 0.1f, 0.14f, 0.55f);

    public void AssignReferences(Image icon, Text keyBind, Image cooldown, Image background)
    {
        iconImage = icon;
        keyBindText = keyBind;
        cooldownOverlay = cooldown;
        backgroundImage = background;
    }

    public void Setup(Sprite icon, string keyBind)
    {
        bool hasItem = icon != null;

        if (iconImage != null)
        {
            iconImage.sprite = hasItem ? icon : placeholderIcon;
            iconImage.color = hasItem ? equippedIconColor : emptyIconColor;
            iconImage.enabled = iconImage.sprite != null;
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
