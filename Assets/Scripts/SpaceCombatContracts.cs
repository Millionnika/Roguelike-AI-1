using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal interface IPlatformService
{
    DevicePlatformFamily CurrentFamily { get; }
    bool IsDesktopLike();
    bool ShouldUseVirtualJoystick();
}

internal interface ICombatService
{
    CombatUpdateResult UpdateFrame(CombatUpdateContext context, float deltaTime);
    void ApplyDamage(SpaceFrontier.Player.PlayerStats stats, float amount);
    bool ApplyDamage(EnemyShip enemy, float amount);
    void SetDefaultWeaponData(WeaponDataSO weaponData);
}

internal interface IInputService
{
    PointerInputState ReadPointerState();
}

internal interface IMovementService
{
    void UpdateMovement(SpaceFrontier.Player.PlayerShip player, MovementUpdateContext context, MovementSettingsSO settings, float deltaTime);
}

internal interface IPoolService
{
    GameObject Get(GameObject prefab, Transform parent);
    void Return(GameObject prefab, GameObject instance);
    void InitializePool(GameObject prefab, int initialCount);
}

internal interface ILocalizationService
{
    string Localize(string key, bool ru);
    string GetShipRoleText(ShipDataSO ship, bool ru);
    string GetShipDescriptionText(ShipDataSO ship, bool ru);
}

internal interface IBackgroundParallaxService
{
    void Initialize(Transform parent, List<BackgroundLayerConfig> layers, IPoolService poolService);
    void Update(Vector3 focusPosition);
    void Dispose();
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
