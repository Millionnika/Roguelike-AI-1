using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SpaceFrontier.Player;

[DisallowMultipleComponent]
public sealed class PlayerModuleController : MonoBehaviour
{
    [Header("Модули игрока")]
    [Tooltip("Панель слотов модулей боевого HUD. Может быть назначена из authored HUD или создана runtime-fallback.")]
    [SerializeField] private RectTransform modulePanelRect;

    private readonly List<ModuleState> modules = new List<ModuleState>();

    private PlayerShip player;
    private Func<string, string> localize;
    private Action<string, string> logMessage;
    private Action<ModuleState> updateModuleVisual;
    private Action refreshEquipmentUi;

    internal List<ModuleState> Modules => modules;
    internal IReadOnlyList<ModuleState> ReadOnlyModules => modules;

    internal void Initialize(
        Func<string, string> localizeCallback,
        Action<string, string> logMessageCallback,
        Action<ModuleState> updateModuleVisualCallback,
        Action refreshEquipmentUiCallback)
    {
        localize = localizeCallback;
        logMessage = logMessageCallback;
        updateModuleVisual = updateModuleVisualCallback;
        refreshEquipmentUi = refreshEquipmentUiCallback;
    }

    public void SetPlayer(PlayerShip newPlayer)
    {
        player = newPlayer;
    }

    public void CreateModules(int moduleSlotCount, ShipDataSO ship)
    {
        modules.Clear();
        int supportedSlots = Mathf.Clamp(Mathf.Max(1, moduleSlotCount), 1, 4);
        WeaponDataSO primaryWeapon = GetPrimaryWeapon(ship);
        float capPerShot = primaryWeapon != null ? primaryWeapon.capacitorPerShot : 0f;
        float rateOfFire = primaryWeapon != null
            ? (primaryWeapon.cooldown > 0f ? primaryWeapon.cooldown : primaryWeapon.fireRate)
            : 1f;
        float damage = primaryWeapon != null ? primaryWeapon.damage : 0f;

        modules.Add(new ModuleState
        {
            Name = "Weapon Group",
            KeyLabel = "1",
            Type = ModuleType.Weapon,
            CapPerShot = capPerShot,
            RateOfFire = rateOfFire,
            Damage = damage,
            OptimalRange = 5.1f,
            FalloffRange = 3.2f,
            WeaponData = primaryWeapon
        });

        if (supportedSlots > 1)
        {
            modules.Add(new ModuleState
            {
                Name = "Shield Rep",
                KeyLabel = "2",
                Type = ModuleType.ShieldRep,
                CapPerSecond = 7f,
                RepairPerSecond = 32f
            });
        }

        if (supportedSlots > 2)
        {
            modules.Add(new ModuleState
            {
                Name = "Armor Rep",
                KeyLabel = "3",
                Type = ModuleType.ArmorRep,
                CapPerSecond = 6f,
                RepairPerSecond = 24f
            });
        }

        if (supportedSlots > 3)
        {
            modules.Add(new ModuleState
            {
                Name = "Afterburn",
                KeyLabel = "4",
                Type = ModuleType.Afterburner,
                CapPerSecond = 5f,
                SpeedBonus = 1.55f
            });
        }

        BindModuleSlots();
    }

    public void ResetModules()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].Active = false;
            modules[i].WeaponTimer = 0f;
            updateModuleVisual?.Invoke(modules[i]);
        }

        refreshEquipmentUi?.Invoke();
    }

    public void ToggleModule(int index)
    {
        if (index < 0 || index >= modules.Count)
        {
            return;
        }

        ModuleState module = modules[index];
        module.Active = !module.Active;

        if (module.Type == ModuleType.Afterburner && !module.Active && player != null)
        {
            player.SpeedMultiplier = 1f;
        }

        updateModuleVisual?.Invoke(module);
        logMessage?.Invoke(module.Name + (module.Active ? Localize("log_module_on") : Localize("log_module_off")), "info");
    }

    public void HandleHotkeys(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) ToggleModule(0);
        if (keyboard.digit2Key.wasPressedThisFrame) ToggleModule(1);
        if (keyboard.digit3Key.wasPressedThisFrame) ToggleModule(2);
        if (keyboard.digit4Key.wasPressedThisFrame) ToggleModule(3);
    }

    public bool TryToggleModuleFromHud(Vector2 screenPosition)
    {
        if (modulePanelRect == null)
        {
            return false;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            Transform slotTransform = modulePanelRect.Find("ModuleSlot_" + i);
            RectTransform slotRect = slotTransform != null ? slotTransform.GetComponent<RectTransform>() : null;
            if (slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, null))
            {
                ToggleModule(i);
                return true;
            }
        }

        return false;
    }

    public void BindModuleSlots(RectTransform panelRect)
    {
        modulePanelRect = panelRect;
        BindModuleSlots();
    }

    public bool IsModulePanelBlocked(Vector2 screenPosition)
    {
        return modulePanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(modulePanelRect, screenPosition, null);
    }

    private void BindModuleSlots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (modulePanelRect == null)
            {
                continue;
            }

            Transform slotTransform = modulePanelRect.Find("ModuleSlot_" + i);
            if (slotTransform == null)
            {
                continue;
            }

            Image slotImage = slotTransform.GetComponent<Image>();
            TMP_Text slotKey = slotTransform.Find("Key") != null ? slotTransform.Find("Key").GetComponent<TMP_Text>() : null;
            TMP_Text slotTitle = slotTransform.Find("Label") != null ? slotTransform.Find("Label").GetComponent<TMP_Text>() : null;

            if (i < modules.Count)
            {
                ModuleState module = modules[i];
                module.SlotImage = slotImage;
                module.SlotKey = slotKey;
                module.SlotTitle = slotTitle;
                if (module.SlotKey != null) module.SlotKey.text = "[" + module.KeyLabel + "]";
                if (module.SlotTitle != null) module.SlotTitle.text = module.Name;
                updateModuleVisual?.Invoke(module);
            }
            else
            {
                if (slotKey != null) slotKey.text = string.Empty;
                if (slotTitle != null) slotTitle.text = string.Empty;
                if (slotImage != null) slotImage.color = new Color(0.05f, 0.1f, 0.14f, 0.45f);
            }
        }
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }

    private static WeaponDataSO GetPrimaryWeapon(ShipDataSO ship)
    {
        if (ship == null || ship.startingWeapons == null)
        {
            return null;
        }

        for (int i = 0; i < ship.startingWeapons.Count; i++)
        {
            if (ship.startingWeapons[i] != null)
            {
                return ship.startingWeapons[i];
            }
        }

        return null;
    }
}
