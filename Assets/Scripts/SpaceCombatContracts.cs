using UnityEngine;
using UnityEngine.UI;

internal interface IPlatformService
{
    DevicePlatformFamily CurrentFamily { get; }
    bool IsDesktopLike();
    bool ShouldUseVirtualJoystick();
}

internal interface ILocalizationService
{
    string Localize(string key, bool ru);
    string GetShipRoleText(ShipDefinition ship, bool ru);
    string GetShipDescriptionText(ShipDefinition ship, bool ru);
}

internal interface ISpaceCombatUiFactory
{
    Image CreateImage(string objectName, Transform parent, Sprite squareSprite, Color color);
    Text CreateText(string objectName, Transform parent, Font uiFont, string content, int fontSize, FontStyle fontStyle, Color color);
    void AddOutline(GameObject target, Color color);
    Image CreateBar(Transform parent, Sprite squareSprite, Vector2 anchoredPosition, Color fillColor);
    Image CreateLabeledBar(Transform parent, Sprite squareSprite, Font uiFont, string label, Vector2 anchoredPosition, Color fillColor);
    void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax);
    void SetFillWidth(RectTransform rect, float percent, float maxWidth);
    void SetFillHeight(RectTransform rect, float percent, float maxHeight);
}
