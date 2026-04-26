using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class EquipmentUiSceneBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/SlotUI.prefab";

    [MenuItem("Tools/Roguelike/Build Equipment UI In Scene")]
    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1400f, 900f);
            scaler.matchWidthOrHeight = 0.6f;
        }

        RectTransform panel = GetOrCreateRect("EquipmentPanel", canvas.transform);
        Image panelBg = panel.GetComponent<Image>();
        if (panelBg == null)
        {
            panelBg = panel.gameObject.AddComponent<Image>();
        }
        panelBg.color = new Color(0.03f, 0.07f, 0.1f, 0.86f);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(-120f, 145f);
        panel.sizeDelta = new Vector2(420f, 130f);

        VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        if (panelFitter == null)
        {
            panelFitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        }
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform weaponsRow = GetOrCreateRect("WeaponsRow", panel);
        ConfigureRow(weaponsRow);

        RectTransform modulesRow = GetOrCreateRect("ModulesRow", panel);
        ConfigureRow(modulesRow);

        SlotUI slotPrefab = EnsureSlotPrefab();

        EquipmentUIController equipmentUi = panel.GetComponent<EquipmentUIController>();
        if (equipmentUi == null)
        {
            equipmentUi = panel.gameObject.AddComponent<EquipmentUIController>();
        }

        SpaceCombatSceneController sceneController = Object.FindObjectOfType<SpaceCombatSceneController>(true);
        if (sceneController == null)
        {
            Debug.LogError("EquipmentUiSceneBuilder: SpaceCombatSceneController not found in scene.");
            return;
        }

        SerializedObject uiSo = new SerializedObject(equipmentUi);
        uiSo.FindProperty("sceneController").objectReferenceValue = sceneController;
        uiSo.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        uiSo.FindProperty("weaponSlotsContainer").objectReferenceValue = weaponsRow;
        uiSo.FindProperty("moduleSlotsContainer").objectReferenceValue = modulesRow;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(sceneController);
        controllerSo.FindProperty("equipmentUiController").objectReferenceValue = equipmentUi;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(sceneController.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("Equipment UI assembled in scene and references are wired.");
    }

    private static void ConfigureRow(RectTransform row)
    {
        row.anchorMin = new Vector2(0f, 0.5f);
        row.anchorMax = new Vector2(1f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(0f, 52f);

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static SlotUI EnsureSlotPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? "Assets/Prefabs/UI");

        SlotUI existing = AssetDatabase.LoadAssetAtPath<SlotUI>(PrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("SlotUI", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(SlotUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(72f, 72f);
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.05f, 0.1f, 0.14f, 0.92f);
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
        keyText.text = "1";

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
        SerializedObject slotSo = new SerializedObject(slotUi);
        slotSo.FindProperty("iconImage").objectReferenceValue = iconImage;
        slotSo.FindProperty("keyBindText").objectReferenceValue = keyText;
        slotSo.FindProperty("cooldownOverlay").objectReferenceValue = cooldownImage;
        slotSo.FindProperty("backgroundImage").objectReferenceValue = rootImage;
        slotSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<SlotUI>(PrefabPath);
    }

    private static RectTransform GetOrCreateRect(string name, Transform parent)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            return found as RectTransform;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
}
