using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    public void Configure(
        SpaceCombatSceneController controller,
        SlotUI prefab,
        Transform weaponContainer,
        Transform moduleContainer)
    {
        if (prefab != null)
        {
            slotPrefab = prefab;
        }

        if (weaponContainer != null)
        {
            weaponSlotsContainer = weaponContainer;
        }

        if (moduleContainer != null)
        {
            moduleSlotsContainer = moduleContainer;
        }

        Bind(controller);
    }

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
                if (weapon != null)
                {
                    float cooldownDuration = weapon.cooldown > 0f ? weapon.cooldown : weapon.fireRate;
                    if (cooldownDuration > 0f)
                    {
                        float timer = Mathf.Clamp(state.WeaponTimers[i], 0f, cooldownDuration);
                        cooldown01 = timer / cooldownDuration;
                    }
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
        if (weaponSlotsContainer == null || moduleSlotsContainer == null)
        {
            Debug.LogError("EquipmentUIController: assign both containers in inspector.", this);
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
            SlotUI slot = slotPrefab != null
                ? Instantiate(slotPrefab, container)
                : CreateRuntimeSlot(container, prefix + "_" + (slots.Count + 1));
            slot.name = prefix + "_" + (slots.Count + 1);
            slots.Add(slot);
        }
    }

    private static SlotUI CreateRuntimeSlot(Transform parent, string objectName)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(SlotUI));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(72f, 72f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.05f, 0.1f, 0.14f, 0.92f);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 72f;
        layout.preferredHeight = 72f;
        layout.minWidth = 72f;
        layout.minHeight = 72f;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(36f, 36f);
        iconRect.anchoredPosition = new Vector2(0f, 4f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.color = new Color(1f, 1f, 1f, 0.25f);

        GameObject keyGo = new GameObject("KeyBind", typeof(RectTransform), typeof(Text));
        keyGo.transform.SetParent(root.transform, false);
        RectTransform keyRect = keyGo.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -4f);
        keyRect.sizeDelta = new Vector2(0f, 18f);
        Text keyText = keyGo.GetComponent<Text>();
        keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyText.fontSize = 12;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = Color.white;

        GameObject cooldownGo = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
        cooldownGo.transform.SetParent(root.transform, false);
        RectTransform cooldownRect = cooldownGo.GetComponent<RectTransform>();
        cooldownRect.anchorMin = Vector2.zero;
        cooldownRect.anchorMax = Vector2.one;
        cooldownRect.offsetMin = Vector2.zero;
        cooldownRect.offsetMax = Vector2.zero;
        Image cooldownImage = cooldownGo.GetComponent<Image>();
        cooldownImage.color = new Color(0f, 0f, 0f, 0.55f);
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Vertical;
        cooldownImage.fillOrigin = (int)Image.OriginVertical.Top;
        cooldownImage.fillAmount = 0f;
        cooldownGo.SetActive(false);

        SlotUI slotUi = root.GetComponent<SlotUI>();
        slotUi.AssignReferences(iconImage, keyText, cooldownImage, background);
        return slotUi;
    }
}
