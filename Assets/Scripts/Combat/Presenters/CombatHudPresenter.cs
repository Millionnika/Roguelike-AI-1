using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatHudPresenter : MonoBehaviour
{
    [Header("Ссылки боевого HUD")]
    [Tooltip("Дополнительный слайдер корпуса игрока. Используется для совместимости со сценой и показывает процент корпуса.")]
    [SerializeField] private Slider healthBar;
    [Tooltip("Текст очков. Сохраняет текущее поведение: показывает опыт игрока.")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("Текст текущей волны. Показывает номер волны из боевой сессии.")]
    [SerializeField] private TMP_Text waveText;
    [Tooltip("Контроллер UI экипировки. Если не назначен, будет найден или создан на панели экипировки.")]
    [SerializeField] private EquipmentUIController equipmentUiController;
    [Tooltip("Префаб слота экипировки для runtime-панели оружия и модулей.")]
    [SerializeField] private SlotUI slotUiPrefab;

    private readonly List<EnemyRowView> enemyRows = new List<EnemyRowView>();

    private SpaceCombatSceneController sceneController;
    private ISpaceCombatUiFactory uiFactory;
    private Func<string, string> localize;
    private Font uiFont;
    private Sprite squareSprite;

    private TMP_Text overviewTitleText;
    private TMP_Text enemyHeaderText;
    private TMP_Text playerStatusTitleText;
    private TMP_Text statusText;
    private TMP_Text targetNameText;
    private TMP_Text targetDistanceText;
    private TMP_Text targetDisplayText;
    private TMP_Text capacitorText;
    private TMP_Text levelText;
    private TMP_Text experienceText;
    private TMP_Text shipText;
    private Image targetPanel;
    private Image targetShieldFill;
    private Image targetArmorFill;
    private Image targetHullFill;
    private Image playerShieldFill;
    private Image playerArmorFill;
    private Image playerHullFill;
    private Image playerExperienceFill;
    private Image capacitorFill;
    private TMP_Text targetShieldValueText;
    private TMP_Text targetArmorValueText;
    private TMP_Text targetHullValueText;
    private TMP_Text playerShieldValueText;
    private TMP_Text playerArmorValueText;
    private TMP_Text playerHullValueText;
    private TMP_Text playerExperienceValueText;
    private TMP_Text playerLevelBadgeText;
    private TMP_Text capacitorValueText;
    private RectTransform overviewPanelRect;

    public EquipmentUIController EquipmentUiController => equipmentUiController;
    public TMP_Text StatusText => statusText;

    internal void Initialize(
        SpaceCombatSceneController controller,
        ISpaceCombatUiFactory combatUiFactory,
        Font combatUiFont,
        Sprite combatSquareSprite,
        SlotUI slotPrefab,
        Slider fallbackHealthBar,
        TMP_Text fallbackScoreText,
        TMP_Text fallbackWaveText,
        EquipmentUIController fallbackEquipmentUiController,
        Func<string, string> localizeCallback)
    {
        sceneController = controller;
        uiFactory = combatUiFactory;
        uiFont = combatUiFont;
        squareSprite = combatSquareSprite;
        localize = localizeCallback;

        if (slotPrefab != null) slotUiPrefab = slotPrefab;
        if (fallbackHealthBar != null) healthBar = fallbackHealthBar;
        if (fallbackScoreText != null) scoreText = fallbackScoreText;
        if (fallbackWaveText != null) waveText = fallbackWaveText;
        if (fallbackEquipmentUiController != null) equipmentUiController = fallbackEquipmentUiController;
    }

    internal void BindOrBuild(Transform uiRoot)
    {
        if (uiRoot == null)
        {
            Debug.LogWarning("CombatHudPresenter: корневой объект UI не назначен.");
            return;
        }

        if (uiRoot.Find("OverviewPanel") != null || uiRoot.Find("PlayerStatus") != null || uiRoot.Find("EquipmentPanel") != null)
        {
            BindOverviewPanel(uiRoot);
            BindPlayerStatusPanel(uiRoot);
            BindEquipmentPanel(uiRoot);
            statusText = FindText(uiRoot, "StatusLabel");
            return;
        }

        CreateRightOverviewPanel(uiRoot);
        CreatePlayerStatusHud(uiRoot);
        if (uiRoot.Find("EquipmentPanel") == null)
        {
            CreateEquipmentHud(uiRoot);
        }
        statusText = FindText(uiRoot, "StatusLabel");
    }

    internal void Tick(CombatHudContext context)
    {
        if (context.Player == null)
        {
            return;
        }

        if (capacitorText != null)
        {
            capacitorText.text = Localize("capacitor") + Mathf.RoundToInt(context.Player.CapacitorPercent * 100f) + "%";
        }
        if (capacitorValueText != null)
        {
            capacitorValueText.text = FormatBarValue(context.Player.Capacitor, context.Player.MaxCapacitor);
        }

        EnemyShip targetEnemy = context.TargetingController != null ? context.TargetingController.TargetEnemy : null;
        EnemyBaseLair targetBase = context.TargetingController != null ? context.TargetingController.TargetBase : null;
        string targetDisplayName = Localize("target_none_name");
        if (targetEnemy != null && targetEnemy.IsAlive())
        {
            targetDisplayName = targetEnemy.Id + " (" + targetEnemy.Type + ")";
        }
        else if (targetBase != null && targetBase.IsAlive)
        {
            targetDisplayName = targetBase.name;
        }

        if (targetDisplayText != null) targetDisplayText.text = Localize("target_label") + targetDisplayName;
        if (shipText != null) shipText.text = Localize("ship_label") + context.ShipName;
        if (levelText != null) levelText.text = Localize("level_label") + context.Player.Level;
        if (experienceText != null) experienceText.text = Localize("xp_label") + context.Player.Experience + " / " + context.Player.ExperienceToNext;

        UpdateTargetPanel(context.Player, targetEnemy, targetBase);
        UpdatePlayerBars(context.Player);
        UpdateEnemyRows(context.Player, context.Enemies, context.TargetingController);
        UpdateStatus(context);

        if (equipmentUiController != null)
        {
            equipmentUiController.RefreshCooldowns(context.EquipmentState);
        }

        if (healthBar != null) healthBar.value = context.Player.HullPercent;
        if (scoreText != null) scoreText.text = context.Player.Experience.ToString();
        if (waveText != null) waveText.text = context.CurrentWave.ToString();
    }

    internal void RefreshLocalizedTexts()
    {
        if (overviewTitleText != null) overviewTitleText.text = Localize("overview");
        if (enemyHeaderText != null) enemyHeaderText.text = Localize("enemy_header");
        if (playerStatusTitleText != null) playerStatusTitleText.text = Localize("ship_status");
    }

    internal void UpdateModuleVisual(ModuleState module)
    {
        if (module == null || module.SlotImage == null)
        {
            return;
        }

        module.SlotImage.color = module.Active
            ? new Color(0.12f, 0.31f, 0.42f, 0.98f)
            : new Color(0.05f, 0.1f, 0.14f, 0.92f);
    }

    public bool TrySelectEnemyFromOverview(Vector2 screenPosition, TargetingController targetingController)
    {
        if (targetingController == null)
        {
            return false;
        }

        for (int i = 0; i < enemyRows.Count; i++)
        {
            EnemyRowView row = enemyRows[i];
            if (row.RootTransform == null || !row.RootTransform.gameObject.activeSelf || row.RootText == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(row.RootTransform, screenPosition, null))
            {
                EnemyShip enemy = row.Enemy;
                return enemy != null && enemy.IsAlive() && targetingController.TrySelectFromOverview(enemy);
            }
        }

        return false;
    }

    public bool IsOverviewBlocked(Vector2 screenPosition)
    {
        return overviewPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(overviewPanelRect, screenPosition, null);
    }

    private void UpdateTargetPanel(PlayerShip player, EnemyShip targetEnemy, EnemyBaseLair targetBase)
    {
        bool hasEnemyTarget = targetEnemy != null && targetEnemy.IsAlive();
        bool hasBaseTarget = targetBase != null && targetBase.IsAlive;
        if (targetPanel != null)
        {
            targetPanel.gameObject.SetActive(hasEnemyTarget || hasBaseTarget);
        }

        if (hasEnemyTarget)
        {
            if (targetNameText != null) targetNameText.text = targetEnemy.Id + "  " + targetEnemy.Type;
            if (targetDistanceText != null && player.Transform != null && targetEnemy.Transform != null)
            {
                targetDistanceText.text = Localize("distance") + Vector3.Distance(player.Transform.position, targetEnemy.Transform.position).ToString("0.0") + " km";
            }

            SetFillWidth(targetShieldFill, targetEnemy.ShieldPercent, 252f);
            SetFillWidth(targetArmorFill, targetEnemy.ArmorPercent, 252f);
            SetFillWidth(targetHullFill, targetEnemy.HullPercent, 252f);
            if (targetShieldValueText != null) targetShieldValueText.text = FormatBarValue(targetEnemy.Shield, targetEnemy.MaxShield);
            if (targetArmorValueText != null) targetArmorValueText.text = FormatBarValue(targetEnemy.Armor, targetEnemy.MaxArmor);
            if (targetHullValueText != null) targetHullValueText.text = FormatBarValue(targetEnemy.Hull, targetEnemy.MaxHull);
            return;
        }

        if (!hasBaseTarget)
        {
            return;
        }

        ShipDurabilityState state = targetBase.CurrentDurability;
        if (targetNameText != null) targetNameText.text = targetBase.name + "  BASE";
        if (targetDistanceText != null && player.Transform != null)
        {
            targetDistanceText.text = Localize("distance") + Vector3.Distance(player.Transform.position, targetBase.transform.position).ToString("0.0") + " km";
        }

        float shieldPercent = state.MaxShield <= 0f ? 0f : state.Shield / Mathf.Max(0.01f, state.MaxShield);
        float armorPercent = state.MaxArmor <= 0f ? 0f : state.Armor / Mathf.Max(0.01f, state.MaxArmor);
        float hullPercent = state.MaxHull <= 0f ? 0f : state.Hull / Mathf.Max(0.01f, state.MaxHull);
        SetFillWidth(targetShieldFill, shieldPercent, 252f);
        SetFillWidth(targetArmorFill, armorPercent, 252f);
        SetFillWidth(targetHullFill, hullPercent, 252f);
        if (targetShieldValueText != null) targetShieldValueText.text = FormatBarValue(state.Shield, state.MaxShield);
        if (targetArmorValueText != null) targetArmorValueText.text = FormatBarValue(state.Armor, state.MaxArmor);
        if (targetHullValueText != null) targetHullValueText.text = FormatBarValue(state.Hull, state.MaxHull);
    }

    private void UpdatePlayerBars(PlayerShip player)
    {
        SetFillWidth(playerShieldFill, player.ShieldPercent, 180f);
        SetFillWidth(playerArmorFill, player.ArmorPercent, 180f);
        SetFillWidth(playerHullFill, player.HullPercent, 180f);
        float experiencePercent = player.ExperienceToNext <= 0 ? 0f : player.Experience / (float)player.ExperienceToNext;
        SetFillWidth(playerExperienceFill, experiencePercent, 180f);
        if (playerShieldValueText != null) playerShieldValueText.text = FormatBarValue(player.Shield, player.MaxShield);
        if (playerArmorValueText != null) playerArmorValueText.text = FormatBarValue(player.Armor, player.MaxArmor);
        if (playerHullValueText != null) playerHullValueText.text = FormatBarValue(player.Hull, player.MaxHull);
        if (playerExperienceValueText != null) playerExperienceValueText.text = player.Experience + " / " + player.ExperienceToNext;
        if (playerLevelBadgeText != null) playerLevelBadgeText.text = "LVL " + player.Level;
        SetFillHeight(capacitorFill, player.CapacitorPercent, 60f);
    }

    private void UpdateEnemyRows(PlayerShip player, IReadOnlyList<EnemyShip> enemies, TargetingController targetingController)
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        List<EnemyShip> sortedEnemies = new List<EnemyShip>();
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                {
                    sortedEnemies.Add(enemies[i]);
                }
            }
        }

        sortedEnemies.Sort((left, right) =>
            Vector3.Distance(left.Transform.position, player.Transform.position)
                .CompareTo(Vector3.Distance(right.Transform.position, player.Transform.position)));

        for (int i = 0; i < enemyRows.Count; i++)
        {
            bool active = i < sortedEnemies.Count;
            if (enemyRows[i].RootTransform != null)
            {
                enemyRows[i].RootTransform.gameObject.SetActive(active);
            }
            if (!active)
            {
                enemyRows[i].Enemy = null;
                continue;
            }

            EnemyShip enemy = sortedEnemies[i];
            enemyRows[i].Enemy = enemy;
            float distance = Vector3.Distance(player.Transform.position, enemy.Transform.position);
            if (enemyRows[i].RootText != null)
            {
                enemyRows[i].RootText.text = string.Format("{0,-7}  {1,-11}  {2,4:0.0}km", enemy.Id, enemy.Type, distance);
            }

            SetFillWidth(enemyRows[i].ShieldFill, enemy.ShieldPercent, 110f);
            SetFillWidth(enemyRows[i].ArmorFill, enemy.ArmorPercent, 110f);
            SetFillWidth(enemyRows[i].HullFill, enemy.HullPercent, 110f);

            Image background = enemyRows[i].RootTransform != null ? enemyRows[i].RootTransform.GetComponent<Image>() : null;
            if (background != null)
            {
                background.color = targetingController != null && enemy == targetingController.TargetEnemy
                    ? new Color(0.12f, 0.3f, 0.42f, 1f)
                    : new Color(0.06f, 0.11f, 0.16f, 0.95f);
            }
        }
    }

    private void UpdateStatus(CombatHudContext context)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = !context.GameStarted
            ? Localize("status_menu")
            : context.GameOver
                ? Localize("status_gameover")
                : context.LevelUpPending
                    ? Localize("status_levelup")
                    : context.UseVirtualJoystick ? Localize("status_play_mobile") : Localize("status_play_desktop");
    }

    private void BindOverviewPanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("OverviewPanel");
        if (panel == null)
        {
            return;
        }

        overviewPanelRect = panel.GetComponent<RectTransform>();
        overviewTitleText = FindText(panel, "Title");
        Transform target = panel.Find("TargetPanel");
        targetPanel = FindImage(panel, "TargetPanel");
        targetNameText = FindText(target, "TargetName");
        targetDistanceText = FindText(target, "TargetDistance");
        targetShieldFill = FindIndexedFill(target, "BarBackground", 0);
        targetArmorFill = FindIndexedFill(target, "BarBackground", 1);
        targetHullFill = FindIndexedFill(target, "BarBackground", 2);
        targetShieldValueText = FindIndexedText(target, "Value", 0);
        targetArmorValueText = FindIndexedText(target, "Value", 1);
        targetHullValueText = FindIndexedText(target, "Value", 2);
        enemyHeaderText = FindText(panel, "EnemyHeader");
        capacitorText = FindText(panel, "CapText");
        targetDisplayText = FindText(panel, "TargetDisplay");
        shipText = FindText(panel, "ShipText");
        levelText = FindText(panel, "LevelText");
        experienceText = FindText(panel, "ExperienceText");

        enemyRows.Clear();
        Transform rowsRoot = panel.Find("EnemyRows");
        if (rowsRoot == null)
        {
            return;
        }

        for (int i = 0; i < 9; i++)
        {
            Transform rowTransform = rowsRoot.Find("EnemyRow_" + i);
            if (rowTransform == null)
            {
                continue;
            }

            enemyRows.Add(new EnemyRowView
            {
                RootTransform = rowTransform.GetComponent<RectTransform>(),
                RootText = FindText(rowTransform, "RowText"),
                ShieldFill = FindIndexedFill(rowTransform, "BarBg", 0),
                ArmorFill = FindIndexedFill(rowTransform, "BarBg", 1),
                HullFill = FindIndexedFill(rowTransform, "BarBg", 2)
            });
        }
    }

    private void BindPlayerStatusPanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("PlayerStatus");
        if (panel == null)
        {
            return;
        }

        playerStatusTitleText = FindText(panel, "Label");
        playerShieldFill = FindImage(panel, "ShieldBg/ShieldFill");
        playerArmorFill = FindImage(panel, "ArmorBg/ArmorFill");
        playerHullFill = FindImage(panel, "HullBg/HullFill");
        playerExperienceFill = FindImage(panel, "XPBg/XPFill");
        playerShieldValueText = FindText(panel, "ShieldBg/Value");
        playerArmorValueText = FindText(panel, "ArmorBg/Value");
        playerHullValueText = FindText(panel, "HullBg/Value");
        playerExperienceValueText = FindText(panel, "XPBg/Value");
        playerLevelBadgeText = FindText(panel, "LevelBadge");
        capacitorFill = FindImage(panel, "CapacitorFill");
        capacitorValueText = FindText(panel, "CapValue");
    }

    private void BindEquipmentPanel(Transform uiRoot)
    {
        Transform authoredPanel = uiRoot.Find("EquipmentPanel");
        Transform runtimePanel = uiRoot.Find("EquipmentPanelRuntime");
        if (authoredPanel == null)
        {
            if (runtimePanel != null) runtimePanel.gameObject.SetActive(false);
            return;
        }

        authoredPanel.gameObject.SetActive(true);
        if (runtimePanel != null) runtimePanel.gameObject.SetActive(false);

        RectTransform panelRect = authoredPanel.GetComponent<RectTransform>();
        bool panelHadInvertedAnchors = NormalizeAuthoredRect(panelRect);
        AlignEquipmentPanelToModulePanelIfNeeded(uiRoot, panelRect, panelHadInvertedAnchors);
        DisableDuplicateEquipmentPanels(uiRoot, authoredPanel);
        EquipmentUIController runtimeController = authoredPanel.GetComponent<EquipmentUIController>();
        if (runtimeController == null)
        {
            runtimeController = authoredPanel.gameObject.AddComponent<EquipmentUIController>();
        }
        else
        {
            runtimeController.enabled = true;
        }

        Transform weaponsRow = authoredPanel.Find("WeaponsRow");
        Transform modulesRow = authoredPanel.Find("ModulesRow");
        runtimeController.Configure(
            sceneController,
            slotUiPrefab,
            weaponsRow != null ? weaponsRow.GetComponent<RectTransform>() : null,
            modulesRow != null ? modulesRow.GetComponent<RectTransform>() : null);
        equipmentUiController = runtimeController;
    }

    private void CreateRightOverviewPanel(Transform parent)
    {
        Image panel = CreateImage("OverviewPanel", parent, new Color(0.04f, 0.08f, 0.11f, 0.94f));
        RectTransform rect = panel.rectTransform;
        overviewPanelRect = rect;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(290f, -40f);
        rect.anchoredPosition = new Vector2(-10f, 0f);
        AddOutline(panel.gameObject, new Color(0.12f, 0.28f, 0.4f, 1f));

        overviewTitleText = CreateText("Title", panel.transform, "OVERVIEW", 20, FontStyle.Bold, new Color(0.52f, 0.8f, 1f));
        RectTransform titleRect = overviewTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-20f, 38f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);

        targetPanel = CreateImage("TargetPanel", panel.transform, new Color(0.06f, 0.13f, 0.18f, 0.95f));
        RectTransform targetRect = targetPanel.rectTransform;
        targetRect.anchorMin = new Vector2(0f, 1f);
        targetRect.anchorMax = new Vector2(1f, 1f);
        targetRect.pivot = new Vector2(0.5f, 1f);
        targetRect.sizeDelta = new Vector2(-18f, 114f);
        targetRect.anchoredPosition = new Vector2(0f, -54f);
        AddOutline(targetPanel.gameObject, new Color(0.25f, 0.55f, 0.7f, 1f));

        targetNameText = CreateText("TargetName", targetPanel.transform, "-", 18, FontStyle.Bold, new Color(1f, 0.88f, 0.45f));
        SetAnchoredRect(targetNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), new Vector2(-8f, -30f));
        targetDistanceText = CreateText("TargetDistance", targetPanel.transform, "-", 13, FontStyle.Normal, new Color(0.6f, 0.82f, 1f));
        SetAnchoredRect(targetDistanceText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -34f), new Vector2(-8f, -52f));

        targetShieldFill = CreateBar(targetPanel.transform, new Vector2(8f, -62f), new Color(0.23f, 0.62f, 1f));
        targetArmorFill = CreateBar(targetPanel.transform, new Vector2(8f, -78f), new Color(0.72f, 0.72f, 0.75f));
        targetHullFill = CreateBar(targetPanel.transform, new Vector2(8f, -94f), new Color(0.86f, 0.31f, 0.31f));
        targetShieldValueText = CreateBarValueText(targetShieldFill);
        targetArmorValueText = CreateBarValueText(targetArmorFill);
        targetHullValueText = CreateBarValueText(targetHullFill);

        enemyHeaderText = CreateText("EnemyHeader", panel.transform, "ID          TYPE        DIST       STATUS", 12, FontStyle.Bold, new Color(0.52f, 0.68f, 0.8f));
        SetAnchoredRect(enemyHeaderText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -176f), new Vector2(-10f, -196f));

        RectTransform rowsRoot = new GameObject("EnemyRows", typeof(RectTransform)).GetComponent<RectTransform>();
        rowsRoot.SetParent(panel.transform, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.sizeDelta = new Vector2(-18f, 430f);
        rowsRoot.anchoredPosition = new Vector2(0f, -204f);

        enemyRows.Clear();
        for (int i = 0; i < 9; i++)
        {
            enemyRows.Add(CreateEnemyRow(rowsRoot, i));
        }

        capacitorText = CreateText("CapText", panel.transform, "Capacitor: 100%", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(capacitorText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 78f), new Vector2(-10f, 102f));
        capacitorText.gameObject.SetActive(false);
        targetDisplayText = CreateText("TargetDisplay", panel.transform, "Target: none", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(targetDisplayText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 52f), new Vector2(-10f, 76f));
        targetDisplayText.gameObject.SetActive(false);
        shipText = CreateText("ShipText", panel.transform, "Ship: none", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(shipText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 26f), new Vector2(-10f, 50f));
        shipText.gameObject.SetActive(false);
        levelText = CreateText("LevelText", panel.transform, "Level: 1", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(levelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 24f));
        levelText.gameObject.SetActive(false);
        experienceText = CreateText("ExperienceText", panel.transform, "XP: 0 / 100", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(experienceText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, -24f), new Vector2(-10f, 0f));
        experienceText.gameObject.SetActive(false);
    }

    private void CreatePlayerStatusHud(Transform parent)
    {
        Image panel = CreateImage("PlayerStatus", parent, new Color(0.03f, 0.07f, 0.1f, 0.86f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(320f, 142f);
        rect.anchoredPosition = new Vector2(-120f, 16f);
        AddOutline(panel.gameObject, new Color(0.15f, 0.32f, 0.44f, 1f));

        playerStatusTitleText = CreateText("Label", panel.transform, "SHIP STATUS", 16, FontStyle.Bold, new Color(0.85f, 0.92f, 1f));
        SetAnchoredRect(playerStatusTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-12f, -28f));
        playerLevelBadgeText = CreateText("LevelBadge", panel.transform, "LVL 1", 13, FontStyle.Bold, new Color(1f, 0.9f, 0.42f));
        playerLevelBadgeText.alignment = TextAlignmentOptions.Right;
        SetAnchoredRect(playerLevelBadgeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-94f, -8f), new Vector2(-12f, -28f));

        playerShieldFill = CreateLabeledBar(panel.transform, "Shield", new Vector2(12f, -40f), new Color(0.23f, 0.62f, 1f));
        playerArmorFill = CreateLabeledBar(panel.transform, "Armor", new Vector2(12f, -64f), new Color(0.72f, 0.72f, 0.75f));
        playerHullFill = CreateLabeledBar(panel.transform, "Hull", new Vector2(12f, -88f), new Color(0.86f, 0.31f, 0.31f));
        playerExperienceFill = CreateLabeledBar(panel.transform, "XP", new Vector2(12f, -112f), new Color(0.58f, 0.42f, 1f));
        playerShieldValueText = CreateBarValueText(playerShieldFill);
        playerArmorValueText = CreateBarValueText(playerArmorFill);
        playerHullValueText = CreateBarValueText(playerHullFill);
        playerExperienceValueText = CreateBarValueText(playerExperienceFill);

        TMP_Text capacitorLabel = CreateText("CapLabel", panel.transform, "Cap", 13, FontStyle.Bold, new Color(0.95f, 0.9f, 0.6f));
        SetAnchoredRect(capacitorLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-86f, 18f), new Vector2(-50f, 34f));
        capacitorFill = CreateImage("CapacitorFill", panel.transform, new Color(0.95f, 0.85f, 0.35f, 1f));
        RectTransform capRect = capacitorFill.rectTransform;
        capRect.anchorMin = new Vector2(1f, 0f);
        capRect.anchorMax = new Vector2(1f, 0f);
        capRect.pivot = new Vector2(0f, 0.5f);
        capRect.sizeDelta = new Vector2(30f, 60f);
        capRect.anchoredPosition = new Vector2(-44f, 46f);
        AddOutline(capacitorFill.gameObject, new Color(0.96f, 0.82f, 0.32f, 1f));

        capacitorValueText = CreateText("CapValue", panel.transform, string.Empty, 10, FontStyle.Bold, new Color(1f, 0.95f, 0.68f));
        capacitorValueText.alignment = TextAlignmentOptions.Center;
        capacitorValueText.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnchoredRect(capacitorValueText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-94f, 0f), new Vector2(-10f, 16f));
    }

    private void CreateEquipmentHud(Transform parent)
    {
        RectTransform panel = new GameObject("EquipmentPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(-120f, 145f);
        panel.sizeDelta = new Vector2(420f, 130f);

        Image panelBackground = panel.GetComponent<Image>();
        panelBackground.color = new Color(0.03f, 0.07f, 0.1f, 0.86f);
        AddOutline(panelBackground.gameObject, new Color(0.15f, 0.32f, 0.44f, 1f));

        VerticalLayoutGroup verticalLayout = panel.GetComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(10, 10, 8, 8);
        verticalLayout.spacing = 8f;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandWidth = false;
        verticalLayout.childForceExpandHeight = false;

        RectTransform weaponsRow = CreateEquipmentRow(panel, "WeaponsRow");
        RectTransform modulesRow = CreateEquipmentRow(panel, "ModulesRow");

        EquipmentUIController runtimeController = panel.gameObject.AddComponent<EquipmentUIController>();
        runtimeController.Configure(sceneController, slotUiPrefab, weaponsRow, modulesRow);
        equipmentUiController = runtimeController;
    }

    private EnemyRowView CreateEnemyRow(Transform parent, int index)
    {
        Image rowBackground = CreateImage("EnemyRow_" + index, parent, new Color(0.06f, 0.11f, 0.16f, 0.95f));
        RectTransform rowRect = rowBackground.rectTransform;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 44f);
        rowRect.anchoredPosition = new Vector2(0f, -index * 46f);
        rowBackground.gameObject.AddComponent<Button>();
        AddOutline(rowBackground.gameObject, new Color(0.12f, 0.24f, 0.34f, 1f));

        TMP_Text text = CreateText("RowText", rowBackground.transform, string.Empty, 12, FontStyle.Bold, new Color(0.85f, 0.92f, 1f));
        SetAnchoredRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -18f));

        return new EnemyRowView
        {
            RootTransform = rowRect,
            RootText = text,
            ShieldFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -24f), new Color(0.23f, 0.62f, 1f)),
            ArmorFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -32f), new Color(0.72f, 0.72f, 0.75f)),
            HullFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -40f), new Color(0.86f, 0.31f, 0.31f))
        };
    }

    private Image CreateMiniBar(Transform parent, Vector2 anchoredPosition, Color fillColor)
    {
        Image background = CreateImage("BarBg", parent, new Color(0.12f, 0.17f, 0.2f, 1f));
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0f, 1f);
        bgRect.sizeDelta = new Vector2(110f, 6f);
        bgRect.anchoredPosition = anchoredPosition;

        Image fill = CreateImage("Fill", background.transform, fillColor);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(110f, 0f);
        fillRect.anchoredPosition = Vector2.zero;
        return fill;
    }

    private static RectTransform CreateEquipmentRow(Transform parent, string objectName)
    {
        RectTransform row = new GameObject(objectName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        row.anchorMin = new Vector2(0f, 0.5f);
        row.anchorMax = new Vector2(1f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(0f, 52f);

        HorizontalLayoutGroup horizontalLayout = row.GetComponent<HorizontalLayoutGroup>();
        horizontalLayout.spacing = 10f;
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row;
    }

    private static void DisableDuplicateEquipmentPanels(Transform uiRoot, Transform activePanel)
    {
        EquipmentUIController[] controllers = uiRoot.GetComponentsInChildren<EquipmentUIController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            EquipmentUIController controller = controllers[i];
            if (controller == null || controller.transform == activePanel)
            {
                continue;
            }

            controller.enabled = false;
            controller.gameObject.SetActive(false);
        }
    }

    private static bool NormalizeAuthoredRect(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        Vector2 anchorMin = rect.anchorMin;
        Vector2 anchorMax = rect.anchorMax;
        bool changed = false;

        if (anchorMin.x > anchorMax.x)
        {
            float temp = anchorMin.x;
            anchorMin.x = anchorMax.x;
            anchorMax.x = temp;
            changed = true;
        }

        if (anchorMin.y > anchorMax.y)
        {
            float temp = anchorMin.y;
            anchorMin.y = anchorMax.y;
            anchorMax.y = temp;
            changed = true;
        }

        if (changed)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
        }

        return changed;
    }

    private static void AlignEquipmentPanelToModulePanelIfNeeded(Transform uiRoot, RectTransform equipmentPanelRect, bool forceAlign)
    {
        if (uiRoot == null || equipmentPanelRect == null)
        {
            return;
        }

        Transform modulePanelTransform = uiRoot.Find("ModulePanel");
        RectTransform modulePanelRect = modulePanelTransform != null ? modulePanelTransform.GetComponent<RectTransform>() : null;
        if (modulePanelRect == null)
        {
            return;
        }

        bool hasInvalidAnchors =
            equipmentPanelRect.anchorMin.x > equipmentPanelRect.anchorMax.x ||
            equipmentPanelRect.anchorMin.y > equipmentPanelRect.anchorMax.y;
        bool hasInvalidSize = equipmentPanelRect.sizeDelta.x <= 0f || equipmentPanelRect.sizeDelta.y <= 0f;
        bool shouldAutoAlign = forceAlign || hasInvalidAnchors || hasInvalidSize;
        if (!shouldAutoAlign)
        {
            return;
        }

        equipmentPanelRect.anchorMin = modulePanelRect.anchorMin;
        equipmentPanelRect.anchorMax = modulePanelRect.anchorMax;
        equipmentPanelRect.pivot = modulePanelRect.pivot;

        float moduleWidth = Mathf.Max(200f, modulePanelRect.sizeDelta.x);
        float moduleHeight = Mathf.Max(48f, modulePanelRect.sizeDelta.y);
        equipmentPanelRect.sizeDelta = new Vector2(moduleWidth * 1.95f, moduleHeight * 1.9f);
        equipmentPanelRect.anchoredPosition = new Vector2(
            modulePanelRect.anchoredPosition.x + moduleWidth + 26f,
            modulePanelRect.anchoredPosition.y);
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        return uiFactory.CreateImage(objectName, parent, squareSprite, color);
    }

    private TMP_Text CreateText(string objectName, Transform parent, string content, int fontSize, FontStyle fontStyle, Color color)
    {
        return uiFactory.CreateText(objectName, parent, uiFont, content, fontSize, fontStyle, color);
    }

    private void AddOutline(GameObject target, Color color)
    {
        uiFactory.AddOutline(target, color);
    }

    private Image CreateBar(Transform parent, Vector2 anchoredPosition, Color fillColor)
    {
        return uiFactory.CreateBar(parent, squareSprite, anchoredPosition, fillColor);
    }

    private Image CreateLabeledBar(Transform parent, string label, Vector2 anchoredPosition, Color fillColor)
    {
        return uiFactory.CreateLabeledBar(parent, squareSprite, uiFont, label, anchoredPosition, fillColor);
    }

    private TMP_Text CreateBarValueText(Image fillImage)
    {
        Transform parent = fillImage != null ? fillImage.transform.parent : null;
        if (parent == null)
        {
            return null;
        }

        TMP_Text valueText = CreateText("Value", parent, string.Empty, 10, FontStyle.Bold, Color.white);
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.raycastTarget = false;
        SetAnchoredRect(valueText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return valueText;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Image FindIndexedFill(Transform root, string backgroundName, int index)
    {
        Transform background = FindIndexedChild(root, backgroundName, index);
        Transform fill = background != null ? background.Find("Fill") : null;
        return fill != null ? fill.GetComponent<Image>() : null;
    }

    private static TMP_Text FindIndexedText(Transform root, string childName, int index)
    {
        Transform child = FindIndexedChild(root, childName, index);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindIndexedChild(Transform root, string childName, int index)
    {
        if (root == null)
        {
            return null;
        }

        int matchIndex = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name != childName)
            {
                continue;
            }

            if (matchIndex == index)
            {
                return child;
            }
            matchIndex++;
        }

        return null;
    }

    private void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect != null)
        {
            uiFactory.SetAnchoredRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        }
    }

    private void SetFillWidth(Image image, float percent, float maxWidth)
    {
        if (image != null)
        {
            uiFactory.SetFillWidth(image.rectTransform, percent, maxWidth);
        }
    }

    private void SetFillHeight(Image image, float percent, float maxHeight)
    {
        if (image != null)
        {
            uiFactory.SetFillHeight(image.rectTransform, percent, maxHeight);
        }
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }

    private static string FormatBarValue(float current, float max)
    {
        return Mathf.RoundToInt(Mathf.Max(0f, current)) + " / " + Mathf.RoundToInt(Mathf.Max(0f, max));
    }

    private sealed class EnemyRowView
    {
        public RectTransform RootTransform;
        public TMP_Text RootText;
        public Image ShieldFill;
        public Image ArmorFill;
        public Image HullFill;
        public EnemyShip Enemy;
    }
}

internal struct CombatHudContext
{
    public PlayerShip Player;
    public IReadOnlyList<EnemyShip> Enemies;
    public IReadOnlyList<ModuleState> Modules;
    public ShipEquipmentState EquipmentState;
    public TargetingController TargetingController;
    public int CurrentWave;
    public bool GameStarted;
    public bool GameOver;
    public bool LevelUpPending;
    public bool UseVirtualJoystick;
    public string ShipName;
}
