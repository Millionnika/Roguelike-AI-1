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
    [SerializeField] private EquipmentUIController equipmentUiController;
    [SerializeField] private SlotUI slotUiPrefab;

    [Header("Data")]
    [SerializeField] private MovementSettingsSO playerMovementSettings;
    [SerializeField] private WeaponDataSO playerWeaponData;
    [SerializeField] private List<ShipDataSO> availableShips = new List<ShipDataSO>();
    [SerializeField] private List<EnemyDataSO> waveConfigs = new List<EnemyDataSO>();

    [Header("Background Layers")]
    [SerializeField] private List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();

    [Header("Wave Settings")]
    [SerializeField] private WaveSpawnSettings waveSettings = new WaveSpawnSettings();

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float shotBaseVolume = 0.85f;
    [SerializeField, Range(0f, 0.5f)] private float shotPitchRandomRange = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float shotVolumeRandomRange = 0.12f;
    [SerializeField, Min(1)] private int shotAudioVoices = 4;

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
    private readonly List<ShipCardView> shipCardViews = new List<ShipCardView>();
    private readonly List<UiButtonView> mainMenuButtons = new List<UiButtonView>();
    private readonly List<UiButtonView> settingsButtons = new List<UiButtonView>();
    private readonly List<AttackBeamEffect> attackBeams = new List<AttackBeamEffect>();
    private readonly List<EngineParticle> engineParticles = new List<EngineParticle>();
    private readonly ShipEquipmentState equipmentState = new ShipEquipmentState();
    private readonly StringBuilder sharedBuilder = new StringBuilder(1024);
    private readonly int[] fpsOptions = { 60, 90, 120, 144 };

    private IPlatformService platformService;
    private IInputService inputService;
    private IMovementService movementService;
    private ICombatService combatService;
    private IPoolService poolService;
    private ILocalizationService localizationService;
    private ISpaceCombatUiFactory uiFactory;
    private IBackgroundParallaxService backgroundParallaxService;

    private Camera mainCamera;
    private PlayerShip player;
    private EnemyShip targetEnemy;
    private Transform worldRoot;
    private Transform starRoot;
    private Transform enemyRoot;
    private Transform projectileRoot;
    private Transform gateTransform;
    private Transform weaponSlotsRoot;
    private GameObject playerVisualInstance;

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
    private bool waitingForNextWave;
    private bool firstWaveSpawned;
    private bool levelUpPending;
    private bool gameOver;
    private bool gameStarted;
    private float nextWaveTimer;
    private int selectedShipIndex;
    private int selectedFpsIndex = 2;
    private LanguageOption currentLanguage = LanguageOption.RU;
    private StartMenuPage startMenuPage = StartMenuPage.Main;
    private bool useVirtualJoystick;
    private bool joystickDragging;
    private Vector2 joystickVector;
    private GameObject runtimeStarLayerPrefab;
    private GameObject runtimeNebulaLayerPrefab;
    private AudioSource[] shotAudioSources;
    private int nextShotAudioSourceIndex;

    public event Action<ShipEquipmentState> EquipmentStateChanged;
    public ShipEquipmentState CurrentEquipmentState => equipmentState;

    internal void ConfigureServices(
        IPlatformService newPlatformService,
        IInputService newInputService,
        IMovementService newMovementService,
        ICombatService newCombatService,
        IPoolService newPoolService,
        ILocalizationService newLocalizationService,
        ISpaceCombatUiFactory newUiFactory,
        IBackgroundParallaxService newBackgroundParallaxService)
    {
        platformService = newPlatformService;
        inputService = newInputService;
        movementService = newMovementService;
        combatService = newCombatService;
        poolService = newPoolService;
        localizationService = newLocalizationService;
        uiFactory = newUiFactory;
        backgroundParallaxService = newBackgroundParallaxService;
    }

    private void EnsureServices()
    {
        EnsureDataAssets();
        platformService ??= new RuntimePlatformService();
        inputService ??= new PlayerInputService();
        movementService ??= new PlayerMovementService();
        combatService ??= new CombatService();
        poolService ??= new PoolService();
        localizationService ??= new SpaceCombatLocalizationService();
        uiFactory ??= new SpaceCombatUiFactory();
        backgroundParallaxService ??= new BackgroundParallaxService();
        combatService.SetDefaultWeaponData(playerWeaponData);

        if (projectilePrefab != null)
        {
            poolService.InitializePool(projectilePrefab, 24);
        }
        if (playerWeaponData != null && playerWeaponData.projectilePrefab != null)
        {
            poolService.InitializePool(playerWeaponData.projectilePrefab, 24);
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
        EnsureWeaponAudioSources();
        CreateSprites();
        CreateStarterShips();
        BuildWorld();
        SpawnPlayer();
        SelectShip(GetInitialShipIndex());
        BuildHud();
        ApplyPerformanceSettings();
        ShowStartMenu(true);
        RefreshLocalizedTexts();
        LogMessage(Localize("log_docked"));
        LogMessage(Localize("log_choose_hull"));
        UpdateHud();
    }

    private void OnDestroy()
    {
        if (equipmentUiController != null)
        {
            equipmentUiController.Bind(null);
        }

        backgroundParallaxService?.Dispose();

        if (runtimeStarLayerPrefab != null)
        {
            Destroy(runtimeStarLayerPrefab);
        }
        if (runtimeNebulaLayerPrefab != null)
        {
            Destroy(runtimeNebulaLayerPrefab);
        }
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

    private void EnsureDataAssets()
    {
        if (playerMovementSettings == null)
        {
            playerMovementSettings = ScriptableObject.CreateInstance<MovementSettingsSO>();
            playerMovementSettings.moveSpeed = 6.2f;
            playerMovementSettings.rotationSpeed = 8f;
            playerMovementSettings.stoppingDistance = 0.25f;
        }

        if (playerWeaponData == null)
        {
            playerWeaponData = ScriptableObject.CreateInstance<WeaponDataSO>();
            playerWeaponData.damage = 28f;
            playerWeaponData.fireRate = 0.45f;
            playerWeaponData.projectileSpeed = 18f;
            playerWeaponData.capacitorPerShot = 9f;
            playerWeaponData.requiredClass = ShipClass.Light;
            playerWeaponData.projectilePrefab = projectilePrefab;
        }
        else if (playerWeaponData.projectilePrefab == null)
        {
            playerWeaponData.projectilePrefab = projectilePrefab;
        }

        availableShips ??= new List<ShipDataSO>();
        availableShips.RemoveAll(ship => ship == null);

        if (waveConfigs == null)
        {
            waveConfigs = new List<EnemyDataSO>();
        }

        if (waveConfigs.Count == 0)
        {
            EnemyDataSO fallbackEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            fallbackEnemy.name = "FallbackEnemyData";
            fallbackEnemy.maxHealth = 100f;
            fallbackEnemy.moveSpeed = 1.5f;
            fallbackEnemy.scoreValue = 40;
            fallbackEnemy.weaponData = playerWeaponData;
            waveConfigs.Add(fallbackEnemy);
        }

        waveSettings ??= new WaveSpawnSettings();
        backgroundLayers ??= new List<BackgroundLayerConfig>();
    }

    private void EnsureWeaponAudioSources()
    {
        int voices = Mathf.Max(1, shotAudioVoices);
        shotAudioSources = new AudioSource[voices];
        nextShotAudioSourceIndex = 0;

        Transform audioRoot = new GameObject("WeaponAudio").transform;
        audioRoot.SetParent(transform, false);
        audioRoot.localPosition = Vector3.zero;

        for (int i = 0; i < voices; i++)
        {
            GameObject sourceObject = new GameObject("WeaponShotSource_" + i);
            sourceObject.transform.SetParent(audioRoot, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            shotAudioSources[i] = source;
        }
    }

    private void PlayWeaponShot(WeaponDataSO weaponData)
    {
        if (weaponData == null || weaponData.fireSound == null || shotAudioSources == null || shotAudioSources.Length == 0)
        {
            return;
        }

        AudioSource source = shotAudioSources[nextShotAudioSourceIndex];
        nextShotAudioSourceIndex = (nextShotAudioSourceIndex + 1) % shotAudioSources.Length;
        if (source == null)
        {
            return;
        }

        float randomPitch = 1f + UnityEngine.Random.Range(-shotPitchRandomRange, shotPitchRandomRange);
        float randomVolume = 1f + UnityEngine.Random.Range(-shotVolumeRandomRange, shotVolumeRandomRange);
        source.pitch = Mathf.Clamp(randomPitch, 0.5f, 2f);
        float volumeScale = Mathf.Clamp01(shotBaseVolume * randomVolume);
        source.PlayOneShot(weaponData.fireSound, volumeScale);
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
        UpdateWaveState(deltaTime);
        UpdateBackgroundParallax();
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
                int hotkeyShipIndex = ReadShipHotkey(keyboard);
                if (hotkeyShipIndex >= 0 && availableShips != null && hotkeyShipIndex < availableShips.Count)
                {
                    SelectShip(hotkeyShipIndex);
                }

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

    private static int ReadShipHotkey(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 5;
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 6;
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 7;
        if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) return 8;
        return -1;
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

    private string GetShipRoleText(ShipDataSO ship)
    {
        return localizationService.GetShipRoleText(ship, currentLanguage == LanguageOption.RU);
    }

    private string GetShipDescriptionText(ShipDataSO ship)
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
        if (availableShips == null)
        {
            availableShips = new List<ShipDataSO>();
        }

        if (availableShips.Count > 0)
        {
            return;
        }

        availableShips.Add(CreateRuntimeShipData(
            "Aegis",
            "Balanced Frigate",
            "Сбалансированный фрегат",
            "Universal hull with reliable capacitor and solid survivability. Good first choice for learning the combat loop.",
            "Универсальный корпус с надежной энергетикой и хорошей живучестью.",
            ShipClass.Medium,
            6.5f,
            11f,
            8.5f,
            1.6f,
            430f,
            320f,
            220f,
            1200f,
            92f,
            2,
            4,
            1f,
            1f,
            new Color(0.28f, 0.6f, 0.94f, 1f),
            new Color(0.38f, 0.76f, 1f, 0.72f),
            playerWeaponData));

        availableShips.Add(CreateRuntimeShipData(
            "Bulwark",
            "Heavy Cruiser",
            "Тяжёлый крейсер",
            "Slow but durable platform with the best shields and armor. Repairs are stronger and capacitor is deep enough for long fights.",
            "Медленный, но очень прочный корабль с мощными щитами и бронёй.",
            ShipClass.Heavy,
            5.4f,
            8.2f,
            6.6f,
            1.95f,
            580f,
            430f,
            290f,
            1450f,
            88f,
            3,
            4,
            0.94f,
            1.22f,
            new Color(0.18f, 0.78f, 0.8f, 1f),
            new Color(0.42f, 1f, 0.92f, 0.72f),
            playerWeaponData));

        availableShips.Add(CreateRuntimeShipData(
            "Raptor",
            "Strike Interceptor",
            "Ударный перехватчик",
            "Fast hunter with stronger volleys and snappier capacitor recovery. Lower defenses reward mobility and target focus.",
            "Быстрый охотник с повышенным уроном. Требует мобильности и приоритета целей.",
            ShipClass.Light,
            8f,
            13.4f,
            10.5f,
            1.15f,
            320f,
            210f,
            170f,
            1050f,
            72f,
            2,
            3,
            1.2f,
            0.9f,
            new Color(1f, 0.58f, 0.18f, 1f),
            new Color(1f, 0.75f, 0.36f, 0.72f),
            playerWeaponData));
    }

    private static ShipDataSO CreateRuntimeShipData(
        string displayName,
        string role,
        string roleRu,
        string description,
        string descriptionRu,
        ShipClass shipClass,
        float maxSpeed,
        float acceleration,
        float rotationSpeed,
        float drag,
        float maxShield,
        float maxArmor,
        float maxHull,
        float capacitor,
        float capacitorRechargeTime,
        int weaponSlotCount,
        int moduleSlotCount,
        float damageMultiplier,
        float repairMultiplier,
        Color accentColor,
        Color auraColor,
        WeaponDataSO defaultWeapon)
    {
        ShipDataSO data = ScriptableObject.CreateInstance<ShipDataSO>();
        data.displayName = displayName;
        data.role = role;
        data.roleRu = roleRu;
        data.description = description;
        data.descriptionRu = descriptionRu;
        data.shipClass = shipClass;
        data.maxSpeed = maxSpeed;
        data.acceleration = acceleration;
        data.rotationSpeed = rotationSpeed;
        data.drag = drag;
        data.maxShield = maxShield;
        data.maxArmor = maxArmor;
        data.maxHull = maxHull;
        data.capacitor = capacitor;
        data.capacitorRechargeTime = capacitorRechargeTime;
        data.weaponSlotCount = weaponSlotCount;
        data.moduleSlotCount = moduleSlotCount;
        data.damageMultiplier = damageMultiplier;
        data.repairMultiplier = repairMultiplier;
        data.accentColor = accentColor;
        data.auraColor = auraColor;
        data.startingWeapons = new List<WeaponDataSO>();
        data.startingModules = new List<ModuleDataSO>();
        for (int i = 0; i < weaponSlotCount; i++)
        {
            data.startingWeapons.Add(defaultWeapon);
        }
        for (int i = 0; i < moduleSlotCount; i++)
        {
            data.startingModules.Add(null);
        }
        return data;
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
        EnsureBackgroundLayers();
        backgroundParallaxService.Dispose();
        backgroundParallaxService.Initialize(starRoot, backgroundLayers, poolService);
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

        player = new PlayerShip
        {
            Transform = playerObject.transform
        };
    }

    private void SelectShip(int index)
    {
        if (availableShips == null || availableShips.Count == 0)
        {
            return;
        }

        selectedShipIndex = Mathf.Clamp(index, 0, availableShips.Count - 1);
        ApplyShipDefinition(availableShips[selectedShipIndex], false);
        UpdateStartMenuVisuals();
    }

    private int GetInitialShipIndex()
    {
        if (availableShips == null || availableShips.Count == 0)
        {
            return 0;
        }

        for (int i = 0; i < availableShips.Count; i++)
        {
            if (availableShips[i] != null && availableShips[i].shipPrefab != null)
            {
                return i;
            }
        }

        return 0;
    }

    private void ApplyShipDefinition(ShipDataSO ship, bool resetProgress)
    {
        if (player == null || ship == null)
        {
            return;
        }

        player.Speed = Mathf.Max(0.1f, ship.maxSpeed);
        player.Acceleration = Mathf.Max(0.1f, ship.acceleration);
        player.Drag = Mathf.Max(0f, ship.drag);
        player.RotationResponsiveness = Mathf.Max(0.1f, ship.rotationSpeed);
        player.SpeedMultiplier = 1f;
        player.DamageMultiplier = Mathf.Max(0.1f, ship.damageMultiplier);
        player.RepairMultiplier = Mathf.Max(0.1f, ship.repairMultiplier);
        player.MaxShield = Mathf.Max(1f, ship.maxShield);
        player.Shield = player.MaxShield;
        player.MaxArmor = Mathf.Max(1f, ship.maxArmor);
        player.Armor = player.MaxArmor;
        player.MaxHull = Mathf.Max(1f, ship.maxHull);
        player.Hull = player.MaxHull;
        player.MaxCapacitor = Mathf.Max(1f, ship.capacitor);
        player.Capacitor = player.MaxCapacitor;
        player.CapacitorRechargeTime = Mathf.Max(1f, ship.capacitorRechargeTime);
        player.Transform.position = Vector3.zero;
        player.Transform.rotation = Quaternion.identity;
        player.Velocity = Vector2.zero;
        player.MoveCommandActive = false;

        playerMovementSettings.moveSpeed = player.Speed;
        playerMovementSettings.rotationSpeed = ship.rotationSpeed;
        ApplyShipVisualFromPrefab(ship);

        CreateModules(ship.moduleSlotCount);
        ConfigureEquipment(ship);

        if (resetProgress)
        {
            player.Level = 1;
            player.Experience = 0;
            player.ExperienceToNext = 100;
        }
    }

    private void ConfigureEquipment(ShipDataSO ship)
    {
        if (player == null || player.Transform == null || ship == null)
        {
            return;
        }

        equipmentState.ShipData = ship;
        equipmentState.ConfigureSlots(Mathf.Max(0, ship.weaponSlotCount), Mathf.Max(0, ship.moduleSlotCount));
        RebuildWeaponSlots(ship.weaponSlotCount);

        for (int i = 0; i < equipmentState.InstalledWeapons.Count; i++)
        {
            WeaponDataSO configuredWeapon = ship.startingWeapons != null && i < ship.startingWeapons.Count
                ? ship.startingWeapons[i]
                : null;
            if (configuredWeapon != null && !CanShipUseWeapon(ship.shipClass, configuredWeapon))
            {
                Debug.LogWarning(
                    "SpaceCombatSceneController: weapon '" + configuredWeapon.name + "' in ship '" + ship.displayName +
                    "' slot " + (i + 1) + " is not compatible with class " + ship.shipClass + ".");
                configuredWeapon = null;
            }

            equipmentState.InstalledWeapons[i] = configuredWeapon;
            equipmentState.WeaponTimers[i] = 0f;
        }

        for (int i = 0; i < equipmentState.InstalledModules.Count; i++)
        {
            ModuleDataSO moduleData = ship.startingModules != null && i < ship.startingModules.Count
                ? ship.startingModules[i]
                : null;
            equipmentState.InstalledModules[i] = moduleData;
        }

        NotifyEquipmentStateChanged();
    }

    private void ApplyShipVisualFromPrefab(ShipDataSO ship)
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        if (playerVisualInstance != null)
        {
            Destroy(playerVisualInstance);
            playerVisualInstance = null;
        }

        player.BodyRenderer = null;
        player.AuraRenderer = null;
        player.ThrusterRenderer = null;

        if (ship == null || ship.shipPrefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: Ship prefab is missing for ship '" + (ship != null ? ship.displayName : "null") + "'.");
            return;
        }

        playerVisualInstance = Instantiate(ship.shipPrefab, player.Transform);
        playerVisualInstance.name = "PlayerVisual";
        playerVisualInstance.transform.localPosition = Vector3.zero;
        playerVisualInstance.transform.localRotation = Quaternion.identity;
        playerVisualInstance.transform.localScale = Vector3.one;

        ResolvePlayerVisualRenderers(playerVisualInstance.transform, out SpriteRenderer body, out SpriteRenderer aura, out SpriteRenderer thruster);
        player.BodyRenderer = body;
        player.AuraRenderer = aura;
        player.ThrusterRenderer = thruster;

        player.BaseBodyColor = body != null ? body.color : ship.accentColor;
        player.BaseAuraColor = aura != null ? aura.color : ship.auraColor;
    }

    private static void ResolvePlayerVisualRenderers(Transform visualRoot, out SpriteRenderer body, out SpriteRenderer aura, out SpriteRenderer thruster)
    {
        body = null;
        aura = null;
        thruster = null;

        if (visualRoot == null)
        {
            return;
        }

        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string lowerName = renderers[i].name.ToLowerInvariant();
            if (body == null && (lowerName.Contains("body") || lowerName.Contains("hull")))
            {
                body = renderers[i];
            }
            else if (aura == null && (lowerName.Contains("aura") || lowerName.Contains("shield")))
            {
                aura = renderers[i];
            }
            else if (thruster == null && (lowerName.Contains("thruster") || lowerName.Contains("engine")))
            {
                thruster = renderers[i];
            }
        }

        if (body == null && renderers.Length > 0)
        {
            body = renderers[0];
        }
    }

    private void RebuildWeaponSlots(int weaponSlotCount)
    {
        int slotCount = Mathf.Max(0, weaponSlotCount);

        if (weaponSlotsRoot == null)
        {
            weaponSlotsRoot = new GameObject("WeaponSlots").transform;
            weaponSlotsRoot.SetParent(player.Transform, false);
        }

        for (int i = weaponSlotsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(weaponSlotsRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject = new GameObject("WeaponSlot_" + (i + 1));
            slotObject.transform.SetParent(weaponSlotsRoot, false);

            float lerp = slotCount <= 1 ? 0.5f : i / (float)(slotCount - 1);
            float x = Mathf.Lerp(-0.38f, 0.38f, lerp);
            float y = Mathf.Lerp(0.58f, 0.66f, 1f - Mathf.Abs(lerp - 0.5f) * 2f);
            slotObject.transform.localPosition = new Vector3(x, y, 0f);
            slotObject.transform.localRotation = Quaternion.identity;

            if (i < equipmentState.WeaponMuzzles.Count)
            {
                equipmentState.WeaponMuzzles[i] = slotObject.transform;
            }
        }
    }

    private static bool CanShipUseWeapon(ShipClass shipClass, WeaponDataSO weaponData)
    {
        if (weaponData == null)
        {
            return false;
        }

        return GetShipClassRank(shipClass) >= GetShipClassRank(weaponData.requiredClass);
    }

    private static int GetShipClassRank(ShipClass shipClass)
    {
        switch (shipClass)
        {
            case ShipClass.Light: return 0;
            case ShipClass.Medium: return 1;
            case ShipClass.Heavy: return 2;
            default: return 0;
        }
    }

    private void NotifyEquipmentStateChanged()
    {
        EquipmentStateChanged?.Invoke(equipmentState);
        if (equipmentUiController != null)
        {
            equipmentUiController.Refresh(equipmentState);
        }
    }

    private void StartRun()
    {
        if (availableShips == null || availableShips.Count == 0)
        {
            Debug.LogError("SpaceCombatSceneController: no ships configured in availableShips.");
            return;
        }
        selectedShipIndex = Mathf.Clamp(selectedShipIndex, 0, availableShips.Count - 1);

        ShowStartMenu(false);
        gameStarted = true;
        gameOver = false;
        levelUpPending = false;
        wave = 1;
        waitingForNextWave = false;
        nextWaveTimer = 0f;
        firstWaveSpawned = false;
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
        ApplyShipDefinition(availableShips[selectedShipIndex], true);
        StartWaveCountdown(waveSettings != null ? waveSettings.initialWaveDelay : 3f, false);
        LogMessage(Localize("log_launch") + availableShips[selectedShipIndex].displayName);
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

        if (equipmentUiController != null)
        {
            equipmentUiController.Refresh(equipmentState);
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
        if (availableShips == null || availableShips.Count == 0 || selectedShipIndex < 0 || selectedShipIndex >= availableShips.Count)
        {
            return;
        }

        ShipDataSO ship = availableShips[selectedShipIndex];
        if (ship == null)
        {
            return;
        }
        if (startMenuShipNameText != null)
        {
            startMenuShipNameText.text = ship.displayName;
            if (startMenuRoleText != null)
            {
                startMenuRoleText.text = GetShipRoleText(ship);
            }
            if (startMenuDescriptionText != null)
            {
                startMenuDescriptionText.text = GetShipDescriptionText(ship);
            }
            if (startMenuStatsText != null)
            {
                startMenuStatsText.text =
                (currentLanguage == LanguageOption.RU
                    ? "Скорость: " + ship.maxSpeed.ToString("0.0") +
                      "    Щит: " + Mathf.RoundToInt(ship.maxShield) +
                      "    Броня: " + Mathf.RoundToInt(ship.maxArmor) +
                      "    Корпус: " + Mathf.RoundToInt(ship.maxHull) +
                      "\nЭнергия: " + Mathf.RoundToInt(ship.capacitor) +
                      "    Перезаряд: " + ship.capacitorRechargeTime.ToString("0") + "с" +
                      "    Слоты оружия: " + Mathf.Max(0, ship.weaponSlotCount) +
                      "    Слоты модулей: " + Mathf.Max(0, ship.moduleSlotCount)
                    : "Speed: " + ship.maxSpeed.ToString("0.0") +
                      "    Shield: " + Mathf.RoundToInt(ship.maxShield) +
                      "    Armor: " + Mathf.RoundToInt(ship.maxArmor) +
                      "    Hull: " + Mathf.RoundToInt(ship.maxHull) +
                      "\nCapacitor: " + Mathf.RoundToInt(ship.capacitor) +
                      "    Recharge: " + ship.capacitorRechargeTime.ToString("0") + "s" +
                      "    Weapon slots: " + Mathf.Max(0, ship.weaponSlotCount) +
                      "    Module slots: " + Mathf.Max(0, ship.moduleSlotCount));
            }
            if (startMenuPreviewImage != null)
            {
                startMenuPreviewImage.sprite = ship.shipIcon;
                startMenuPreviewImage.color = ship.shipIcon != null ? Color.white : ship.accentColor;
            }
        }

        if (startButtonImage != null)
        {
            startButtonImage.color = Color.Lerp(new Color(0.12f, 0.3f, 0.42f, 1f), ship.accentColor, 0.45f);
            if (startButtonText != null)
            {
                startButtonText.text = Localize("start_operation") + " " + ship.displayName.ToUpperInvariant();
            }
        }

        int cardCount = Mathf.Min(shipCardViews.Count, availableShips.Count);
        for (int i = 0; i < cardCount; i++)
        {
            ShipDataSO cardShip = availableShips[i];
            if (cardShip == null)
            {
                continue;
            }
            bool isSelected = i == selectedShipIndex;
            if (shipCardViews[i].Background != null)
            {
                shipCardViews[i].Background.color = isSelected
                ? new Color(0.12f, 0.26f, 0.36f, 1f)
                : new Color(0.06f, 0.12f, 0.17f, 0.96f);
            }
            if (shipCardViews[i].Title != null)
            {
                shipCardViews[i].Title.text = cardShip.displayName + "\n<size=16>" + GetShipRoleText(cardShip) + "</size>";
                shipCardViews[i].Title.color = isSelected ? cardShip.accentColor : Color.white;
            }
            if (shipCardViews[i].Stats != null)
            {
                shipCardViews[i].Stats.text =
                (currentLanguage == LanguageOption.RU
                    ? "Щит " + Mathf.RoundToInt(cardShip.maxShield) +
                      "  Броня " + Mathf.RoundToInt(cardShip.maxArmor) +
                      "\nСкорость " + cardShip.maxSpeed.ToString("0.0") +
                      "  Пушки " + Mathf.Max(0, cardShip.weaponSlotCount)
                    : "Shield " + Mathf.RoundToInt(cardShip.maxShield) +
                      "  Armor " + Mathf.RoundToInt(cardShip.maxArmor) +
                      "\nSpeed " + cardShip.maxSpeed.ToString("0.0") +
                      "  Guns " + Mathf.Max(0, cardShip.weaponSlotCount));
            }
        }

        if (startMenuHintText != null)
        {
            startMenuHintText.text = useVirtualJoystick ? Localize("hangar_hint_mobile") : Localize("hangar_hint_desktop");
        }
    }

    private void CreateModules(int moduleSlotCount)
    {
        modules.Clear();
        int supportedSlots = Mathf.Clamp(Mathf.Max(1, moduleSlotCount), 1, 4);

        modules.Add(new ModuleState
        {
            Name = "Weapon Group",
            KeyLabel = "1",
            Type = ModuleType.Weapon,
            CapPerShot = playerWeaponData != null ? playerWeaponData.capacitorPerShot : 9f,
            RateOfFire = playerWeaponData != null ? playerWeaponData.fireRate : 0.45f,
            Damage = playerWeaponData != null ? playerWeaponData.damage : 28f,
            OptimalRange = 5.1f,
            FalloffRange = 3.2f,
            WeaponData = playerWeaponData
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

    private void SpawnWave()
    {
        ClearEnemies();
        ClearProjectiles();
        waitingForNextWave = false;
        nextWaveTimer = 0f;
        firstWaveSpawned = true;

        if (gateTransform != null)
        {
            gateTransform.gameObject.SetActive(false);
        }

        int baseCount = waveSettings != null ? Mathf.Max(1, waveSettings.enemiesPerWave) : 5;
        int enemyCount = Mathf.Clamp(baseCount + (wave - 1), baseCount, 24);
        EnemyDataSO[] configs = waveConfigs != null && waveConfigs.Count > 0
            ? waveConfigs.ToArray()
            : Array.Empty<EnemyDataSO>();

        for (int i = 0; i < enemyCount; i++)
        {
            float margin = waveSettings != null ? waveSettings.spawnOffscreenMargin : 2f;
            Vector3 position = GetOffscreenSpawnPosition(margin);
            EnemyDataSO enemyData = configs.Length > 0
                ? configs[UnityEngine.Random.Range(0, configs.Length)]
                : null;
            EnemyShip enemy = CreateEnemy(
                "E-" + (i + 1).ToString("00"),
                enemyData,
                position,
                1f + (wave - 1) * 0.14f);
            if (enemy != null)
            {
                enemies.Add(enemy);
            }
        }

        targetEnemy = enemies.Count > 0 ? enemies[0] : null;
        UpdateTargetState();
        LogMessage(Localize("log_hostiles") + enemies.Count);
    }

    private EnemyShip CreateEnemy(string id, EnemyDataSO enemyData, Vector3 position, float levelScale)
    {
        if (enemyData == null || enemyData.prefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: EnemyDataSO or enemy prefab is missing for enemy " + id + ".");
            return null;
        }

        string typeName = enemyData.name;
        GameObject enemyPrefab = enemyData.prefab;

        GameObject enemyObject = poolService.Get(enemyPrefab, enemyRoot);
        if (enemyObject == null) return null;

        enemyObject.name = id;
        enemyObject.transform.position = position;

        SpriteRenderer bodyRenderer = enemyObject.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer shieldRenderer = FindChildSpriteRenderer(enemyObject.transform, "Shield");
        SpriteRenderer targetRenderer = FindChildSpriteRenderer(enemyObject.transform, "TargetRing");
        if (targetRenderer != null)
        {
            targetRenderer.gameObject.SetActive(false);
        }
        SpriteRenderer thrusterRenderer = FindChildSpriteRenderer(enemyObject.transform, "Thruster");

        float baseHealth = (enemyData != null ? enemyData.maxHealth : 100f) * levelScale;
        float shieldValue = Mathf.RoundToInt(baseHealth * 1.5f);
        float armorValue = Mathf.RoundToInt(baseHealth * 1.2f);
        float hullValue = Mathf.RoundToInt(baseHealth);
        float enemyMoveSpeed = enemyData != null ? enemyData.moveSpeed : 1.5f;
        WeaponDataSO enemyWeapon = enemyData != null ? enemyData.weaponData : null;
        float enemyDamage = enemyWeapon != null ? enemyWeapon.damage * levelScale : 10f * levelScale;

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
            Damage = enemyDamage,
            ScoreValue = enemyData != null ? enemyData.scoreValue : 40,
            DriftSpeed = enemyMoveSpeed + levelScale * 0.2f,
            MaxShield = shieldValue,
            Shield = shieldValue,
            MaxArmor = armorValue,
            Armor = armorValue,
            MaxHull = hullValue,
            Hull = hullValue,
            WeaponData = enemyWeapon,
            Prefab = enemyPrefab,
            BaseBodyColor = bodyRenderer != null ? bodyRenderer.color : Color.white,
            BaseShieldColor = shieldRenderer != null ? shieldRenderer.color : Color.white
        };
    }

    private void ClearEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].Transform != null)
            {
                if (enemies[i].Prefab != null)
                {
                    poolService.Return(enemies[i].Prefab, enemies[i].Transform.gameObject);
                }
                else
                {
                    Destroy(enemies[i].Transform.gameObject);
                }
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
                GameObject pooledPrefab = projectiles[i].Prefab != null ? projectiles[i].Prefab : projectilePrefab;
                if (pooledPrefab != null)
                {
                    poolService.Return(pooledPrefab, projectiles[i].Transform.gameObject);
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
        CreateEquipmentHud(canvasObject.transform);
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

    private void CreateEquipmentHud(Transform parent)
    {
        RectTransform panel = new GameObject("EquipmentPanelRuntime", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
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
        runtimeController.Configure(this, slotUiPrefab, weaponsRow, modulesRow);
        equipmentUiController = runtimeController;
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
        int shipCount = availableShips != null ? availableShips.Count : 0;
        for (int i = 0; i < shipCount; i++)
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
        startMenuPreviewImage.sprite = null;

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
            Text slotKey = slotTransform.Find("Key") != null ? slotTransform.Find("Key").GetComponent<Text>() : null;
            Text slotTitle = slotTransform.Find("Label") != null ? slotTransform.Find("Label").GetComponent<Text>() : null;

            if (i < modules.Count)
            {
                ModuleState module = modules[i];
                module.SlotImage = slotImage;
                module.SlotKey = slotKey;
                module.SlotTitle = slotTitle;
                module.SlotKey.text = "[" + module.KeyLabel + "]";
                module.SlotTitle.text = module.Name;
                UpdateModuleVisual(module);
            }
            else
            {
                if (slotKey != null) slotKey.text = string.Empty;
                if (slotTitle != null) slotTitle.text = string.Empty;
                if (slotImage != null) slotImage.color = new Color(0.05f, 0.1f, 0.14f, 0.45f);
            }
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
        }

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ToggleModule(0);
            if (keyboard.digit2Key.wasPressedThisFrame) ToggleModule(1);
            if (keyboard.digit3Key.wasPressedThisFrame) ToggleModule(2);
            if (keyboard.digit4Key.wasPressedThisFrame) ToggleModule(3);
        }

        PointerInputState pointerState = inputService.ReadPointerState();
        bool pointerBlocked = pointerState.HasPointer && IsGameplayHudBlocked(pointerState.ScreenPosition);
        Vector3 pointerWorldPosition = pointerState.HasPointer
            ? ScreenToWorldPosition(pointerState.ScreenPosition)
            : player.Transform.position;

        MovementUpdateContext movementContext = new MovementUpdateContext(
            moveInput,
            pointerBlocked,
            pointerState,
            pointerWorldPosition);
        movementService.UpdateMovement(player, movementContext, playerMovementSettings, deltaTime);
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

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null)
        {
            return Vector3.zero;
        }

        Vector3 world = camera.ScreenToWorldPoint(screenPosition);
        world.z = 0f;
        return world;
    }

    private void UpdateBackgroundParallax()
    {
        if (player != null && player.Transform != null && backgroundParallaxService != null)
        {
            backgroundParallaxService.Update(player.Transform.position);
        }
    }

    private void EnsureBackgroundLayers()
    {
        if (backgroundLayers == null)
        {
            backgroundLayers = new List<BackgroundLayerConfig>();
        }

        if (backgroundLayers.Count > 0)
        {
            return;
        }

        backgroundLayers.Add(new BackgroundLayerConfig
        {
            prefab = GetRuntimeNebulaLayerPrefab(),
            parallaxFactor = 0.08f,
            tileSize = 48f,
            gridRadius = 2
        });
        backgroundLayers.Add(new BackgroundLayerConfig
        {
            prefab = GetRuntimeStarLayerPrefab(),
            parallaxFactor = 0.18f,
            tileSize = 36f,
            gridRadius = 2
        });
    }

    private GameObject GetRuntimeStarLayerPrefab()
    {
        if (runtimeStarLayerPrefab != null)
        {
            return runtimeStarLayerPrefab;
        }

        runtimeStarLayerPrefab = new GameObject("RuntimeStarLayerPrefab");
        runtimeStarLayerPrefab.SetActive(false);
        runtimeStarLayerPrefab.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = runtimeStarLayerPrefab.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = new Color(0.7f, 0.85f, 1f, 0.85f);
        renderer.sortingOrder = -20;
        runtimeStarLayerPrefab.transform.localScale = new Vector3(0.08f, 0.08f, 1f);
        return runtimeStarLayerPrefab;
    }

    private GameObject GetRuntimeNebulaLayerPrefab()
    {
        if (runtimeNebulaLayerPrefab != null)
        {
            return runtimeNebulaLayerPrefab;
        }

        runtimeNebulaLayerPrefab = new GameObject("RuntimeNebulaLayerPrefab");
        runtimeNebulaLayerPrefab.SetActive(false);
        runtimeNebulaLayerPrefab.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = runtimeNebulaLayerPrefab.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = new Color(0.1f, 0.24f, 0.35f, 0.16f);
        renderer.sortingOrder = -30;
        runtimeNebulaLayerPrefab.transform.localScale = new Vector3(5.5f, 3.8f, 1f);
        return runtimeNebulaLayerPrefab;
    }

    private Vector3 GetOffscreenSpawnPosition(float margin)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null)
        {
            return player != null ? player.Transform.position : Vector3.zero;
        }

        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        float extra = Mathf.Max(0f, margin);

        int side = UnityEngine.Random.Range(0, 4);
        switch (side)
        {
            case 0:
                return new Vector3(UnityEngine.Random.Range(min.x, max.x), max.y + extra, 0f);
            case 1:
                return new Vector3(UnityEngine.Random.Range(min.x, max.x), min.y - extra, 0f);
            case 2:
                return new Vector3(min.x - extra, UnityEngine.Random.Range(min.y, max.y), 0f);
            default:
                return new Vector3(max.x + extra, UnityEngine.Random.Range(min.y, max.y), 0f);
        }
    }

    private static SpriteRenderer FindChildSpriteRenderer(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (!string.Equals(children[i].name, childName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return children[i].GetComponent<SpriteRenderer>();
        }

        return null;
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
            EquipmentState = equipmentState,
            TargetEnemy = targetEnemy,
            ProjectileRoot = projectileRoot,
            ProjectilePrefab = projectilePrefab,
            PoolService = poolService,
            Wave = wave,
            Localize = Localize,
            LogMessage = LogMessage,
            UpdateModuleVisual = UpdateModuleVisual,
            CreateAttackBeam = CreateAttackBeam,
            PlayWeaponShot = PlayWeaponShot
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

    private void UpdateWaveState(float deltaTime)
    {
        if (enemies.Count > 0)
        {
            waitingForNextWave = false;
            nextWaveTimer = 0f;
            if (gateTransform != null)
            {
                gateTransform.gameObject.SetActive(false);
            }
            if (gateHintText != null)
            {
                gateHintText.transform.parent.gameObject.SetActive(false);
            }

            return;
        }

        if (!waitingForNextWave)
        {
            StartWaveCountdown(waveSettings != null ? waveSettings.timeBetweenWaves : 3f, true);
        }

        nextWaveTimer = Mathf.Max(0f, nextWaveTimer - deltaTime);
        if (gateHintText != null)
        {
            gateHintText.text = currentLanguage == LanguageOption.RU
                ? "Следующая волна через " + nextWaveTimer.ToString("0.0") + "с"
                : "Next wave in " + nextWaveTimer.ToString("0.0") + "s";
        }

        if (nextWaveTimer > 0f)
        {
            return;
        }

        waitingForNextWave = false;
        if (firstWaveSpawned)
        {
            wave++;
            player.Shield = player.MaxShield;
            player.Armor = player.MaxArmor;
            player.Hull = player.MaxHull;
            player.Capacitor = player.MaxCapacitor;
        }
        if (gateHintText != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }
        if (firstWaveSpawned)
        {
            LogMessage(Localize("log_warp_sector") + wave, "critical");
        }
        SpawnWave();
    }

    private void StartWaveCountdown(float delaySeconds, bool logActivation)
    {
        waitingForNextWave = true;
        nextWaveTimer = Mathf.Max(0f, delaySeconds);
        if (gateHintText != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(true);
        }

        if (logActivation)
        {
            LogMessage(Localize("log_warp_active"), "warning");
        }
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

    }

    private void UpdateHud()
    {
        combatLogText.text = string.Join("\n", combatLog);
        capacitorText.text = Localize("capacitor") + Mathf.RoundToInt(player.CapacitorPercent * 100f) + "%";
        targetDisplayText.text = Localize("target_label") + (targetEnemy != null ? targetEnemy.Id + " (" + targetEnemy.Type + ")" : Localize("target_none_name"));
        string shipName = (availableShips != null && availableShips.Count > 0 && selectedShipIndex >= 0 && selectedShipIndex < availableShips.Count)
            ? availableShips[selectedShipIndex].displayName
            : "-";
        shipText.text = Localize("ship_label") + shipName;
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

        if (!waitingForNextWave || !gameStarted)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }

        if (equipmentUiController != null)
        {
            equipmentUiController.RefreshCooldowns(equipmentState);
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

