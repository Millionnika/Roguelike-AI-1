using System.Collections.Generic;
using TMPro;
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

    [Header("Responsive Layout")]
    [SerializeField] private bool autoResizeSlots = true;
    [SerializeField, Min(0f)] private float panelPadding = 10f;
    [SerializeField, Min(0f)] private float rowSpacing = 8f;
    [SerializeField, Min(0f)] private float slotSpacing = 10f;
    [SerializeField, Min(12f)] private float minRowHeight = 32f;
    [SerializeField, Min(8f)] private float minSlotSize = 16f;

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

    private void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
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

        RemoveMissingSlots(weaponSlots);
        RemoveMissingSlots(moduleSlots);

        int weaponCount = state != null ? state.InstalledWeapons.Count : 0;
        int moduleCount = state != null ? state.InstalledModules.Count : 0;

        EnsureSlotCount(weaponSlots, weaponSlotsContainer, weaponCount, "WeaponSlot");
        EnsureSlotCount(moduleSlots, moduleSlotsContainer, moduleCount, "ModuleSlot");

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            SlotUI slot = weaponSlots[i];
            if (slot == null)
            {
                continue;
            }

            WeaponDataSO weapon = state != null ? state.InstalledWeapons[i] : null;
            Sprite icon = weapon != null ? weapon.icon : null;
            slot.Setup(icon, (i + 1).ToString(), weapon != null);
        }

        for (int i = 0; i < moduleSlots.Count; i++)
        {
            SlotUI slot = moduleSlots[i];
            if (slot == null)
            {
                continue;
            }

            ModuleDataSO module = state != null ? state.InstalledModules[i] : null;
            Sprite icon = module != null ? module.icon : null;
            slot.Setup(icon, (i + 1).ToString());
        }

        ApplyResponsiveLayout();
        RefreshCooldowns(state);
    }

    public void RefreshCooldowns(ShipEquipmentState state)
    {
        RemoveMissingSlots(weaponSlots);
        RemoveMissingSlots(moduleSlots);

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            SlotUI slot = weaponSlots[i];
            if (slot == null)
            {
                continue;
            }

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

            slot.SetCooldown01(cooldown01);
        }

        for (int i = 0; i < moduleSlots.Count; i++)
        {
            SlotUI slot = moduleSlots[i];
            if (slot != null)
            {
                slot.SetCooldown01(0f);
            }
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
        SyncSlotsFromContainer(slots, container);

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

    private static void SyncSlotsFromContainer(List<SlotUI> slots, Transform container)
    {
        if (slots == null)
        {
            return;
        }

        slots.Clear();
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            SlotUI existing = container.GetChild(i).GetComponent<SlotUI>();
            if (existing != null && !slots.Contains(existing))
            {
                slots.Add(existing);
            }
        }
    }

    private static void RemoveMissingSlots(List<SlotUI> slots)
    {
        if (slots == null || slots.Count == 0)
        {
            return;
        }

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == null)
            {
                slots.RemoveAt(i);
            }
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!autoResizeSlots)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        RectTransform weaponsRect = weaponSlotsContainer as RectTransform;
        RectTransform modulesRect = moduleSlotsContainer as RectTransform;
        if (panelRect == null || weaponsRect == null || modulesRect == null)
        {
            return;
        }

        NormalizePanelRect(panelRect);
        DisableConflictingPanelLayout(panelRect);

        float panelWidth = Mathf.Max(1f, panelRect.rect.width);
        float panelHeight = Mathf.Max(1f, panelRect.rect.height);
        float usableHeight = Mathf.Max(1f, panelHeight - panelPadding * 2f - rowSpacing);
        float rowHeight = Mathf.Max(minRowHeight, usableHeight * 0.5f);

        SetRowRect(weaponsRect, panelPadding, rowHeight, panelPadding);
        SetRowRect(modulesRect, panelPadding + rowHeight + rowSpacing, rowHeight, panelPadding);

        UpdateRowLayoutGroup(weaponsRect);
        UpdateRowLayoutGroup(modulesRect);
        ResizeSlotsToRow(weaponsRect, weaponSlots);
        ResizeSlotsToRow(modulesRect, moduleSlots);
    }

    private static void NormalizePanelRect(RectTransform panelRect)
    {
        if (panelRect == null)
        {
            return;
        }

        Vector2 anchorMin = panelRect.anchorMin;
        Vector2 anchorMax = panelRect.anchorMax;
        if (anchorMin.x > anchorMax.x)
        {
            float temp = anchorMin.x;
            anchorMin.x = anchorMax.x;
            anchorMax.x = temp;
        }

        if (anchorMin.y > anchorMax.y)
        {
            float temp = anchorMin.y;
            anchorMin.y = anchorMax.y;
            anchorMax.y = temp;
        }

        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.localScale = Vector3.one;
    }

    private static void DisableConflictingPanelLayout(RectTransform panelRect)
    {
        if (panelRect == null)
        {
            return;
        }

        VerticalLayoutGroup verticalLayout = panelRect.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null && verticalLayout.enabled)
        {
            verticalLayout.enabled = false;
        }

        ContentSizeFitter fitter = panelRect.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled)
        {
            fitter.enabled = false;
        }
    }

    private static void SetRowRect(RectTransform rowRect, float topOffset, float height, float horizontalPadding)
    {
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(horizontalPadding, -topOffset - height);
        rowRect.offsetMax = new Vector2(-horizontalPadding, -topOffset);
    }

    private void UpdateRowLayoutGroup(RectTransform rowRect)
    {
        HorizontalLayoutGroup layoutGroup = rowRect.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup == null)
        {
            return;
        }

        layoutGroup.spacing = slotSpacing;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = rowRect.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled)
        {
            fitter.enabled = false;
        }
    }

    private void ResizeSlotsToRow(RectTransform rowRect, List<SlotUI> slots)
    {
        if (rowRect == null || slots == null || slots.Count == 0)
        {
            return;
        }

        HorizontalLayoutGroup layoutGroup = rowRect.GetComponent<HorizontalLayoutGroup>();
        float spacing = layoutGroup != null ? layoutGroup.spacing : slotSpacing;
        int paddingLeft = layoutGroup != null ? layoutGroup.padding.left : 0;
        int paddingRight = layoutGroup != null ? layoutGroup.padding.right : 0;
        int paddingTop = layoutGroup != null ? layoutGroup.padding.top : 0;
        int paddingBottom = layoutGroup != null ? layoutGroup.padding.bottom : 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                count++;
            }
        }
        if (count == 0)
        {
            return;
        }

        float usableWidth = Mathf.Max(1f, rowRect.rect.width - paddingLeft - paddingRight - spacing * Mathf.Max(0, count - 1));
        float usableHeight = Mathf.Max(1f, rowRect.rect.height - paddingTop - paddingBottom);
        float slotSize = Mathf.Max(minSlotSize, Mathf.Min(usableWidth / count, usableHeight));

        for (int i = 0; i < slots.Count; i++)
        {
            SlotUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            RectTransform slotRect = slot.transform as RectTransform;
            if (slotRect != null)
            {
                NormalizeSlotRect(slotRect);
                slotRect.sizeDelta = new Vector2(slotSize, slotSize);
            }

            LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = slot.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = minSlotSize;
            layoutElement.minHeight = minSlotSize;
            layoutElement.preferredWidth = slotSize;
            layoutElement.preferredHeight = slotSize;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }
    }

    private static void NormalizeSlotRect(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.anchoredPosition = Vector2.zero;
        slotRect.localScale = Vector3.one;
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

        GameObject keyGo = new GameObject("KeyBind", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyGo.transform.SetParent(root.transform, false);
        RectTransform keyRect = keyGo.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -4f);
        keyRect.sizeDelta = new Vector2(0f, 18f);
        TMP_Text keyText = keyGo.GetComponent<TMP_Text>();
        keyText.fontSize = 12;
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.color = Color.white;
        keyText.raycastTarget = false;

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
