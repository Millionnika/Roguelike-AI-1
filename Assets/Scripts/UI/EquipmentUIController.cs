using System.Collections.Generic;
using UnityEngine;

public sealed class EquipmentUIController : MonoBehaviour
{
    [Header("Scene Binding")]
    [SerializeField] private SpaceCombatSceneController sceneController;

    [Header("UI References")]
    [SerializeField] private SlotUI slotPrefab;
    [SerializeField] private Transform weaponSlotsContainer;
    [SerializeField] private Transform moduleSlotsContainer;

    private readonly List<SlotUI> weaponSlots = new List<SlotUI>();
    private readonly List<SlotUI> moduleSlots = new List<SlotUI>();
    private bool subscribed;

    private void OnEnable()
    {
        Bind(sceneController);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(SpaceCombatSceneController controller)
    {
        if (sceneController == controller && subscribed)
        {
            return;
        }

        Unsubscribe();
        sceneController = controller;
        if (sceneController == null)
        {
            return;
        }

        sceneController.EquipmentStateChanged += Refresh;
        subscribed = true;
        Refresh(sceneController.CurrentEquipmentState);
    }

    public void Refresh(ShipEquipmentState state)
    {
        if (!HasUiReferences())
        {
            return;
        }

        int weaponCount = state != null ? state.InstalledWeapons.Count : 0;
        int moduleCount = state != null ? state.InstalledModules.Count : 0;

        EnsureSlotCount(weaponSlots, weaponSlotsContainer, weaponCount, "WeaponSlot");
        EnsureSlotCount(moduleSlots, moduleSlotsContainer, moduleCount, "ModuleSlot");

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            WeaponDataSO weapon = state != null ? state.InstalledWeapons[i] : null;
            Sprite icon = weapon != null ? weapon.icon : null;
            weaponSlots[i].Setup(icon, (i + 1).ToString());
        }

        for (int i = 0; i < moduleSlots.Count; i++)
        {
            ModuleDataSO module = state != null ? state.InstalledModules[i] : null;
            Sprite icon = module != null ? module.icon : null;
            moduleSlots[i].Setup(icon, (i + 1).ToString());
        }

        RefreshCooldowns(state);
    }

    public void RefreshCooldowns(ShipEquipmentState state)
    {
        for (int i = 0; i < weaponSlots.Count; i++)
        {
            float cooldown01 = 0f;

            if (state != null && i < state.InstalledWeapons.Count && i < state.WeaponTimers.Count)
            {
                WeaponDataSO weapon = state.InstalledWeapons[i];
                if (weapon != null && weapon.fireRate > 0f)
                {
                    float timer = Mathf.Clamp(state.WeaponTimers[i], 0f, weapon.fireRate);
                    cooldown01 = 1f - timer / weapon.fireRate;
                }
            }

            weaponSlots[i].SetCooldown01(cooldown01);
        }

        for (int i = 0; i < moduleSlots.Count; i++)
        {
            moduleSlots[i].SetCooldown01(0f);
        }
    }

    private void Unsubscribe()
    {
        if (sceneController != null && subscribed)
        {
            sceneController.EquipmentStateChanged -= Refresh;
        }

        subscribed = false;
    }

    private bool HasUiReferences()
    {
        if (slotPrefab == null || weaponSlotsContainer == null || moduleSlotsContainer == null)
        {
            Debug.LogError("EquipmentUIController: assign SlotUI prefab and both containers in inspector.", this);
            return false;
        }

        return true;
    }

    private void EnsureSlotCount(List<SlotUI> slots, Transform container, int targetCount, string prefix)
    {
        while (slots.Count > targetCount)
        {
            int lastIndex = slots.Count - 1;
            if (slots[lastIndex] != null)
            {
                Destroy(slots[lastIndex].gameObject);
            }

            slots.RemoveAt(lastIndex);
        }

        while (slots.Count < targetCount)
        {
            SlotUI slot = Instantiate(slotPrefab, container);
            slot.name = prefix + "_" + (slots.Count + 1);
            slots.Add(slot);
        }
    }
}
