using System;
using System.Collections.Generic;
using System.Text;
using SpaceFrontier.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SpaceCombatSceneController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private GameObject engineParticlePrefab;

    [Header("UI References")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;

    private enum StartMenuPage
    {
        Main,
        Hangar,
        Settings
    }

    private enum LanguageOption
    {
        RU,
        ENG
    }

    private readonly List<EnemyShip> enemies = new List<EnemyShip>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<ModuleState> modules = new List<ModuleState>();
    private readonly List<string> combatLog = new List<string>();
    private readonly List<EnemyRow> enemyRows = new List<EnemyRow>();
    private readonly List<PerkChoice> activePerks = new List<PerkChoice>();
    private readonly List<ShipDefinition> starterShips = new List<ShipDefinition>();
    private readonly List<ShipCardView> shipCardViews = new List<ShipCardView>();
    private readonly List<UiButtonView> mainMenuButtons = new List<UiButtonView>();
    private readonly List<UiButtonView> settingsButtons = new List<UiButtonView>();
    private readonly List<AttackBeamEffect> attackBeams = new List<AttackBeamEffect>();
    private readonly List<EngineParticle> engineParticles = new List<EngineParticle>();
    private readonly List<StarVisual> stars = new List<StarVisual>();
    private readonly StringBuilder sharedBuilder = new StringBuilder(1024);
    private readonly int[] fpsOptions = { 60, 90, 120, 144 };

    private IPlatformService platformService;
    private ICombatService combatService;
    private IPoolService poolService;
    private ILocalizationService localizationService;
    private ISpaceCombatUiFactory uiFactory;

    private Camera mainCamera;
    private PlayerShip player;
    private EnemyShip targetEnemy;
    private Transform worldRoot;
    private Transform starRoot;
    private Transform enemyRoot;
    private Transform projectileRoot;
    private Transform gateTransform;

    private Canvas hudCanvas;
    private Text combatLogText;
    private Text gateHintText;
    private Text statusText;
    private Text overviewTitleText;
    private Text enemyHeaderText;
    private Text combatLogTitleText;
    private Text playerStatusTitleText;
    private Text targetNameText;
    private Text targetDistanceText;
    private Text targetDisplayText;
    private Text capacitorText;
    private Text levelText;
    private Text experienceText;
    private Text shipText;
    private Text perkTitleText;
    private Text perkHintText;
    private Text startMenuShipNameText;
    private Text startMenuRoleText;
    private Text startMenuDescriptionText;
    private Text startMenuStatsText;
    private Text startMenuHintText;
    private Text hangarTitleText;
    private Text hangarSubtitleText;
    private Text mainMenuTitleText;
    private Text mainMenuSubtitleText;
    private Text settingsTitleText;
    private Text settingsSubtitleText;
    private Text settingsLanguageLabelText;
    private Text settingsFpsLabelText;
    private Text joystickHintText;
    private Image targetPanel;
    private Image targetShieldFill;
    private Image targetArmorFill;
    private Image targetHullFill;
    private Image playerShieldFill;
    private Image playerArmorFill;
    private Image playerHullFill;
    private Image capacitorFill;
    private GameObject perkPanelObject;
    private GameObject startMenuObject;
    private GameObject mainMenuPanelObject;
    private GameObject hangarPanelObject;
    private GameObject settingsPanelObject;
    private GameObject joystickRootObject;
    private Image startMenuPreviewImage;
    private Image startButtonImage;
    private Text startButtonText;
    private RectTransform startButtonRect;
    private UiButtonView newGameButtonView;
    private UiButtonView settingsMenuButtonView;
    private UiButtonView exitButtonView;
    private UiButtonView hangarBackButtonView;
    private UiButtonView settingsBackButtonView;
    private UiButtonView languageRuButtonView;
    private UiButtonView languageEngButtonView;
    private UiButtonView[] fpsButtonViews = new UiButtonView[4];
    private RectTransform overviewPanelRect;
    private RectTransform modulePanelRect;
    private RectTransform joystickAreaRect;
    private Image joystickBaseImage;
    private Image joystickKnobImage;

    private Font uiFont;
    private Sprite squareSprite;
    private Sprite circleSprite;
    private Sprite ringSprite;
    private Sprite diamondSprite;

    private int wave = 1;
    private bool gateActive;
    private bool levelUpPending;
    private bool gameOver;
    private bool gameStarted;
    private Vector3 gatePosition;
    private int selectedShipIndex;
    private int selectedFpsIndex = 2;
    private LanguageOption currentLanguage = LanguageOption.RU;
    private StartMenuPage startMenuPage = StartMenuPage.Main;
    private bool useVirtualJoystick;
    private bool joystickDragging;
    private Vector2 joystickVector;

    internal void ConfigureServices(IPlatformService newPlatformService, ICombatService newCombatService, IPoolService newPoolService, ILocalizationService newLocalizationService, ISpaceCombatUiFactory newUiFactory)
    {
        platformService = newPlatformService;
        combatService = newCombatService;
        poolService = newPoolService;
        localizationService = newLocalizationService;
        uiFactory = newUiFactory;
    }

    private void EnsureServices()
    {
        platformService ??= new RuntimePlatformService();
        combatService ??= new CombatService();
        poolService ??= new PoolService();
        localizationService ??= new SpaceCombatLocalizationService();
        uiFactory ??= new SpaceCombatUiFactory();

        if (projectilePrefab != null)
        {
            poolService.InitializePool(projectilePrefab, 24);
        }
        if (beamPrefab != null)
        {
            poolService.InitializePool(beamPrefab, 16);
        }
        if (engineParticlePrefab != null)
        {
            poolService.InitializePool(engineParticlePrefab, 32);
        }
    }

    private void Awake()
    {
        EnsureServices();
        ValidateSerializedReferences();
        mainCamera = Camera.main;
        useVirtualJoystick = platformService.ShouldUseVirtualJoystick();
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        ConfigureCamera();
        CreateSprites();
        CreateStarterShips();
        CreateModules();
        BuildWorld();
        BuildHud();
        SpawnPlayer();
        SelectShip(0);
        ApplyPerformanceSettings();
        ShowStartMenu(true);
        RefreshLocalizedTexts();
        LogMessage(Localize("log_docked"));
        LogMessage(Localize("log_choose_hull"));
        UpdateHud();
    }

    private void ValidateSerializedReferences()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: projectilePrefab is not assigned.", this);
        }
        if (beamPrefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: beamPrefab is not assigned.", this);
        }
        if (engineParticlePrefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: engineParticlePrefab is not assigned.", this);
        }

        if (healthBar == null)
        {
            Debug.LogError("SpaceCombatSceneController: healthBar is not assigned.", this);
        }
        if (scoreText == null)
        {
            Debug.LogError("SpaceCombatSceneController: scoreText is not assigned.", this);
        }
        if (waveText == null)
        {
            Debug.LogError("SpaceCombatSceneController: waveText is not assigned.", this);
        }
    }

    private void Update()
    {
        if (!gameStarted)
        {
            HandleStartMenuInput();
            UpdateHud();
            return;
        }

        if (gameOver)
        {
            UpdateHud();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (levelUpPending)
        {
            UpdatePerkSelectionInput();
            UpdateHud();
            return;
        }

        HandleInput(deltaTime);
        UpdatePlayer(deltaTime);
        UpdateCombat(deltaTime);
        UpdateGate();
        UpdateEffects(deltaTime);
        UpdateVisuals();
        UpdateHud();
    }

    private void HandleStartMenuInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (startMenuPage == StartMenuPage.Hangar || startMenuPage == StartMenuPage.Settings)
                {
                    SetStartMenuPage(StartMenuPage.Main);
                }
            }

            if (startMenuPage == StartMenuPage.Hangar)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectShip(0);
                if (keyboard.digit2Key.wasPressedThisFrame && starterShips.Count > 1) SelectShip(1);
                if (keyboard.digit3Key.wasPressedThisFrame && starterShips.Count > 2) SelectShip(2);

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                {
                    StartRun();
                }
            }
            else if (startMenuPage == StartMenuPage.Settings)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetLanguage(LanguageOption.RU);
                if (keyboard.digit2Key.wasPressedThisFrame) SetLanguage(LanguageOption.ENG);
                if (keyboard.f1Key.wasPressedThisFrame) SetFpsIndex(0);
                if (keyboard.f2Key.wasPressedThisFrame) SetFpsIndex(1);
                if (keyboard.f3Key.wasPressedThisFrame) SetFpsIndex(2);
                if (keyboard.f4Key.wasPressedThisFrame) SetFpsIndex(3);
            }
        }

        Vector2 position;
        if (TryGetPrimaryPointerDown(out position))
        {
            if (startMenuPage == StartMenuPage.Main)
            {
                if (IsButtonClicked(newGameButtonView, position))
                {
                    SetStartMenuPage(StartMenuPage.Hangar);
                    return;
                }

                if (IsButtonClicked(settingsMenuButtonView, position))
                {
                    SetStartMenuPage(StartMenuPage.Settings);
                    return;
                }

                if (IsButtonClicked(exitButtonView, position))
                {
                    ExitGame();
                    return;
                }
            }
            else if (startMenuPage == StartMenuPage.Hangar)
            {
                for (int i = 0; i < shipCardViews.Count; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(shipCardViews[i].Rect, position, null))
                    {
                        SelectShip(i);
                        return;
                    }
                }

                if (startButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(startButtonRect, position, null))
                {
                    StartRun();
                    return;
                }

                if (IsButtonClicked(hangarBackButtonView, position))
                {
                    SetStartMenuPage(StartMenuPage.Main);
                    return;
                }
            }
            else if (startMenuPage == StartMenuPage.Settings)
            {
                if (IsButtonClicked(languageRuButtonView, position))
                {
                    SetLanguage(LanguageOption.RU);
                    return;
                }

                if (IsButtonClicked(languageEngButtonView, position))
                {
                    SetLanguage(LanguageOption.ENG);
                    return;
                }

                for (int i = 0; i < fpsButtonViews.Length; i++)
                {
                    if (IsButtonClicked(fpsButtonViews[i], position))
                    {
                        SetFpsIndex(i);
                        return;
                    }
                }

                if (IsButtonClicked(settingsBackButtonView, position))
                {
                    SetStartMenuPage(StartMenuPage.Main);
                    return;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (player != null && player.Transform != null && mainCamera != null)
        {
            Vector3 current = player.Transform.position;
            Vector3 lookAhead = new Vector3(player.Velocity.x, player.Velocity.y, 0f) * 0.15f;
            Vector3 targetPosition = new Vector3(current.x, current.y, -10f) + new Vector3(lookAhead.x, lookAhead.y, 0f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, 6f * Time.deltaTime);
        }
    }

    private void ConfigureCamera()
    {
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5.8f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.01f, 0.03f, 0.05f);
    }

    private string Localize(string key)
    {
        return localizationService.Localize(key, currentLanguage == LanguageOption.RU);
    }

    private void ApplyPerformanceSettings()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fpsOptions[Mathf.Clamp(selectedFpsIndex, 0, fpsOptions.Length - 1)];
    }

    private void SetLanguage(LanguageOption language)
    {
        currentLanguage = language;
        RefreshLocalizedTexts();
    }

    private void SetFpsIndex(int index)
    {
        selectedFpsIndex = Mathf.Clamp(index, 0, fpsOptions.Length - 1);
        ApplyPerformanceSettings();
        RefreshLocalizedTexts();
    }

    private void RefreshLocalizedTexts()
    {
        if (overviewTitleText != null) overviewTitleText.text = Localize("overview");
        if (enemyHeaderText != null) enemyHeaderText.text = Localize("enemy_header");
        if (combatLogTitleText != null) combatLogTitleText.text = Localize("combat_log");
        if (playerStatusTitleText != null) playerStatusTitleText.text = Localize("ship_status");
        if (gateHintText != null) gateHintText.text = Localize("warp_inactive");
        if (perkTitleText != null) perkTitleText.text = Localize("perk_title");
        if (joystickHintText != null) joystickHintText.text = Localize("joystick_hint");
        RefreshStartMenuTexts();
        RefreshSettingsButtons();
        UpdateStartMenuVisuals();
    }

    private string GetShipRoleText(ShipDefinition ship)
    {
        return localizationService.GetShipRoleText(ship, currentLanguage == LanguageOption.RU);
    }

    private string GetShipDescriptionText(ShipDefinition ship)
    {
        return localizationService.GetShipDescriptionText(ship, currentLanguage == LanguageOption.RU);
    }

    private void CreateSprites()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        squareSprite = CreateFilledSprite(2, 2, (x, y, size) => Color.white);
        circleSprite = CreateFilledSprite(64, 64, (x, y, size) =>
        {
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float radius = size * 0.48f;
            if (distance <= radius)
            {
                float alpha = Mathf.SmoothStep(1f, 0.75f, distance / radius);
                return new Color(1f, 1f, 1f, alpha);
            }

            return Color.clear;
        });
        ringSprite = CreateFilledSprite(64, 64, (x, y, size) =>
        {
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float radius = size * 0.48f;
            float innerRadius = size * 0.39f;
            return distance <= radius && distance >= innerRadius ? Color.white : Color.clear;
        });
        diamondSprite = CreateFilledSprite(64, 64, (x, y, size) =>
        {
            float center = (size - 1) * 0.5f;
            float normalized = (Mathf.Abs(x - center) + Mathf.Abs(y - center)) / center;
            return normalized <= 0.95f ? Color.white : Color.clear;
        });
    }

    private void CreateStarterShips()
    {
        starterShips.Clear();
        starterShips.Add(new ShipDefinition
        {
            Name = "Aegis",
            Role = "Balanced Frigate",
            Description = "Universal hull with reliable capacitor and solid survivability. Good first choice for learning the combat loop.",
            Speed = 6.5f,
            Acceleration = 11f,
            Drag = 1.6f,
            RotationResponsiveness = 8.5f,
            MaxShield = 430f,
            MaxArmor = 320f,
            MaxHull = 220f,
            MaxCapacitor = 1200f,
            CapacitorRechargeTime = 92f,
            DamageMultiplier = 1f,
            RepairMultiplier = 1f,
            AccentColor = new Color(0.28f, 0.6f, 0.94f, 1f),
            AuraColor = new Color(0.38f, 0.76f, 1f, 0.72f)
        });
        starterShips.Add(new ShipDefinition
        {
            Name = "Bulwark",
            Role = "Heavy Cruiser",
            Description = "Slow but durable platform with the best shields and armor. Repairs are stronger and capacitor is deep enough for long fights.",
            Speed = 5.4f,
            Acceleration = 8.2f,
            Drag = 1.95f,
            RotationResponsiveness = 6.6f,
            MaxShield = 580f,
            MaxArmor = 430f,
            MaxHull = 290f,
            MaxCapacitor = 1450f,
            CapacitorRechargeTime = 88f,
            DamageMultiplier = 0.94f,
            RepairMultiplier = 1.22f,
            AccentColor = new Color(0.18f, 0.78f, 0.8f, 1f),
            AuraColor = new Color(0.42f, 1f, 0.92f, 0.72f)
        });
        starterShips.Add(new ShipDefinition
        {
            Name = "Raptor",
            Role = "Strike Interceptor",
            Description = "Fast hunter with stronger volleys and snappier capacitor recovery. Lower defenses reward mobility and target focus.",
            Speed = 8f,
            Acceleration = 13.4f,
            Drag = 1.15f,
            RotationResponsiveness = 10.5f,
            MaxShield = 320f,
            MaxArmor = 210f,
            MaxHull = 170f,
            MaxCapacitor = 1050f,
            CapacitorRechargeTime = 72f,
            DamageMultiplier = 1.2f,
            RepairMultiplier = 0.9f,
            AccentColor = new Color(1f, 0.58f, 0.18f, 1f),
            AuraColor = new Color(1f, 0.75f, 0.36f, 0.72f)
        });
    }

    private void BuildWorld()
    {
        worldRoot = new GameObject("SpaceWorld").transform;
        enemyRoot = new GameObject("Enemies").transform;
        enemyRoot.SetParent(worldRoot, false);
        projectileRoot = new GameObject("Projectiles").transform;
        projectileRoot.SetParent(worldRoot, false);
        starRoot = new GameObject("Stars").transform;
        starRoot.SetParent(worldRoot, false);

        BuildStarfield();
        BuildGate();
    }

    private void BuildStarfield()
    {
        stars.Clear();
        UnityEngine.Random.InitState(1187);
        for (int i = 0; i < 220; i++)
        {
            GameObject star = new GameObject("Star_" + i);
            star.transform.SetParent(starRoot, false);
            star.transform.position = new Vector3(
                UnityEngine.Random.Range(-55f, 55f),
                UnityEngine.Random.Range(-45f, 45f),
                0f);

            SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
            renderer.sprite = circleSprite;
            renderer.color = Color.Lerp(
                new Color(0.5f, 0.7f, 1f, 0.5f),
                new Color(1f, 0.95f, 0.8f, 0.85f),
                UnityEngine.Random.value);

            float scale = UnityEngine.Random.Range(0.03f, 0.12f);
            star.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.sortingOrder = -20;
            stars.Add(new StarVisual
            {
                Transform = star.transform,
                Renderer = renderer,
                BaseAlpha = renderer.color.a,
                TwinkleSpeed = UnityEngine.Random.Range(0.7f, 1.9f),
                TwinkleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            });
        }

        for (int i = 0; i < 12; i++)
        {
            GameObject nebula = new GameObject("Nebula_" + i);
            nebula.transform.SetParent(starRoot, false);
            nebula.transform.position = new Vector3(
                UnityEngine.Random.Range(-40f, 40f),
                UnityEngine.Random.Range(-30f, 30f),
                0f);

            SpriteRenderer renderer = nebula.AddComponent<SpriteRenderer>();
            renderer.sprite = circleSprite;
            renderer.color = new Color(
                UnityEngine.Random.Range(0.05f, 0.15f),
                UnityEngine.Random.Range(0.22f, 0.42f),
                UnityEngine.Random.Range(0.25f, 0.45f),
                0.1f);

            float scale = UnityEngine.Random.Range(3.5f, 6.5f);
            nebula.transform.localScale = new Vector3(scale, scale * 0.7f, 1f);
            renderer.sortingOrder = -30;
        }
    }

    private void BuildGate()
    {
        gateTransform = new GameObject("WarpGate").transform;
        gateTransform.SetParent(worldRoot, false);

        SpriteRenderer ringRenderer = gateTransform.gameObject.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = ringSprite;
        ringRenderer.color = new Color(0.47f, 0.86f, 1f, 0.9f);
        ringRenderer.sortingOrder = 3;
        gateTransform.localScale = new Vector3(1.2f, 1.2f, 1f);
        gateTransform.gameObject.SetActive(false);

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(gateTransform, false);
        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = circleSprite;
        glowRenderer.color = new Color(0.18f, 0.55f, 0.85f, 0.18f);
        glowRenderer.sortingOrder = 2;
        glow.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
    }

    private void SpawnPlayer()
    {
        GameObject playerObject = new GameObject("PlayerShip");
        playerObject.transform.SetParent(worldRoot, false);

        SpriteRenderer bodyRenderer = playerObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = diamondSprite;
        bodyRenderer.color = new Color(0.24f, 0.56f, 0.92f, 1f);
        bodyRenderer.sortingOrder = 5;
        playerObject.transform.localScale = new Vector3(0.4f, 0.55f, 1f);

        GameObject aura = new GameObject("ShieldAura");
        aura.transform.SetParent(playerObject.transform, false);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = ringSprite;
        auraRenderer.color = new Color(0.35f, 0.72f, 1f, 0.7f);
        auraRenderer.sortingOrder = 4;
        aura.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        GameObject thruster = new GameObject("Thruster");
        thruster.transform.SetParent(playerObject.transform, false);
        thruster.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        SpriteRenderer thrusterRenderer = thruster.AddComponent<SpriteRenderer>();
        thrusterRenderer.sprite = circleSprite;
        thrusterRenderer.color = new Color(0.9f, 0.72f, 0.28f, 0.55f);
        thrusterRenderer.sortingOrder = 3;
        thruster.transform.localScale = new Vector3(0.35f, 0.7f, 1f);

        player = new PlayerShip
        {
            Transform = playerObject.transform,
            BodyRenderer = bodyRenderer,
            AuraRenderer = auraRenderer,
            ThrusterRenderer = thrusterRenderer,
            BaseBodyColor = bodyRenderer.color,
            BaseAuraColor = auraRenderer.color
        };
    }

    private void SelectShip(int index)
    {
        if (starterShips.Count == 0)
        {
            return;
        }

        selectedShipIndex = Mathf.Clamp(index, 0, starterShips.Count - 1);
        ApplyShipDefinition(starterShips[selectedShipIndex], false);
        UpdateStartMenuVisuals();
    }

    private void ApplyShipDefinition(ShipDefinition ship, bool resetProgress)
    {
        if (player == null)
        {
            return;
        }

        player.Speed = ship.Speed;
        player.Acceleration = ship.Acceleration;
        player.Drag = ship.Drag;
        player.RotationResponsiveness = ship.RotationResponsiveness;
        player.SpeedMultiplier = 1f;
        player.DamageMultiplier = ship.DamageMultiplier;
        player.RepairMultiplier = ship.RepairMultiplier;
        player.MaxShield = ship.MaxShield;
        player.Shield = ship.MaxShield;
        player.MaxArmor = ship.MaxArmor;
        player.Armor = ship.MaxArmor;
        player.MaxHull = ship.MaxHull;
        player.Hull = ship.MaxHull;
        player.MaxCapacitor = ship.MaxCapacitor;
        player.Capacitor = ship.MaxCapacitor;
        player.CapacitorRechargeTime = ship.CapacitorRechargeTime;
        player.Transform.position = Vector3.zero;
        player.Transform.rotation = Quaternion.identity;
        player.Velocity = Vector2.zero;
        player.MoveCommandActive = false;

        if (player.BodyRenderer != null)
        {
            player.BodyRenderer.color = ship.AccentColor;
            player.BaseBodyColor = ship.AccentColor;
        }

        if (player.AuraRenderer != null)
        {
            player.AuraRenderer.color = ship.AuraColor;
            player.BaseAuraColor = ship.AuraColor;
        }

        if (resetProgress)
        {
            player.Level = 1;
            player.Experience = 0;
            player.ExperienceToNext = 100;
        }
    }

    private void StartRun()
    {
        ShowStartMenu(false);
        gameStarted = true;
        gameOver = false;
        levelUpPending = false;
        wave = 1;
        gateActive = false;
        targetEnemy = null;
        activePerks.Clear();
        perkPanelObject.SetActive(false);
        combatLog.Clear();
        ClearEnemies();
        ClearProjectiles();
        if (gateTransform != null)
        {
            gateTransform.gameObject.SetActive(false);
        }
        if (gateHintText != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }
        ResetModules();
        ApplyShipDefinition(starterShips[selectedShipIndex], true);
        SpawnWave();
        LogMessage(Localize("log_launch") + starterShips[selectedShipIndex].Name);
        LogMessage(Localize("log_sector_scan"));
    }

    private void ResetModules()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].Active = false;
            modules[i].WeaponTimer = 0f;
            UpdateModuleVisual(modules[i]);
        }
    }

    private void ShowStartMenu(bool show)
    {
        if (startMenuObject != null)
        {
            startMenuObject.SetActive(show);
        }

        if (joystickRootObject != null)
        {
            joystickRootObject.SetActive(!show && useVirtualJoystick);
        }

        if (show)
        {
            SetStartMenuPage(StartMenuPage.Main);
        }
    }

    private void UpdateStartMenuVisuals()
    {
        if (starterShips.Count == 0 || selectedShipIndex < 0 || selectedShipIndex >= starterShips.Count)
        {
            return;
        }

        ShipDefinition ship = starterShips[selectedShipIndex];
        if (startMenuShipNameText != null)
        {
            startMenuShipNameText.text = ship.Name;
            startMenuRoleText.text = GetShipRoleText(ship);
            startMenuDescriptionText.text = GetShipDescriptionText(ship);
            startMenuStatsText.text =
                (currentLanguage == LanguageOption.RU
                    ? "РЎРєРѕСЂРѕСЃС‚СЊ: " + ship.Speed.ToString("0.0") +
                      "    Р©РёС‚: " + Mathf.RoundToInt(ship.MaxShield) +
                      "    Р‘СЂРѕРЅСЏ: " + Mathf.RoundToInt(ship.MaxArmor) +
                      "    РљРѕСЂРїСѓСЃ: " + Mathf.RoundToInt(ship.MaxHull) +
                      "\nР­РЅРµСЂРіРёСЏ: " + Mathf.RoundToInt(ship.MaxCapacitor) +
                      "    РџРµСЂРµР·Р°СЂСЏРґ: " + ship.CapacitorRechargeTime.ToString("0") + "СЃ" +
                      "    РЈСЂРѕРЅ: x" + ship.DamageMultiplier.ToString("0.00") +
                      "    Р РµРјРѕРЅС‚: x" + ship.RepairMultiplier.ToString("0.00")
                    : "Speed: " + ship.Speed.ToString("0.0") +
                      "    Shield: " + Mathf.RoundToInt(ship.MaxShield) +
                      "    Armor: " + Mathf.RoundToInt(ship.MaxArmor) +
                      "    Hull: " + Mathf.RoundToInt(ship.MaxHull) +
                      "\nCapacitor: " + Mathf.RoundToInt(ship.MaxCapacitor) +
                      "    Recharge: " + ship.CapacitorRechargeTime.ToString("0") + "s" +
                      "    Damage: x" + ship.DamageMultiplier.ToString("0.00") +
                      "    Repair: x" + ship.RepairMultiplier.ToString("0.00"));
            startMenuPreviewImage.color = ship.AccentColor;
        }

        if (startButtonImage != null)
        {
            startButtonImage.color = Color.Lerp(new Color(0.12f, 0.3f, 0.42f, 1f), ship.AccentColor, 0.45f);
            startButtonText.text = Localize("start_operation") + " " + ship.Name.ToUpperInvariant();
        }

        for (int i = 0; i < shipCardViews.Count; i++)
        {
            ShipDefinition cardShip = starterShips[i];
            bool isSelected = i == selectedShipIndex;
            shipCardViews[i].Background.color = isSelected
                ? new Color(0.12f, 0.26f, 0.36f, 1f)
                : new Color(0.06f, 0.12f, 0.17f, 0.96f);
            shipCardViews[i].Title.text = cardShip.Name + "\n<size=16>" + GetShipRoleText(cardShip) + "</size>";
            shipCardViews[i].Title.color = isSelected ? cardShip.AccentColor : Color.white;
            shipCardViews[i].Stats.text =
                (currentLanguage == LanguageOption.RU
                    ? "Р©РёС‚ " + Mathf.RoundToInt(cardShip.MaxShield) +
                      "  Р‘СЂРѕРЅСЏ " + Mathf.RoundToInt(cardShip.MaxArmor) +
                      "\nР­РЅРµСЂРіРёСЏ " + Mathf.RoundToInt(cardShip.MaxCapacitor) +
                      "  РЎРєРѕСЂРѕСЃС‚СЊ " + cardShip.Speed.ToString("0.0") +
                      "\nРЈСЂРѕРЅ x" + cardShip.DamageMultiplier.ToString("0.00")
                    : "Shield " + Mathf.RoundToInt(cardShip.MaxShield) +
                      "  Armor " + Mathf.RoundToInt(cardShip.MaxArmor) +
                      "\nCap " + Mathf.RoundToInt(cardShip.MaxCapacitor) +
                      "  Speed " + cardShip.Speed.ToString("0.0") +
                      "\nDamage x" + cardShip.DamageMultiplier.ToString("0.00"));
        }

        if (startMenuHintText != null)
        {
            startMenuHintText.text = useVirtualJoystick ? Localize("hangar_hint_mobile") : Localize("hangar_hint_desktop");
        }
    }

    private void CreateModules()
    {
        modules.Clear();
        modules.Add(new ModuleState
        {
            Name = "Pulse Laser",
            KeyLabel = "1",
            Type = ModuleType.Weapon,
            CapPerShot = 9f,
            RateOfFire = 0.45f,
            Damage = 28f,
            OptimalRange = 5.1f,
            FalloffRange = 3.2f
        });
        modules.Add(new ModuleState
        {
            Name = "Shield Rep",
            KeyLabel = "2",
            Type = ModuleType.ShieldRep,
            CapPerSecond = 7f,
            RepairPerSecond = 32f
        });
        modules.Add(new ModuleState
        {
            Name = "Armor Rep",
            KeyLabel = "3",
            Type = ModuleType.ArmorRep,
            CapPerSecond = 6f,
            RepairPerSecond = 24f
        });
        modules.Add(new ModuleState
        {
            Name = "Afterburn",
            KeyLabel = "4",
            Type = ModuleType.Afterburner,
            CapPerSecond = 5f,
            SpeedBonus = 1.55f
        });

        BindModuleSlots();
    }

    private void SpawnWave()
    {
        ClearEnemies();
        ClearProjectiles();
        gateActive = false;
        gateTransform.gameObject.SetActive(false);

        int enemyCount = Mathf.Clamp(3 + wave, 4, 9);
        string[] types = { "Scout", "Drone", "Raider", "Interceptor" };

        for (int i = 0; i < enemyCount; i++)
        {
            float angle = i * Mathf.PI * 2f / enemyCount + UnityEngine.Random.Range(-0.35f, 0.35f);
            float distance = UnityEngine.Random.Range(5.5f, 8.5f);
            Vector3 position = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
            EnemyShip enemy = CreateEnemy(
                "E-" + (i + 1).ToString("00"),
                types[UnityEngine.Random.Range(0, types.Length)],
                position,
                1f + (wave - 1) * 0.14f);
            enemies.Add(enemy);
        }

        targetEnemy = enemies.Count > 0 ? enemies[0] : null;
        UpdateTargetState();
        LogMessage(Localize("log_hostiles") + enemies.Count);
    }

    private EnemyShip CreateEnemy(string id, string typeName, Vector3 position, float levelScale)
    {
        GameObject enemyObject = new GameObject(id);
        enemyObject.transform.SetParent(enemyRoot, false);
        enemyObject.transform.position = position;
        enemyObject.transform.localScale = new Vector3(0.34f, 0.34f, 1f);

        SpriteRenderer bodyRenderer = enemyObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = circleSprite;
        bodyRenderer.color = new Color(0.75f, 0.2f, 0.24f, 1f);
        bodyRenderer.sortingOrder = 5;

        GameObject shieldObject = new GameObject("Shield");
        shieldObject.transform.SetParent(enemyObject.transform, false);
        SpriteRenderer shieldRenderer = shieldObject.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = ringSprite;
        shieldRenderer.color = new Color(0.25f, 0.68f, 1f, 0.9f);
        shieldRenderer.sortingOrder = 4;
        shieldObject.transform.localScale = new Vector3(1.22f, 1.22f, 1f);

        GameObject targetObject = new GameObject("TargetRing");
        targetObject.transform.SetParent(enemyObject.transform, false);
        SpriteRenderer targetRenderer = targetObject.AddComponent<SpriteRenderer>();
        targetRenderer.sprite = ringSprite;
        targetRenderer.color = new Color(1f, 0.88f, 0.42f, 1f);
        targetRenderer.sortingOrder = 6;
        targetObject.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        targetObject.SetActive(false);

        GameObject thrusterObject = new GameObject("Thruster");
        thrusterObject.transform.SetParent(enemyObject.transform, false);
        thrusterObject.transform.localPosition = new Vector3(0f, -0.72f, 0f);
        SpriteRenderer thrusterRenderer = thrusterObject.AddComponent<SpriteRenderer>();
        thrusterRenderer.sprite = circleSprite;
        thrusterRenderer.color = new Color(1f, 0.36f, 0.22f, 0.34f);
        thrusterRenderer.sortingOrder = 3;
        thrusterObject.transform.localScale = new Vector3(0.36f, 0.7f, 1f);

        return new EnemyShip
        {
            Id = id,
            Type = typeName,
            Transform = enemyObject.transform,
            BodyRenderer = bodyRenderer,
            ShieldRenderer = shieldRenderer,
            TargetRenderer = targetRenderer,
            ThrusterRenderer = thrusterRenderer,
            OrbitDistance = UnityEngine.Random.Range(2.4f, 4.2f),
            OrbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            OrbitSpeed = UnityEngine.Random.Range(0.4f, 0.95f),
            AttackCooldown = UnityEngine.Random.Range(1.15f, 1.8f),
            AttackTimer = UnityEngine.Random.Range(0f, 0.7f),
            Damage = 10f * levelScale,
            DriftSpeed = 1.5f + levelScale * 0.2f,
            MaxShield = Mathf.RoundToInt(150f * levelScale),
            Shield = Mathf.RoundToInt(150f * levelScale),
            MaxArmor = Mathf.RoundToInt(120f * levelScale),
            Armor = Mathf.RoundToInt(120f * levelScale),
            MaxHull = Mathf.RoundToInt(100f * levelScale),
            Hull = Mathf.RoundToInt(100f * levelScale),
            BaseBodyColor = bodyRenderer.color,
            BaseShieldColor = shieldRenderer.color
        };
    }

    private void ClearEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].Transform != null)
            {
                Destroy(enemies[i].Transform.gameObject);
            }
        }

        enemies.Clear();
    }

    private void ClearProjectiles()
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i].Transform != null)
            {
                if (projectilePrefab != null)
                {
                    poolService.Return(projectilePrefab, projectiles[i].Transform.gameObject);
                }
            }
        }

        projectiles.Clear();
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1400f, 900f);
        scaler.matchWidthOrHeight = 0.6f;

        Image rootBackground = CreateImage("Frame", canvasObject.transform, new Color(0.01f, 0.015f, 0.02f, 0f));
        RectTransform rootRect = rootBackground.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateRightOverviewPanel(canvasObject.transform);
        CreateCombatLogPanel(canvasObject.transform);
        CreateGateHint(canvasObject.transform);
        CreatePlayerStatusHud(canvasObject.transform);
        CreateModuleHud(canvasObject.transform);
        CreateStatusLabel(canvasObject.transform);
        CreatePerkPanel(canvasObject.transform);
        CreateStartMenu(canvasObject.transform);
        if (useVirtualJoystick)
        {
            CreateVirtualJoystick(canvasObject.transform);
        }
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

        enemyHeaderText = CreateText("EnemyHeader", panel.transform, "ID          TYPE        DIST       STATUS", 12, FontStyle.Bold, new Color(0.52f, 0.68f, 0.8f));
        SetAnchoredRect(enemyHeaderText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -176f), new Vector2(-10f, -196f));

        RectTransform rowsRoot = new GameObject("EnemyRows", typeof(RectTransform)).GetComponent<RectTransform>();
        rowsRoot.SetParent(panel.transform, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.sizeDelta = new Vector2(-18f, 430f);
        rowsRoot.anchoredPosition = new Vector2(0f, -204f);

        for (int i = 0; i < 9; i++)
        {
            EnemyRow row = CreateEnemyRow(rowsRoot, i);
            enemyRows.Add(row);
        }

        capacitorText = CreateText("CapText", panel.transform, "Capacitor: 100%", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(capacitorText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 78f), new Vector2(-10f, 102f));

        targetDisplayText = CreateText("TargetDisplay", panel.transform, "Target: none", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(targetDisplayText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 52f), new Vector2(-10f, 76f));

        shipText = CreateText("ShipText", panel.transform, "Ship: none", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(shipText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 26f), new Vector2(-10f, 50f));

        levelText = CreateText("LevelText", panel.transform, "Level: 1", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(levelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 0f), new Vector2(-10f, 24f));

        experienceText = CreateText("ExperienceText", panel.transform, "XP: 0 / 100", 15, FontStyle.Normal, new Color(0.88f, 0.92f, 1f));
        SetAnchoredRect(experienceText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, -24f), new Vector2(-10f, 0f));
    }

    private void CreateCombatLogPanel(Transform parent)
    {
        Image panel = CreateImage("CombatLog", parent, new Color(0f, 0.03f, 0.06f, 0.82f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(360f, 200f);
        rect.anchoredPosition = new Vector2(14f, 14f);
        AddOutline(panel.gameObject, new Color(0.16f, 0.34f, 0.48f, 1f));

        combatLogTitleText = CreateText("Label", panel.transform, "COMBAT LOG", 16, FontStyle.Bold, new Color(0.52f, 0.8f, 1f));
        SetAnchoredRect(combatLogTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f), new Vector2(-10f, -30f));

        combatLogText = CreateText("Text", panel.transform, string.Empty, 13, FontStyle.Normal, new Color(0.74f, 0.86f, 1f));
        combatLogText.alignment = TextAnchor.UpperLeft;
        combatLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
        combatLogText.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchoredRect(combatLogText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -36f));
    }

    private void CreateGateHint(Transform parent)
    {
        Image panel = CreateImage("GateHint", parent, new Color(0.04f, 0.11f, 0.17f, 0.88f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(470f, 34f);
        rect.anchoredPosition = new Vector2(20f, 226f);
        AddOutline(panel.gameObject, new Color(0.38f, 0.77f, 1f, 1f));

        gateHintText = CreateText("Text", panel.transform, "Warp gate inactive", 14, FontStyle.Bold, new Color(0.76f, 0.9f, 1f));
        SetAnchoredRect(gateHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 6f), new Vector2(-10f, -6f));
        panel.gameObject.SetActive(false);
    }

    private void CreatePlayerStatusHud(Transform parent)
    {
        Image panel = CreateImage("PlayerStatus", parent, new Color(0.03f, 0.07f, 0.1f, 0.86f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(320f, 118f);
        rect.anchoredPosition = new Vector2(-120f, 16f);
        AddOutline(panel.gameObject, new Color(0.15f, 0.32f, 0.44f, 1f));

        playerStatusTitleText = CreateText("Label", panel.transform, "SHIP STATUS", 16, FontStyle.Bold, new Color(0.85f, 0.92f, 1f));
        SetAnchoredRect(playerStatusTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -8f), new Vector2(-12f, -28f));

        playerShieldFill = CreateLabeledBar(panel.transform, "Shield", new Vector2(12f, -40f), new Color(0.23f, 0.62f, 1f));
        playerArmorFill = CreateLabeledBar(panel.transform, "Armor", new Vector2(12f, -64f), new Color(0.72f, 0.72f, 0.75f));
        playerHullFill = CreateLabeledBar(panel.transform, "Hull", new Vector2(12f, -88f), new Color(0.86f, 0.31f, 0.31f));

        Text capacitorLabel = CreateText("CapLabel", panel.transform, "Cap", 13, FontStyle.Bold, new Color(0.95f, 0.9f, 0.6f));
        SetAnchoredRect(capacitorLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-86f, 18f), new Vector2(-50f, 34f));
        capacitorFill = CreateImage("CapacitorFill", panel.transform, new Color(0.95f, 0.85f, 0.35f, 1f));
        RectTransform capRect = capacitorFill.rectTransform;
        capRect.anchorMin = new Vector2(1f, 0f);
        capRect.anchorMax = new Vector2(1f, 0f);
        capRect.pivot = new Vector2(0f, 0.5f);
        capRect.sizeDelta = new Vector2(30f, 60f);
        capRect.anchoredPosition = new Vector2(-44f, 46f);
        AddOutline(capacitorFill.gameObject, new Color(0.96f, 0.82f, 0.32f, 1f));
    }

    private void CreateModuleHud(Transform parent)
    {
        RectTransform root = new GameObject("ModulePanel", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        modulePanelRect = root;
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(360f, 100f);
        root.anchoredPosition = new Vector2(-310f, 18f);

        for (int i = 0; i < 4; i++)
        {
            Image slot = CreateImage("ModuleSlot_" + i, root, new Color(0.05f, 0.1f, 0.14f, 0.92f));
            RectTransform slotRect = slot.rectTransform;
            slotRect.anchorMin = new Vector2(0f, 0f);
            slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0f, 0f);
            slotRect.sizeDelta = new Vector2(78f, 78f);
            slotRect.anchoredPosition = new Vector2(i * 88f, 10f);
            AddOutline(slot.gameObject, new Color(0.19f, 0.36f, 0.48f, 1f));

            Text key = CreateText("Key", slot.transform, string.Empty, 14, FontStyle.Bold, Color.white);
            SetAnchoredRect(key.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, -28f));

            Text label = CreateText("Label", slot.transform, string.Empty, 11, FontStyle.Bold, new Color(0.84f, 0.92f, 1f));
            label.alignment = TextAnchor.MiddleCenter;
            SetAnchoredRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 24f), new Vector2(-6f, -24f));

            if (i < modules.Count)
            {
                modules[i].SlotImage = slot;
                modules[i].SlotKey = key;
                modules[i].SlotTitle = label;
            }
        }

        BindModuleSlots();
    }

    private void CreateVirtualJoystick(Transform parent)
    {
        joystickRootObject = new GameObject("VirtualJoystick", typeof(RectTransform));
        joystickRootObject.transform.SetParent(parent, false);
        RectTransform root = joystickRootObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image baseImage = CreateImage("Base", joystickRootObject.transform, new Color(0.08f, 0.15f, 0.22f, 0.45f));
        joystickAreaRect = baseImage.rectTransform;
        joystickAreaRect.anchorMin = new Vector2(0f, 0f);
        joystickAreaRect.anchorMax = new Vector2(0f, 0f);
        joystickAreaRect.pivot = new Vector2(0f, 0f);
        joystickAreaRect.sizeDelta = new Vector2(170f, 170f);
        joystickAreaRect.anchoredPosition = new Vector2(26f, 26f);
        AddOutline(baseImage.gameObject, new Color(0.32f, 0.62f, 0.86f, 0.95f));
        joystickBaseImage = baseImage;

        joystickKnobImage = CreateImage("Knob", baseImage.transform, new Color(0.62f, 0.86f, 1f, 0.8f));
        RectTransform knobRect = joystickKnobImage.rectTransform;
        knobRect.anchorMin = new Vector2(0.5f, 0.5f);
        knobRect.anchorMax = new Vector2(0.5f, 0.5f);
        knobRect.pivot = new Vector2(0.5f, 0.5f);
        knobRect.sizeDelta = new Vector2(74f, 74f);
        knobRect.anchoredPosition = Vector2.zero;

        joystickHintText = CreateText("Hint", baseImage.transform, Localize("joystick_hint"), 15, FontStyle.Bold, new Color(0.9f, 0.97f, 1f));
        joystickHintText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(joystickHintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(0f, -46f));
    }

    private void CreateStatusLabel(Transform parent)
    {
        statusText = CreateText("StatusLabel", parent, "Move: WASD  Select: LMB  Modules: 1-4  Warp: G", 15, FontStyle.Bold, new Color(0.88f, 0.94f, 1f));
        RectTransform rect = statusText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(700f, 28f);
        rect.anchoredPosition = new Vector2(-150f, -10f);
    }

    private void CreatePerkPanel(Transform parent)
    {
        perkPanelObject = new GameObject("PerkPanel", typeof(RectTransform));
        perkPanelObject.transform.SetParent(parent, false);
        RectTransform perkRootRect = perkPanelObject.GetComponent<RectTransform>();
        perkRootRect.anchorMin = Vector2.zero;
        perkRootRect.anchorMax = Vector2.one;
        perkRootRect.offsetMin = Vector2.zero;
        perkRootRect.offsetMax = Vector2.zero;

        Image dim = CreateImage("Dimmer", perkPanelObject.transform, new Color(0f, 0f, 0f, 0.45f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = CreateImage("Content", perkPanelObject.transform, new Color(0.05f, 0.11f, 0.16f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 250f);
        AddOutline(panel.gameObject, new Color(0.32f, 0.64f, 0.8f, 1f));

        perkTitleText = CreateText("Title", panel.transform, "LEVEL UP", 28, FontStyle.Bold, new Color(1f, 0.87f, 0.38f));
        SetAnchoredRect(perkTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, -56f));

        perkHintText = CreateText("Choices", panel.transform, string.Empty, 18, FontStyle.Bold, new Color(0.88f, 0.94f, 1f));
        perkHintText.alignment = TextAnchor.UpperLeft;
        SetAnchoredRect(perkHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 28f), new Vector2(-24f, -72f));

        perkPanelObject.SetActive(false);
    }

    private void CreateStartMenu(Transform parent)
    {
        startMenuObject = new GameObject("StartMenu", typeof(RectTransform));
        startMenuObject.transform.SetParent(parent, false);
        RectTransform startMenuRect = startMenuObject.GetComponent<RectTransform>();
        startMenuRect.anchorMin = Vector2.zero;
        startMenuRect.anchorMax = Vector2.one;
        startMenuRect.offsetMin = Vector2.zero;
        startMenuRect.offsetMax = Vector2.zero;
        Image dim = CreateImage("Dimmer", startMenuObject.transform, new Color(0f, 0f, 0f, 0.62f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = CreateImage("Panel", startMenuObject.transform, new Color(0.04f, 0.08f, 0.12f, 0.96f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 620f);
        AddOutline(panel.gameObject, new Color(0.2f, 0.42f, 0.58f, 1f));

        mainMenuPanelObject = new GameObject("MainMenuPanel", typeof(RectTransform));
        mainMenuPanelObject.transform.SetParent(panel.transform, false);
        StretchToParent(mainMenuPanelObject.GetComponent<RectTransform>());

        hangarPanelObject = new GameObject("HangarPanel", typeof(RectTransform));
        hangarPanelObject.transform.SetParent(panel.transform, false);
        StretchToParent(hangarPanelObject.GetComponent<RectTransform>());

        settingsPanelObject = new GameObject("SettingsPanel", typeof(RectTransform));
        settingsPanelObject.transform.SetParent(panel.transform, false);
        StretchToParent(settingsPanelObject.GetComponent<RectTransform>());

        CreateMainMenuPanel(mainMenuPanelObject.transform);
        CreateHangarPanel(hangarPanelObject.transform);
        CreateSettingsPanel(settingsPanelObject.transform);
        SetStartMenuPage(StartMenuPage.Main);
    }

    private void CreateMainMenuPanel(Transform parent)
    {
        mainMenuTitleText = CreateText("Title", parent, string.Empty, 42, FontStyle.Bold, new Color(0.88f, 0.95f, 1f));
        mainMenuTitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(mainMenuTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -80f), new Vector2(-40f, -130f));

        mainMenuSubtitleText = CreateText("Subtitle", parent, string.Empty, 20, FontStyle.Normal, new Color(0.62f, 0.82f, 0.98f));
        mainMenuSubtitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(mainMenuSubtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -132f), new Vector2(-60f, -170f));

        newGameButtonView = CreateMenuButton(parent, "main_new_game", new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(280f, 62f));
        settingsMenuButtonView = CreateMenuButton(parent, "main_settings", new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(280f, 62f));
        exitButtonView = CreateMenuButton(parent, "main_exit", new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(280f, 62f));
    }

    private void CreateHangarPanel(Transform parent)
    {
        hangarTitleText = CreateText("Title", parent, string.Empty, 34, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        hangarTitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(hangarTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -24f), new Vector2(-26f, -64f));

        hangarSubtitleText = CreateText("Subtitle", parent, string.Empty, 18, FontStyle.Normal, new Color(0.58f, 0.8f, 0.96f));
        hangarSubtitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(hangarSubtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -68f), new Vector2(-26f, -98f));

        RectTransform cardsRoot = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
        cardsRoot.SetParent(parent, false);
        cardsRoot.anchorMin = new Vector2(0f, 1f);
        cardsRoot.anchorMax = new Vector2(1f, 1f);
        cardsRoot.pivot = new Vector2(0.5f, 1f);
        cardsRoot.sizeDelta = new Vector2(-60f, 210f);
        cardsRoot.anchoredPosition = new Vector2(0f, -124f);

        shipCardViews.Clear();
        for (int i = 0; i < starterShips.Count; i++)
        {
            shipCardViews.Add(CreateShipCard(cardsRoot, i));
        }

        Image infoPanel = CreateImage("InfoPanel", parent, new Color(0.05f, 0.11f, 0.16f, 0.98f));
        RectTransform infoRect = infoPanel.rectTransform;
        infoRect.anchorMin = new Vector2(0.5f, 0f);
        infoRect.anchorMax = new Vector2(0.5f, 0f);
        infoRect.pivot = new Vector2(0.5f, 0f);
        infoRect.sizeDelta = new Vector2(900f, 240f);
        infoRect.anchoredPosition = new Vector2(0f, 104f);
        AddOutline(infoPanel.gameObject, new Color(0.16f, 0.34f, 0.48f, 1f));

        startMenuPreviewImage = CreateImage("Preview", infoPanel.transform, Color.white);
        RectTransform previewRect = startMenuPreviewImage.rectTransform;
        previewRect.anchorMin = new Vector2(0f, 0.5f);
        previewRect.anchorMax = new Vector2(0f, 0.5f);
        previewRect.pivot = new Vector2(0f, 0.5f);
        previewRect.sizeDelta = new Vector2(150f, 150f);
        previewRect.anchoredPosition = new Vector2(28f, 0f);
        startMenuPreviewImage.sprite = diamondSprite;

        startMenuShipNameText = CreateText("ShipName", infoPanel.transform, "-", 28, FontStyle.Bold, Color.white);
        SetAnchoredRect(startMenuShipNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -22f), new Vector2(-26f, -58f));

        startMenuRoleText = CreateText("Role", infoPanel.transform, "-", 18, FontStyle.Bold, new Color(0.7f, 0.88f, 1f));
        SetAnchoredRect(startMenuRoleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -58f), new Vector2(-26f, -90f));

        startMenuDescriptionText = CreateText("Description", infoPanel.transform, "-", 16, FontStyle.Normal, new Color(0.86f, 0.92f, 1f));
        startMenuDescriptionText.alignment = TextAnchor.UpperLeft;
        startMenuDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        startMenuDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchoredRect(startMenuDescriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -96f), new Vector2(-26f, -156f));

        startMenuStatsText = CreateText("Stats", infoPanel.transform, "-", 15, FontStyle.Normal, new Color(0.92f, 0.95f, 1f));
        startMenuStatsText.alignment = TextAnchor.UpperLeft;
        SetAnchoredRect(startMenuStatsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(200f, 24f), new Vector2(-26f, 84f));

        startMenuHintText = CreateText("Hint", parent, string.Empty, 16, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        startMenuHintText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(startMenuHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 74f), new Vector2(-26f, 102f));

        startButtonImage = CreateImage("StartButton", parent, new Color(0.12f, 0.3f, 0.42f, 1f));
        startButtonRect = startButtonImage.rectTransform;
        startButtonRect.anchorMin = new Vector2(0.5f, 0f);
        startButtonRect.anchorMax = new Vector2(0.5f, 0f);
        startButtonRect.pivot = new Vector2(0.5f, 0f);
        startButtonRect.sizeDelta = new Vector2(260f, 54f);
        startButtonRect.anchoredPosition = new Vector2(130f, 18f);
        AddOutline(startButtonImage.gameObject, new Color(0.52f, 0.82f, 1f, 1f));

        startButtonText = CreateText("Label", startButtonImage.transform, string.Empty, 20, FontStyle.Bold, Color.white);
        startButtonText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(startButtonText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));

        hangarBackButtonView = CreateMenuButton(parent, "hangar_back", new Vector2(0.5f, 0f), new Vector2(-130f, 18f), new Vector2(220f, 54f));
    }

    private void CreateSettingsPanel(Transform parent)
    {
        settingsTitleText = CreateText("Title", parent, string.Empty, 34, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        settingsTitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(settingsTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -36f), new Vector2(-26f, -76f));

        settingsSubtitleText = CreateText("Subtitle", parent, string.Empty, 18, FontStyle.Normal, new Color(0.58f, 0.8f, 0.96f));
        settingsSubtitleText.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(settingsSubtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -84f), new Vector2(-26f, -116f));

        Image settingsBox = CreateImage("SettingsBox", parent, new Color(0.05f, 0.11f, 0.16f, 0.98f));
        RectTransform boxRect = settingsBox.rectTransform;
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(760f, 300f);
        boxRect.anchoredPosition = new Vector2(0f, 10f);
        AddOutline(settingsBox.gameObject, new Color(0.16f, 0.34f, 0.48f, 1f));

        settingsLanguageLabelText = CreateText("LanguageLabel", settingsBox.transform, string.Empty, 22, FontStyle.Bold, Color.white);
        SetAnchoredRect(settingsLanguageLabelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -32f), new Vector2(-28f, -70f));

        languageRuButtonView = CreateMenuButton(settingsBox.transform, "lang_ru", new Vector2(0f, 1f), new Vector2(28f, -108f), new Vector2(140f, 50f));
        languageRuButtonView.Rect.anchorMax = new Vector2(0f, 1f);
        languageRuButtonView.Rect.pivot = new Vector2(0f, 1f);
        languageEngButtonView = CreateMenuButton(settingsBox.transform, "lang_eng", new Vector2(0f, 1f), new Vector2(186f, -108f), new Vector2(140f, 50f));
        languageEngButtonView.Rect.anchorMax = new Vector2(0f, 1f);
        languageEngButtonView.Rect.pivot = new Vector2(0f, 1f);

        settingsFpsLabelText = CreateText("FpsLabel", settingsBox.transform, string.Empty, 22, FontStyle.Bold, Color.white);
        SetAnchoredRect(settingsFpsLabelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -164f), new Vector2(-28f, -202f));

        for (int i = 0; i < fpsOptions.Length; i++)
        {
            UiButtonView button = CreateMenuButton(settingsBox.transform, "fps_" + fpsOptions[i], new Vector2(0f, 1f), new Vector2(28f + i * 164f, -240f), new Vector2(140f, 50f));
            button.Rect.anchorMax = new Vector2(0f, 1f);
            button.Rect.pivot = new Vector2(0f, 1f);
            fpsButtonViews[i] = button;
        }

        settingsBackButtonView = CreateMenuButton(parent, "settings_back", new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(240f, 54f));
    }

    private UiButtonView CreateMenuButton(Transform parent, string id, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        Image buttonImage = CreateImage(id, parent, new Color(0.08f, 0.16f, 0.22f, 0.98f));
        RectTransform rect = buttonImage.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        AddOutline(buttonImage.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        Text label = CreateText("Label", buttonImage.transform, id, 20, FontStyle.Bold, Color.white);
        label.alignment = TextAnchor.MiddleCenter;
        SetAnchoredRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        return new UiButtonView
        {
            Id = id,
            Rect = rect,
            Background = buttonImage,
            Label = label
        };
    }

    private void SetStartMenuPage(StartMenuPage page)
    {
        startMenuPage = page;
        if (mainMenuPanelObject != null) mainMenuPanelObject.SetActive(page == StartMenuPage.Main);
        if (hangarPanelObject != null) hangarPanelObject.SetActive(page == StartMenuPage.Hangar);
        if (settingsPanelObject != null) settingsPanelObject.SetActive(page == StartMenuPage.Settings);
        RefreshLocalizedTexts();
    }

    private void RefreshStartMenuTexts()
    {
        if (mainMenuTitleText != null) mainMenuTitleText.text = Localize("main_title");
        if (mainMenuSubtitleText != null) mainMenuSubtitleText.text = Localize("main_subtitle");
        if (newGameButtonView != null) newGameButtonView.Label.text = Localize("menu_new_game");
        if (settingsMenuButtonView != null) settingsMenuButtonView.Label.text = Localize("menu_settings");
        if (exitButtonView != null) exitButtonView.Label.text = Localize("menu_exit");

        if (hangarTitleText != null) hangarTitleText.text = Localize("hangar_title");
        if (hangarSubtitleText != null) hangarSubtitleText.text = Localize("hangar_subtitle");
        if (hangarBackButtonView != null) hangarBackButtonView.Label.text = Localize("back");
        if (startButtonText != null) startButtonText.text = Localize("start_operation");
        if (startMenuHintText != null)
        {
            startMenuHintText.text = useVirtualJoystick ? Localize("hangar_hint_mobile") : Localize("hangar_hint_desktop");
        }

        if (settingsTitleText != null) settingsTitleText.text = Localize("settings_title");
        if (settingsSubtitleText != null) settingsSubtitleText.text = Localize("settings_subtitle");
        if (settingsLanguageLabelText != null) settingsLanguageLabelText.text = Localize("settings_language");
        if (settingsFpsLabelText != null) settingsFpsLabelText.text = Localize("settings_fps");
        if (settingsBackButtonView != null) settingsBackButtonView.Label.text = Localize("back");
        if (languageRuButtonView != null) languageRuButtonView.Label.text = Localize("lang_ru");
        if (languageEngButtonView != null) languageEngButtonView.Label.text = Localize("lang_eng");
        for (int i = 0; i < fpsButtonViews.Length; i++)
        {
            if (fpsButtonViews[i] != null)
            {
                fpsButtonViews[i].Label.text = fpsOptions[i].ToString();
            }
        }
    }

    private void RefreshSettingsButtons()
    {
        UpdateButtonState(languageRuButtonView, currentLanguage == LanguageOption.RU, new Color(0.45f, 0.72f, 1f, 1f));
        UpdateButtonState(languageEngButtonView, currentLanguage == LanguageOption.ENG, new Color(0.45f, 0.72f, 1f, 1f));
        for (int i = 0; i < fpsButtonViews.Length; i++)
        {
            UpdateButtonState(fpsButtonViews[i], i == selectedFpsIndex, new Color(1f, 0.7f, 0.36f, 1f));
        }
    }

    private void UpdateButtonState(UiButtonView button, bool active, Color accent)
    {
        if (button == null)
        {
            return;
        }

        button.Background.color = active ? Color.Lerp(new Color(0.08f, 0.16f, 0.22f, 1f), accent, 0.55f) : new Color(0.08f, 0.16f, 0.22f, 0.98f);
        button.Label.color = active ? Color.white : new Color(0.88f, 0.94f, 1f);
    }

    private bool IsButtonClicked(UiButtonView button, Vector2 screenPosition)
    {
        return button != null && RectTransformUtility.RectangleContainsScreenPoint(button.Rect, screenPosition, null);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private ShipCardView CreateShipCard(RectTransform parent, int index)
    {
        Image background = CreateImage("ShipCard_" + index, parent, new Color(0.06f, 0.12f, 0.17f, 0.96f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(280f, 170f);
        rect.anchoredPosition = new Vector2(index * 292f, 0f);
        AddOutline(background.gameObject, new Color(0.14f, 0.28f, 0.38f, 1f));

        Text title = CreateText("Title", background.transform, "-", 22, FontStyle.Bold, Color.white);
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -18f), new Vector2(-14f, -54f));

        Text stats = CreateText("Stats", background.transform, "-", 15, FontStyle.Normal, new Color(0.8f, 0.9f, 1f));
        stats.alignment = TextAnchor.UpperLeft;
        stats.horizontalOverflow = HorizontalWrapMode.Wrap;
        stats.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchoredRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -58f), new Vector2(-14f, -148f));

        return new ShipCardView
        {
            Rect = rect,
            Background = background,
            Title = title,
            Stats = stats
        };
    }

    private void BindModuleSlots()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i].SlotImage == null)
            {
                continue;
            }

            modules[i].SlotKey.text = "[" + modules[i].KeyLabel + "]";
            modules[i].SlotTitle.text = modules[i].Name;
            UpdateModuleVisual(modules[i]);
        }
    }

    private EnemyRow CreateEnemyRow(RectTransform parent, int index)
    {
        Image rowBackground = CreateImage("EnemyRow_" + index, parent, new Color(0.06f, 0.11f, 0.16f, 0.95f));
        RectTransform rowRect = rowBackground.rectTransform;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 46f);
        rowRect.anchoredPosition = new Vector2(0f, -index * 52f);
        AddOutline(rowBackground.gameObject, new Color(0.12f, 0.24f, 0.34f, 1f));

        Text text = CreateText("RowText", rowBackground.transform, string.Empty, 12, FontStyle.Bold, new Color(0.85f, 0.92f, 1f));
        SetAnchoredRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -18f));

        Image shieldFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -24f), new Color(0.23f, 0.62f, 1f));
        Image armorFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -32f), new Color(0.72f, 0.72f, 0.75f));
        Image hullFill = CreateMiniBar(rowBackground.transform, new Vector2(132f, -40f), new Color(0.86f, 0.31f, 0.31f));

        return new EnemyRow
        {
            RootTransform = rowRect,
            RootText = text,
            ShieldFill = shieldFill,
            ArmorFill = armorFill,
            HullFill = hullFill
        };
    }

    private Image CreateMiniBar(Transform parent, Vector2 anchoredPosition, Color fillColor)
    {
        Image background = CreateImage("BarBg", parent, new Color(0.12f, 0.17f, 0.2f, 1f));
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0f, 1f);
        bgRect.sizeDelta = new Vector2(110f, 4f);
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

    private void HandleInput(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        UpdateVirtualJoystick();

        Vector2 moveInput = GetMovementVector(keyboard);
        if (moveInput.sqrMagnitude > 0.01f)
        {
            player.MoveCommandActive = false;
        }

        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition))
        {
            if (TrySelectEnemyFromOverview(pointerPosition))
            {
                return;
            }

            if (!IsGameplayHudBlocked(pointerPosition))
            {
                IssueMoveCommand(pointerPosition);
            }
        }

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ToggleModule(0);
            if (keyboard.digit2Key.wasPressedThisFrame) ToggleModule(1);
            if (keyboard.digit3Key.wasPressedThisFrame) ToggleModule(2);
            if (keyboard.digit4Key.wasPressedThisFrame) ToggleModule(3);

            if (keyboard.gKey.wasPressedThisFrame && gateActive)
            {
                float gateDistance = Vector3.Distance(player.Transform.position, gatePosition);
                if (gateDistance <= 2f)
                {
                    WarpToNextWave();
                }
                else
                {
                    LogMessage(Localize("log_move_gate"), "warning");
                }
            }
        }

        Vector2 thrust = moveInput;
        if (thrust.sqrMagnitude <= 0.01f && player.MoveCommandActive)
        {
            Vector2 toTarget = (Vector2)(player.MoveCommandTarget - player.Transform.position);
            float distance = toTarget.magnitude;
            float brakingDistance = Mathf.Max(0.35f, player.Velocity.sqrMagnitude / Mathf.Max(0.1f, player.Acceleration * 2f));
            if (distance <= 0.25f && player.Velocity.magnitude <= 0.2f)
            {
                player.MoveCommandActive = false;
                player.Velocity = Vector2.Lerp(player.Velocity, Vector2.zero, 0.6f);
            }
            else
            {
                float desiredMagnitude = distance <= brakingDistance
                    ? Mathf.Clamp01(distance / brakingDistance)
                    : 1f;
                thrust = toTarget.normalized * desiredMagnitude;
            }
        }

        if (thrust.sqrMagnitude > 1f)
        {
            thrust.Normalize();
        }

        player.Velocity += thrust * (player.Acceleration * player.SpeedMultiplier) * deltaTime;
        float maxSpeed = player.Speed * player.SpeedMultiplier;
        player.Velocity = Vector2.ClampMagnitude(player.Velocity, maxSpeed);
        float damping = Mathf.Clamp01(player.Drag * deltaTime);
        player.Velocity = Vector2.Lerp(player.Velocity, Vector2.zero, damping);
        player.Transform.position += (Vector3)(player.Velocity * deltaTime);

        if (player.Velocity.sqrMagnitude > 0.02f)
        {
            float angle = Mathf.Atan2(player.Velocity.y, player.Velocity.x) * Mathf.Rad2Deg - 90f;
            player.Transform.rotation = Quaternion.Lerp(
                player.Transform.rotation,
                Quaternion.Euler(0f, 0f, angle),
                player.RotationResponsiveness * deltaTime);
        }
    }

    private void ToggleModule(int index)
    {
        if (index < 0 || index >= modules.Count)
        {
            return;
        }

        ModuleState module = modules[index];
        module.Active = !module.Active;

        if (module.Type == ModuleType.Afterburner && !module.Active)
        {
            player.SpeedMultiplier = 1f;
        }

        UpdateModuleVisual(module);
        LogMessage(module.Name + (module.Active ? Localize("log_module_on") : Localize("log_module_off")));
    }

    private Vector2 GetMovementVector(Keyboard keyboard)
    {
        Vector2 moveInput = joystickVector;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;
        }

        return moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
    }

    private void IssueMoveCommand(Vector2 screenPosition)
    {
        Vector3 world = mainCamera.ScreenToWorldPoint(screenPosition);
        world.z = 0f;
        player.MoveCommandActive = true;
        player.MoveCommandTarget = world;
    }

    private bool TrySelectEnemyFromOverview(Vector2 screenPosition)
    {
        for (int i = 0; i < enemyRows.Count; i++)
        {
            EnemyRow row = enemyRows[i];
            if (!row.RootTransform.gameObject.activeSelf || row.RootText == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(row.RootTransform, screenPosition, null))
            {
                EnemyShip enemy = row.Enemy;
                if (enemy != null && enemy.IsAlive())
                {
                    targetEnemy = enemy;
                    UpdateTargetState();
                    LogMessage(Localize("log_target_locked") + enemy.Id);
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsGameplayHudBlocked(Vector2 screenPosition)
    {
        if (overviewPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(overviewPanelRect, screenPosition, null))
        {
            return true;
        }

        if (modulePanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(modulePanelRect, screenPosition, null))
        {
            return true;
        }

        return false;
    }

    private void UpdateVirtualJoystick()
    {
        if (!useVirtualJoystick || joystickAreaRect == null)
        {
            joystickVector = Vector2.zero;
            return;
        }

        Vector2 pointerPosition = Vector2.zero;
        bool pressed = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            pressed = true;
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            pressed = true;
            pointerPosition = Pointer.current.position.ReadValue();
        }

        if (!pressed)
        {
            joystickDragging = false;
            joystickVector = Vector2.zero;
            if (joystickKnobImage != null)
            {
                joystickKnobImage.rectTransform.anchoredPosition = Vector2.zero;
            }
            return;
        }

        bool insideJoystick = RectTransformUtility.RectangleContainsScreenPoint(joystickAreaRect, pointerPosition, null);
        if (!joystickDragging && !insideJoystick)
        {
            joystickVector = Vector2.zero;
            return;
        }

        joystickDragging = true;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickAreaRect, pointerPosition, null, out localPoint);
        Vector2 halfSize = joystickAreaRect.sizeDelta * 0.5f;
        Vector2 normalized = new Vector2(localPoint.x / halfSize.x, localPoint.y / halfSize.y);
        joystickVector = Vector2.ClampMagnitude(normalized, 1f);
        if (joystickKnobImage != null)
        {
            joystickKnobImage.rectTransform.anchoredPosition = joystickVector * 42f;
        }
    }

    private bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private void UpdatePlayer(float deltaTime)
    {
        player.UpdateCapacitor(deltaTime);
        player.HitFlashTimer = Mathf.Max(0f, player.HitFlashTimer - deltaTime * 3.6f);
        player.ThrusterPulse += deltaTime * (2.4f + player.Velocity.magnitude * 0.4f);
        if (!player.IsAlive())
        {
            gameOver = true;
            LogMessage(Localize("log_hull_breach"), "critical");
            statusText.text = Localize("status_gameover");
        }

        if (player.Velocity.magnitude > 0.65f)
        {
            SpawnEngineParticle(player.Transform.position - player.Transform.up * 0.42f, -player.Transform.up, player.BaseAuraColor);
        }
    }

    private void UpdateCombat(float deltaTime)
    {
        CombatUpdateContext context = new CombatUpdateContext
        {
            Player = player,
            Enemies = enemies,
            Projectiles = projectiles,
            Modules = modules,
            TargetEnemy = targetEnemy,
            ProjectileRoot = projectileRoot,
            ProjectilePrefab = projectilePrefab,
            PoolService = poolService,
            Wave = wave,
            Localize = Localize,
            LogMessage = LogMessage,
            UpdateModuleVisual = UpdateModuleVisual,
            CreateAttackBeam = CreateAttackBeam
        };

        CombatUpdateResult result = combatService.UpdateFrame(context, deltaTime);
        targetEnemy = result.TargetEnemy;

        if (result.LevelUpRequested)
        {
            BeginLevelUp();
        }
    }

    private void BeginLevelUp()
    {
        player.Level++;
        player.Experience -= player.ExperienceToNext;
        player.ExperienceToNext = Mathf.RoundToInt(player.ExperienceToNext * 1.5f);

        player.MaxShield += 50f;
        player.MaxArmor += 40f;
        player.MaxHull += 30f;
        player.Shield = player.MaxShield;
        player.Armor = player.MaxArmor;
        player.Hull = player.MaxHull;

        levelUpPending = true;
        perkPanelObject.SetActive(true);
        activePerks.Clear();

        List<PerkChoice> pool = new List<PerkChoice>
        {
            new PerkChoice { Label = Localize("perk_damage"), Apply = () => player.DamageMultiplier += 0.15f },
            new PerkChoice
            {
                Label = Localize("perk_capacitor"),
                Apply = () =>
                {
                    player.MaxCapacitor = Mathf.Round(player.MaxCapacitor * 1.2f);
                    player.Capacitor = player.MaxCapacitor;
                }
            },
            new PerkChoice
            {
                Label = Localize("perk_shield"),
                Apply = () =>
                {
                    player.MaxShield = Mathf.Round(player.MaxShield * 1.25f);
                    player.Shield = player.MaxShield;
                }
            },
            new PerkChoice { Label = Localize("perk_speed"), Apply = () => player.Speed += 1.1f },
            new PerkChoice { Label = Localize("perk_repair"), Apply = () => player.RepairMultiplier += 0.3f }
        };

        while (activePerks.Count < 3 && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            activePerks.Add(pool[index]);
            pool.RemoveAt(index);
        }

        sharedBuilder.Length = 0;
        for (int i = 0; i < activePerks.Count; i++)
        {
            sharedBuilder.Append(i + 1);
            sharedBuilder.Append(". ");
            sharedBuilder.Append(activePerks[i].Label);
            sharedBuilder.AppendLine();
            sharedBuilder.AppendLine();
        }

        sharedBuilder.Append(Localize("perk_pick"));
        perkHintText.text = sharedBuilder.ToString();
        LogMessage(Localize("log_levelup"), "warning");
    }

    private void UpdatePerkSelectionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) ApplyPerk(0);
        if (keyboard.digit2Key.wasPressedThisFrame) ApplyPerk(1);
        if (keyboard.digit3Key.wasPressedThisFrame) ApplyPerk(2);
    }

    private void ApplyPerk(int index)
    {
        if (index < 0 || index >= activePerks.Count)
        {
            return;
        }

        activePerks[index].Apply?.Invoke();
        levelUpPending = false;
        perkPanelObject.SetActive(false);
        LogMessage(Localize("log_perk_selected") + activePerks[index].Label, "warning");
        activePerks.Clear();
    }

    private void UpdateGate()
    {
        if (enemies.Count == 0 && !gateActive)
        {
            gateActive = true;
            gatePosition = player.Transform.position + new Vector3(2.1f, 0.8f, 0f);
            gateTransform.position = gatePosition;
            gateTransform.gameObject.SetActive(true);
            gateHintText.transform.parent.gameObject.SetActive(true);
            gateHintText.text = Localize("warp_active");
            LogMessage(Localize("log_warp_active"), "critical");
        }

        if (gateActive)
        {
            gateTransform.Rotate(0f, 0f, 45f * Time.deltaTime);
        }
    }

    private void WarpToNextWave()
    {
        wave++;
        player.Shield = player.MaxShield;
        player.Armor = player.MaxArmor;
        player.Hull = player.MaxHull;
        player.Capacitor = player.MaxCapacitor;
        gateHintText.transform.parent.gameObject.SetActive(false);
        LogMessage(Localize("log_warp_sector") + wave, "critical");
        SpawnWave();
    }

    private void UpdateEffects(float deltaTime)
    {
        for (int i = attackBeams.Count - 1; i >= 0; i--)
        {
            AttackBeamEffect beam = attackBeams[i];
            beam.Lifetime += deltaTime;
            float t = beam.Lifetime / beam.Duration;
            Color color = beam.Renderer.color;
            color.a = Mathf.Lerp(0.9f, 0f, t);
            beam.Renderer.color = color;
            beam.Transform.localScale = new Vector3(beam.Transform.localScale.x, Mathf.Lerp(0.18f, 0.05f, t), 1f);
            if (beam.Lifetime >= beam.Duration)
            {
                if (beamPrefab != null)
                {
                    poolService.Return(beamPrefab, beam.Transform.gameObject);
                }
                attackBeams.RemoveAt(i);
            }
        }

        for (int i = engineParticles.Count - 1; i >= 0; i--)
        {
            EngineParticle particle = engineParticles[i];
            particle.Lifetime += deltaTime;
            particle.Transform.position += particle.Velocity * deltaTime;
            float t = particle.Lifetime / particle.Duration;
            Color color = particle.BaseColor;
            color.a = Mathf.Lerp(particle.BaseColor.a, 0f, t);
            particle.Renderer.color = color;
            particle.Transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.04f, t);
            if (particle.Lifetime >= particle.Duration)
            {
                if (engineParticlePrefab != null)
                {
                    poolService.Return(engineParticlePrefab, particle.Transform.gameObject);
                }
                engineParticles.RemoveAt(i);
            }
        }
    }

    private void CreateAttackBeam(Vector3 start, Vector3 end, Color color)
    {
        if (beamPrefab == null)
        {
            return;
        }

        GameObject beamObject = poolService.Get(beamPrefab, projectileRoot);
        if (beamObject == null)
        {
            return;
        }
        Vector3 delta = end - start;
        float length = Mathf.Max(0.2f, delta.magnitude);
        beamObject.transform.position = (start + end) * 0.5f;
        beamObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        beamObject.transform.localScale = new Vector3(length, 0.18f, 1f);

        SpriteRenderer renderer = beamObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = beamObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = 7;

        attackBeams.Add(new AttackBeamEffect
        {
            Transform = beamObject.transform,
            Renderer = renderer,
            Duration = 0.16f
        });
    }

    private void SpawnEngineParticle(Vector3 position, Vector3 direction, Color baseColor)
    {
        if (engineParticles.Count > 40)
        {
            return;
        }

        if (engineParticlePrefab == null)
        {
            return;
        }

        GameObject particleObject = poolService.Get(engineParticlePrefab, projectileRoot);
        if (particleObject == null)
        {
            return;
        }
        particleObject.transform.position = position + direction.normalized * 0.08f;
        particleObject.transform.localScale = Vector3.one * 0.12f;

        SpriteRenderer renderer = particleObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = particleObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = circleSprite;
        renderer.sortingOrder = 2;
        Color color = baseColor;
        color.a = 0.38f;
        renderer.color = color;

        engineParticles.Add(new EngineParticle
        {
            Transform = particleObject.transform,
            Renderer = renderer,
            Velocity = direction.normalized * UnityEngine.Random.Range(1.8f, 2.8f) + (Vector3)(-player.Velocity * 0.15f),
            Duration = UnityEngine.Random.Range(0.25f, 0.42f),
            BaseColor = color
        });
    }

    private void UpdateVisuals()
    {
        if (player != null && player.AuraRenderer != null)
        {
            Color aura = player.BaseAuraColor;
            aura.a = Mathf.Lerp(0.18f, 0.82f, player.ShieldPercent);
            player.AuraRenderer.color = aura;
        }

        if (player != null && player.BodyRenderer != null)
        {
            player.BodyRenderer.color = Color.Lerp(player.BaseBodyColor, new Color(1f, 0.28f, 0.28f, 1f), player.HitFlashTimer);
        }

        if (player != null && player.ThrusterRenderer != null)
        {
            float thrustAmount = Mathf.Clamp01(player.Velocity.magnitude / Mathf.Max(0.1f, player.Speed * player.SpeedMultiplier));
            float pulse = 0.7f + Mathf.Sin(player.ThrusterPulse * 7f) * 0.18f;
            Color color = new Color(1f, 0.74f, 0.3f, Mathf.Lerp(0.12f, 0.78f, thrustAmount) * pulse);
            player.ThrusterRenderer.color = color;
            player.ThrusterRenderer.transform.localScale = new Vector3(0.3f + thrustAmount * 0.12f, 0.45f + thrustAmount * 0.45f, 1f);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy.ShieldRenderer != null)
            {
                Color shield = enemy.BaseShieldColor;
                shield.a = Mathf.Lerp(0.08f, 0.9f, enemy.ShieldPercent);
                enemy.ShieldRenderer.color = shield;
            }

            if (enemy.BodyRenderer != null)
            {
                float flash = Mathf.Max(enemy.HitFlashTimer, enemy.AttackFlashTimer * 0.4f);
                enemy.BodyRenderer.color = Color.Lerp(enemy.BaseBodyColor, new Color(1f, 0.25f, 0.25f, 1f), flash);
            }

            if (enemy.ThrusterRenderer != null)
            {
                float pulse = 0.4f + Mathf.Sin(Time.time * 8f + i) * 0.15f + enemy.AttackFlashTimer * 0.35f;
                enemy.ThrusterRenderer.color = new Color(1f, 0.36f, 0.22f, Mathf.Clamp01(pulse));
            }

            if (enemy.TargetRenderer != null)
            {
                enemy.TargetRenderer.gameObject.SetActive(enemy == targetEnemy);
                enemy.TargetRenderer.transform.Rotate(0f, 0f, 65f * Time.deltaTime);
            }
        }

        for (int i = 0; i < stars.Count; i++)
        {
            StarVisual star = stars[i];
            Color color = star.Renderer.color;
            color.a = star.BaseAlpha * Mathf.Lerp(0.72f, 1.3f, (Mathf.Sin(Time.time * star.TwinkleSpeed + star.TwinkleOffset) + 1f) * 0.5f);
            star.Renderer.color = color;
        }
    }

    private void UpdateHud()
    {
        combatLogText.text = string.Join("\n", combatLog);
        capacitorText.text = Localize("capacitor") + Mathf.RoundToInt(player.CapacitorPercent * 100f) + "%";
        targetDisplayText.text = Localize("target_label") + (targetEnemy != null ? targetEnemy.Id + " (" + targetEnemy.Type + ")" : Localize("target_none_name"));
        shipText.text = Localize("ship_label") + starterShips[selectedShipIndex].Name;
        levelText.text = Localize("level_label") + player.Level;
        experienceText.text = Localize("xp_label") + player.Experience + " / " + player.ExperienceToNext;

        targetPanel.gameObject.SetActive(targetEnemy != null && targetEnemy.IsAlive());
        if (targetEnemy != null && targetEnemy.IsAlive())
        {
            targetNameText.text = targetEnemy.Id + "  " + targetEnemy.Type;
            targetDistanceText.text = Localize("distance") + Vector3.Distance(player.Transform.position, targetEnemy.Transform.position).ToString("0.0") + " km";
            SetFillWidth(targetShieldFill.rectTransform, targetEnemy.ShieldPercent, 252f);
            SetFillWidth(targetArmorFill.rectTransform, targetEnemy.ArmorPercent, 252f);
            SetFillWidth(targetHullFill.rectTransform, targetEnemy.HullPercent, 252f);
        }

        SetFillWidth(playerShieldFill.rectTransform, player.ShieldPercent, 180f);
        SetFillWidth(playerArmorFill.rectTransform, player.ArmorPercent, 180f);
        SetFillWidth(playerHullFill.rectTransform, player.HullPercent, 180f);
        SetFillHeight(capacitorFill.rectTransform, player.CapacitorPercent, 60f);

        UpdateEnemyRows();
        statusText.text = !gameStarted
            ? Localize("status_menu")
            : gameOver
                ? Localize("status_gameover")
                : levelUpPending
                    ? Localize("status_levelup")
                    : useVirtualJoystick ? Localize("status_play_mobile") : Localize("status_play_desktop");

        if (!gateActive || !gameStarted)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (player == null)
        {
            return;
        }

        if (healthBar != null)
        {
            healthBar.value = player.HullPercent;
        }

        if (scoreText != null)
        {
            scoreText.text = player.Experience.ToString();
        }

        if (waveText != null)
        {
            waveText.text = wave.ToString();
        }
    }

    private void UpdateEnemyRows()
    {
        enemies.Sort((left, right) =>
            Vector3.Distance(left.Transform.position, player.Transform.position)
                .CompareTo(Vector3.Distance(right.Transform.position, player.Transform.position)));

        for (int i = 0; i < enemyRows.Count; i++)
        {
            bool active = i < enemies.Count;
            enemyRows[i].RootTransform.gameObject.SetActive(active);
            if (!active)
            {
                enemyRows[i].Enemy = null;
                continue;
            }

            EnemyShip enemy = enemies[i];
            enemyRows[i].Enemy = enemy;
            float distance = Vector3.Distance(player.Transform.position, enemy.Transform.position);
            enemyRows[i].RootText.text = string.Format(
                "{0,-7}  {1,-11}  {2,4:0.0}km",
                enemy.Id,
                enemy.Type,
                distance);

            SetFillWidth(enemyRows[i].ShieldFill.rectTransform, enemy.ShieldPercent, 110f);
            SetFillWidth(enemyRows[i].ArmorFill.rectTransform, enemy.ArmorPercent, 110f);
            SetFillWidth(enemyRows[i].HullFill.rectTransform, enemy.HullPercent, 110f);

            Image background = enemyRows[i].RootTransform.GetComponent<Image>();
            background.color = enemy == targetEnemy
                ? new Color(0.12f, 0.3f, 0.42f, 1f)
                : new Color(0.06f, 0.11f, 0.16f, 0.95f);
        }
    }

    private void UpdateTargetState()
    {
        if (targetEnemy != null && !targetEnemy.IsAlive())
        {
            targetEnemy = null;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].TargetRenderer != null)
            {
                enemies[i].TargetRenderer.gameObject.SetActive(enemies[i] == targetEnemy);
            }
        }
    }

    private void UpdateModuleVisual(ModuleState module)
    {
        if (module.SlotImage == null)
        {
            return;
        }

        module.SlotImage.color = module.Active
            ? new Color(0.12f, 0.31f, 0.42f, 0.98f)
            : new Color(0.05f, 0.1f, 0.14f, 0.92f);
    }

    private void LogMessage(string message, string kind = "info")
    {
        string prefix;
        switch (kind)
        {
            case "hit":
                prefix = "[HIT] ";
                break;
            case "miss":
                prefix = "[MISS] ";
                break;
            case "critical":
                prefix = "[ALERT] ";
                break;
            case "warning":
                prefix = "[WARN] ";
                break;
            default:
                prefix = "[INFO] ";
                break;
        }

        combatLog.Add(prefix + message);
        while (combatLog.Count > 11)
        {
            combatLog.RemoveAt(0);
        }
    }

    private Sprite CreateFilledSprite(int width, int height, Func<int, int, int, Color> generator)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = generator(x, y, width);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        return uiFactory.CreateImage(objectName, parent, squareSprite, color);
    }

    private Text CreateText(string objectName, Transform parent, string content, int fontSize, FontStyle fontStyle, Color color)
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

    private void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        uiFactory.SetAnchoredRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    private void SetFillWidth(RectTransform rect, float percent, float maxWidth)
    {
        uiFactory.SetFillWidth(rect, percent, maxWidth);
    }

    private void SetFillHeight(RectTransform rect, float percent, float maxHeight)
    {
        uiFactory.SetFillHeight(rect, percent, maxHeight);
    }
}

