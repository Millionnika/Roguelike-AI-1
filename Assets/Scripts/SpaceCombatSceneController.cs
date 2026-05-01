using System;
using System.Collections.Generic;
using System.Text;
using SpaceFrontier.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SpaceCombatSceneController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Inspector: health bar")]
    [SerializeField] private Slider healthBar;
    [Tooltip("Inspector: score text")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("Inspector: wave text")]
    [SerializeField] private TMP_Text waveText;
    [Tooltip("Inspector: equipment ui controller")]
    [SerializeField] private EquipmentUIController equipmentUiController;
    [Tooltip("Inspector: slot ui prefab")]
    [SerializeField] private SlotUI slotUiPrefab;
    [Header("Аудио боя")]
    [Tooltip("Компонент, отвечающий за звук выстрелов оружия. Если поле пустое, контроллер найдет компонент на сцене или добавит его автоматически.")]
    [SerializeField] private CombatAudioController combatAudioController;
    [Header("Журнал боя")]
    [Tooltip("Компонент, отвечающий за хранение и отображение журнала боя. Если поле пустое, контроллер найдет компонент на сцене или добавит его автоматически.")]
    [SerializeField] private CombatLogPresenter combatLogPresenter;
    [Header("Камера боя")]
    [Tooltip("Компонент, отвечающий за основную камеру боя: настройку, следование за игроком и zoom. Если поле пустое, контроллер найдет компонент на сцене или добавит его автоматически.")]
    [SerializeField] private CombatCameraController combatCameraController;
    [Header("Локации")]
    [Tooltip("Контроллер run/encounter flow: выбор локаций, завершение encounter, небоевые заглушки и связь с RunManager.")]
    [SerializeField] private EncounterFlowController encounterFlowController;
    [Tooltip("Контроллер runtime-спавна WaveTimelineSO: таймер, волны, паттерны и позиции спавна за экраном.")]
    [SerializeField] private TimelineSpawnController timelineSpawnController;
    [Tooltip("Компонент, создающий и настраивающий вражеские корабли из ShipDataSO. Список врагов остается в SpaceCombatSceneController.")]
    [SerializeField] private EnemySpawner enemySpawner;
    [Tooltip("Компонент, отвечающий за выбор текущей цели, рамку/линию цели и активацию TargetRing на выбранном враге.")]
    [SerializeField] private TargetingController targetingController;
    [Tooltip("Компонент визуализации команды движения: линия к точке и маркер цели. Не влияет на игровую механику движения.")]
    [SerializeField] private MoveCommandVisualController moveCommandVisualController;
    [Tooltip("Компонент системы миникарты: камера, RenderTexture, привязка к панели и обновление позиции.")]
    [SerializeField] private MinimapController minimapController;
    [Header("Фон и параллакс")]
    [Tooltip("Компонент фона и параллакса. Отвечает за слои звезд/туманности, runtime-fallback и обновление фона по позиции игрока.")]
    [SerializeField] private BackgroundController backgroundController;
    [Tooltip("Компонент, создающий корабль игрока, применяющий ShipDataSO, экипировку, слои и прием урона.")]
    [SerializeField] private PlayerShipController playerShipController;
    [Header("Боевой HUD")]
    [Tooltip("Компонент отображения боевого HUD: статус игрока, цель, обзор врагов и панель экипировки.")]
    [SerializeField] private CombatHudPresenter combatHudPresenter;
    [Tooltip("Presenter меню паузы: HUD-кнопка, панель паузы и обработка UI-нажатий.")]
    [SerializeField] private PauseMenuPresenter pauseMenuPresenter;
    [Tooltip("Presenter диалога подтверждения: показ вопроса и обработка кнопок Да/Нет.")]
    [SerializeField] private ConfirmationDialogPresenter confirmationDialogPresenter;
    [Tooltip("Presenter экрана поражения: показ панели Game Over и обработка кнопок повтора, меню и выхода.")]
    [SerializeField] private GameOverPresenter gameOverPresenter;
    [Tooltip("Presenter выбора улучшения при повышении уровня. Отвечает за панель, тексты вариантов и UI-ввод выбора.")]
    [SerializeField] private PerkSelectionPresenter perkSelectionPresenter;
    [Tooltip("Presenter панели выбора следующей локации. Отвечает только за показ вариантов и обработку нажатий UI.")]
    [SerializeField] private EncounterChoicePresenter encounterChoicePresenter;
    [Tooltip("Presenter панели небоевой локации. Отвечает только за показ заглушки и обработку кнопки действия.")]
    [SerializeField] private NonCombatEncounterPresenter nonCombatEncounterPresenter;
    [Tooltip("Inspector: shield hit material")]
    [SerializeField] private Material shieldHitMaterial;
    [Tooltip("Если включено, встроенное старт-меню отключается и бой начинается сразу после загрузки сцены.")]
    [SerializeField] private bool startDirectlyFromExternalMenu = true;
    [Tooltip("Имя сцены отдельного главного меню.")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Данные")]
    [Tooltip("Список кораблей, доступных игроку для выбора в стартовом меню.")]
    [SerializeField] private List<ShipDataSO> availableShips = new List<ShipDataSO>();
    [Tooltip("Ручной таймлайн для тестирования боя. Используется как резервный вариант, если RunManager не имеет выбранной EncounterSO с WaveTimelineSO.")]
    [SerializeField] public WaveTimelineSO currentTimeline;
    [SerializeField, HideInInspector] private List<EncounterSO> testNextEncounters = new List<EncounterSO>();

    [Header("Фон")]
    [Tooltip("Legacy-настройки слоев фона. Используются только как резерв для BackgroundController, чтобы не потерять старые настройки сцены при переносе.")]
    [SerializeField] private List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();

    [Header("Shield Visuals")]
    [Tooltip("РђРјРїР»РёС‚СѓРґР° РїСѓР»СЊСЃР°С†РёРё РїСЂРѕР·СЂР°С‡РЅРѕСЃС‚Рё С‰РёС‚Р° (fallback, РµСЃР»Рё ShipShieldVisual РЅРµ РЅР°Р·РЅР°С‡РµРЅ).")]
    [SerializeField, Range(0f, 0.6f)] private float shieldPulseAlpha = 0.12f;
    [Tooltip("РЎРєРѕСЂРѕСЃС‚СЊ РїСѓР»СЊСЃР°С†РёРё С‰РёС‚Р° (fallback, РµСЃР»Рё ShipShieldVisual РЅРµ РЅР°Р·РЅР°С‡РµРЅ).")]
    [SerializeField, Min(0.1f)] private float shieldPulseSpeed = 3.2f;
    [Tooltip("Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅР°СЏ СЏСЂРєРѕСЃС‚СЊ С‰РёС‚Р° РІ РјРѕРјРµРЅС‚ РїРѕРїР°РґР°РЅРёСЏ (fallback).")]
    [SerializeField, Range(0f, 2f)] private float shieldHitAlphaBoost = 0.55f;
    [Tooltip("РЎРёР»Р° РїРѕРґРєСЂР°С€РёРІР°РЅРёСЏ С‰РёС‚Р° РїСЂРё РїРѕРїР°РґР°РЅРёРё (fallback).")]
    [SerializeField, Range(0f, 1f)] private float shieldHitTintStrength = 0.65f;
    [Tooltip("Р¦РІРµС‚ РїРѕРґСЃРІРµС‚РєРё С‰РёС‚Р° РїСЂРё РїРѕРїР°РґР°РЅРёРё (fallback).")]
    [SerializeField] private Color shieldHitTint = new Color(0.72f, 0.95f, 1f, 1f);

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
    private readonly List<ModuleState> modules = new List<ModuleState>();
    private readonly List<PerkChoice> activePerks = new List<PerkChoice>();
    private readonly List<ShipCardView> shipCardViews = new List<ShipCardView>();
    private readonly List<UiButtonView> mainMenuButtons = new List<UiButtonView>();
    private readonly List<UiButtonView> settingsButtons = new List<UiButtonView>();
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
    private Transform worldRoot;
    private Transform enemyRoot;
    private Transform projectileRoot;
    private Transform gateTransform;
    private Canvas hudCanvas;
    private TMP_Text gateHintText;
    private TMP_Text statusText;
    private TMP_Text startMenuShipNameText;
    private TMP_Text startMenuRoleText;
    private TMP_Text startMenuDescriptionText;
    private TMP_Text startMenuStatsText;
    private TMP_Text startMenuHintText;
    private TMP_Text hangarTitleText;
    private TMP_Text hangarSubtitleText;
    private TMP_Text mainMenuTitleText;
    private TMP_Text mainMenuSubtitleText;
    private TMP_Text settingsTitleText;
    private TMP_Text settingsSubtitleText;
    private TMP_Text settingsLanguageLabelText;
    private TMP_Text settingsFpsLabelText;
    private TMP_Text joystickHintText;
    private GameObject startMenuObject;
    private GameObject mainMenuPanelObject;
    private GameObject hangarPanelObject;
    private GameObject settingsPanelObject;
    private GameObject joystickRootObject;
    private Image startMenuPreviewImage;
    private Image startButtonImage;
    private TMP_Text startButtonText;
    private RectTransform startButtonRect;
    private UiButtonView newGameButtonView;
    private UiButtonView continueButtonView;
    private UiButtonView settingsMenuButtonView;
    private UiButtonView exitButtonView;
    private UiButtonView hangarBackButtonView;
    private UiButtonView settingsBackButtonView;
    private UiButtonView languageRuButtonView;
    private UiButtonView languageEngButtonView;
    private UiButtonView[] fpsButtonViews = new UiButtonView[4];
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
    private bool levelUpPending;
    private bool gameOver;
    private bool gameStarted;
    private bool gamePaused;
    private bool pauseSettingsOpened;
    private float encounterStartHullPercent = 1f;
    private int selectedShipIndex;
    private int selectedFpsIndex = 2;
    private LanguageOption currentLanguage = LanguageOption.RU;
    private StartMenuPage startMenuPage = StartMenuPage.Main;
    private bool useVirtualJoystick;
    private bool joystickDragging;
    private Vector2 joystickVector;
    private bool suppressPointerMovementUntilRelease;
    private int enemySpawnSequence;

    private enum ConfirmAction
    {
        None,
        ReturnToMainMenu,
        ExitGame
    }

    private ConfirmAction pendingConfirmAction = ConfirmAction.None;

    public event Action<ShipEquipmentState> EquipmentStateChanged;
    public ShipEquipmentState CurrentEquipmentState => playerShipController != null ? playerShipController.EquipmentState : equipmentState;

    private void OnValidate()
    {
    }

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
    }

    private void EnsureCombatAudioController()
    {
        if (combatAudioController == null)
        {
            combatAudioController = GetComponent<CombatAudioController>();
        }

        if (combatAudioController == null)
        {
            combatAudioController = FindAnyObjectByType<CombatAudioController>(FindObjectsInactive.Include);
        }

        if (combatAudioController == null)
        {
            combatAudioController = gameObject.AddComponent<CombatAudioController>();
        }

        if (player != null)
        {
            combatAudioController.SetPlayerTransform(player.Transform);
        }
    }

    private void EnsureCombatLogPresenter()
    {
        if (combatLogPresenter == null)
        {
            combatLogPresenter = GetComponent<CombatLogPresenter>();
        }

        if (combatLogPresenter == null)
        {
            combatLogPresenter = FindAnyObjectByType<CombatLogPresenter>(FindObjectsInactive.Include);
        }

        if (combatLogPresenter == null)
        {
            combatLogPresenter = gameObject.AddComponent<CombatLogPresenter>();
        }
    }

    private void EnsureCombatCameraController()
    {
        if (combatCameraController == null)
        {
            combatCameraController = GetComponent<CombatCameraController>();
        }

        if (combatCameraController == null)
        {
            combatCameraController = FindAnyObjectByType<CombatCameraController>(FindObjectsInactive.Include);
        }

        if (combatCameraController == null)
        {
            combatCameraController = gameObject.AddComponent<CombatCameraController>();
        }

        combatCameraController.Initialize(mainCamera != null ? mainCamera : Camera.main);
        mainCamera = combatCameraController.CurrentCamera;
        if (player != null)
        {
            combatCameraController.SetTarget(player.Transform);
        }
    }

    private void EnsureEncounterChoicePresenter()
    {
        if (encounterChoicePresenter == null)
        {
            encounterChoicePresenter = GetComponent<EncounterChoicePresenter>();
        }

        if (encounterChoicePresenter == null)
        {
            encounterChoicePresenter = FindAnyObjectByType<EncounterChoicePresenter>(FindObjectsInactive.Include);
        }

        if (encounterChoicePresenter == null)
        {
            encounterChoicePresenter = gameObject.AddComponent<EncounterChoicePresenter>();
        }
    }

    private void EnsureNonCombatEncounterPresenter()
    {
        if (nonCombatEncounterPresenter == null)
        {
            nonCombatEncounterPresenter = GetComponent<NonCombatEncounterPresenter>();
        }

        if (nonCombatEncounterPresenter == null)
        {
            nonCombatEncounterPresenter = FindAnyObjectByType<NonCombatEncounterPresenter>(FindObjectsInactive.Include);
        }

        if (nonCombatEncounterPresenter == null)
        {
            nonCombatEncounterPresenter = gameObject.AddComponent<NonCombatEncounterPresenter>();
        }
    }

    private void EnsureEncounterFlowController()
    {
        if (encounterFlowController == null)
        {
            encounterFlowController = GetComponent<EncounterFlowController>();
        }

        if (encounterFlowController == null)
        {
            encounterFlowController = FindAnyObjectByType<EncounterFlowController>(FindObjectsInactive.Include);
        }

        if (encounterFlowController == null)
        {
            encounterFlowController = gameObject.AddComponent<EncounterFlowController>();
        }

        encounterFlowController.ImportFallbackEncounters(testNextEncounters);
        encounterFlowController.Initialize(StartSelectedCombatEncounter, GetPlayerHullPercent, RestorePlayerHull, LogMessage);
    }

    private void EnsureTimelineSpawnController()
    {
        if (timelineSpawnController == null)
        {
            timelineSpawnController = GetComponent<TimelineSpawnController>();
        }

        if (timelineSpawnController == null)
        {
            timelineSpawnController = FindAnyObjectByType<TimelineSpawnController>(FindObjectsInactive.Include);
        }

        if (timelineSpawnController == null)
        {
            timelineSpawnController = gameObject.AddComponent<TimelineSpawnController>();
        }

        timelineSpawnController.SetCamera(mainCamera != null ? mainCamera : Camera.main);
        timelineSpawnController.SetSpawnEnemyCallback(SpawnEnemyFromTimeline);
    }

    private void EnsureEnemySpawner()
    {
        if (enemySpawner == null)
        {
            enemySpawner = GetComponent<EnemySpawner>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        }

        if (enemySpawner == null)
        {
            enemySpawner = gameObject.AddComponent<EnemySpawner>();
        }

        enemySpawner.Initialize(poolService, enemyRoot, shieldHitMaterial, ringSprite, OnEnemyDamageApplied, AttachWeaponVisual);
    }

    private void EnsureTargetingController()
    {
        if (targetingController == null)
        {
            targetingController = GetComponent<TargetingController>();
        }

        if (targetingController == null)
        {
            targetingController = FindAnyObjectByType<TargetingController>(FindObjectsInactive.Include);
        }

        if (targetingController == null)
        {
            targetingController = gameObject.AddComponent<TargetingController>();
        }

        targetingController.Initialize(player, enemies, worldRoot, mainCamera != null ? mainCamera : Camera.main, LogMessage, Localize);
    }

    private void EnsureMoveCommandVisualController()
    {
        if (moveCommandVisualController == null)
        {
            moveCommandVisualController = GetComponent<MoveCommandVisualController>();
        }

        if (moveCommandVisualController == null)
        {
            moveCommandVisualController = FindAnyObjectByType<MoveCommandVisualController>(FindObjectsInactive.Include);
        }

        if (moveCommandVisualController == null)
        {
            moveCommandVisualController = gameObject.AddComponent<MoveCommandVisualController>();
        }

        moveCommandVisualController.Initialize(player, worldRoot, circleSprite);
    }

    private void EnsureMinimapController()
    {
        if (minimapController == null)
        {
            minimapController = GetComponent<MinimapController>();
        }

        if (minimapController == null)
        {
            minimapController = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);
        }

        if (minimapController == null)
        {
            minimapController = gameObject.AddComponent<MinimapController>();
        }

        minimapController.SetPlayer(player);
    }

    private void EnsureBackgroundController()
    {
        if (backgroundController == null)
        {
            backgroundController = GetComponent<BackgroundController>();
        }

        if (backgroundController == null)
        {
            backgroundController = FindAnyObjectByType<BackgroundController>(FindObjectsInactive.Include);
        }

        if (backgroundController == null)
        {
            backgroundController = gameObject.AddComponent<BackgroundController>();
        }

        backgroundController.Initialize(worldRoot, player, poolService, backgroundParallaxService, circleSprite, backgroundLayers);
    }

    private void EnsurePlayerShipController()
    {
        if (playerShipController == null)
        {
            playerShipController = GetComponent<PlayerShipController>();
        }

        if (playerShipController == null)
        {
            playerShipController = FindAnyObjectByType<PlayerShipController>(FindObjectsInactive.Include);
        }

        if (playerShipController == null)
        {
            playerShipController = gameObject.AddComponent<PlayerShipController>();
        }

        playerShipController.Initialize(equipmentState, shieldHitMaterial, ringSprite, _ => NotifyEquipmentStateChanged());
        player = playerShipController.Player;
    }

    private void EnsureCombatHudPresenter()
    {
        if (combatHudPresenter == null)
        {
            combatHudPresenter = GetComponent<CombatHudPresenter>();
        }

        if (combatHudPresenter == null)
        {
            combatHudPresenter = FindAnyObjectByType<CombatHudPresenter>(FindObjectsInactive.Include);
        }

        if (combatHudPresenter == null)
        {
            combatHudPresenter = gameObject.AddComponent<CombatHudPresenter>();
        }

        combatHudPresenter.Initialize(
            this,
            uiFactory,
            uiFont,
            squareSprite,
            slotUiPrefab,
            healthBar,
            scoreText,
            waveText,
            equipmentUiController,
            Localize);
        equipmentUiController = combatHudPresenter.EquipmentUiController;
        statusText = combatHudPresenter.StatusText;
    }

    private void EnsurePauseMenuPresenter()
    {
        if (pauseMenuPresenter == null)
        {
            pauseMenuPresenter = GetComponent<PauseMenuPresenter>();
        }

        if (pauseMenuPresenter == null)
        {
            pauseMenuPresenter = FindAnyObjectByType<PauseMenuPresenter>(FindObjectsInactive.Include);
        }

        if (pauseMenuPresenter == null)
        {
            pauseMenuPresenter = gameObject.AddComponent<PauseMenuPresenter>();
        }

        pauseMenuPresenter.Initialize(Localize);
        pauseMenuPresenter.OnResume = ResumeRun;
        pauseMenuPresenter.OnOpenSettings = OpenPauseSettings;
        pauseMenuPresenter.OnReturnToMenuRequested = () => RequestConfirmation(ConfirmAction.ReturnToMainMenu, Localize("confirm_to_menu"));
        pauseMenuPresenter.OnExitRequested = () => RequestConfirmation(ConfirmAction.ExitGame, Localize("confirm_exit"));
    }

    private void EnsureConfirmationDialogPresenter()
    {
        if (confirmationDialogPresenter == null)
        {
            confirmationDialogPresenter = GetComponent<ConfirmationDialogPresenter>();
        }

        if (confirmationDialogPresenter == null)
        {
            confirmationDialogPresenter = FindAnyObjectByType<ConfirmationDialogPresenter>(FindObjectsInactive.Include);
        }

        if (confirmationDialogPresenter == null)
        {
            confirmationDialogPresenter = gameObject.AddComponent<ConfirmationDialogPresenter>();
        }

        confirmationDialogPresenter.Initialize(Localize);
    }

    private void EnsureGameOverPresenter()
    {
        if (gameOverPresenter == null)
        {
            gameOverPresenter = GetComponent<GameOverPresenter>();
        }

        if (gameOverPresenter == null)
        {
            gameOverPresenter = FindAnyObjectByType<GameOverPresenter>(FindObjectsInactive.Include);
        }

        if (gameOverPresenter == null)
        {
            gameOverPresenter = gameObject.AddComponent<GameOverPresenter>();
        }

        gameOverPresenter.Initialize(Localize);
        gameOverPresenter.OnRetry = () => StartRun();
        gameOverPresenter.OnReturnToMenuRequested = () => RequestConfirmation(ConfirmAction.ReturnToMainMenu, Localize("confirm_to_menu"));
        gameOverPresenter.OnExitRequested = () => RequestConfirmation(ConfirmAction.ExitGame, Localize("confirm_exit"));
    }

    private void EnsurePerkSelectionPresenter()
    {
        if (perkSelectionPresenter == null)
        {
            perkSelectionPresenter = GetComponent<PerkSelectionPresenter>();
        }

        if (perkSelectionPresenter == null)
        {
            perkSelectionPresenter = FindAnyObjectByType<PerkSelectionPresenter>(FindObjectsInactive.Include);
        }

        if (perkSelectionPresenter == null)
        {
            perkSelectionPresenter = gameObject.AddComponent<PerkSelectionPresenter>();
        }

        perkSelectionPresenter.Initialize(Localize);
        perkSelectionPresenter.OnPerkSelected = ApplyPerk;
    }

    private float GetPlayerHullPercent()
    {
        return playerShipController != null ? playerShipController.GetHullPercent() : player != null ? player.HullPercent : 0f;
    }

    private WaveTimelineSO GetActiveTimeline()
    {
        return encounterFlowController != null
            ? encounterFlowController.GetActiveTimeline(currentTimeline)
            : currentTimeline;
    }

    private void Awake()
    {
        EnsureServices();
        EnsureCombatAudioController();
        EnsureCombatLogPresenter();
        EnsureCombatCameraController();
        EnsureEncounterChoicePresenter();
        EnsureNonCombatEncounterPresenter();
        EnsurePauseMenuPresenter();
        EnsureConfirmationDialogPresenter();
        EnsureGameOverPresenter();
        EnsurePerkSelectionPresenter();
        EnsureEncounterFlowController();
        EnsureTimelineSpawnController();
        ValidateSerializedReferences();
        useVirtualJoystick = platformService.ShouldUseVirtualJoystick();
        CreateSprites();
        CreateStarterShips();
        BuildWorld();
        EnsurePlayerShipController();
        EnsureEnemySpawner();
        EnsureTargetingController();
        EnsureMoveCommandVisualController();
        EnsureMinimapController();
        SpawnPlayer();
        EnsureBackgroundController();
        EnsureCombatAudioController();
        EnsureCombatCameraController();
        EnsureTimelineSpawnController();
        EnsureTargetingController();
        EnsureMoveCommandVisualController();
        EnsureMinimapController();
        SelectShip(GetInitialShipIndex());
        BuildHud();
        EnsureEncounterChoiceUi();
        EnsureNonCombatUi();
        ApplyPerformanceSettings();
        RefreshLocalizedTexts();

        if (startDirectlyFromExternalMenu)
        {
            ShowStartMenu(false);
            StartRun();
        }
        else
        {
            ShowStartMenu(true);
            LogMessage(Localize("log_docked"));
            LogMessage(Localize("log_choose_hull"));
            UpdateHud();
        }
    }

    private void OnDestroy()
    {
        if (equipmentUiController != null)
        {
            equipmentUiController.Bind(null);
        }

        backgroundController?.Cleanup();
        minimapController?.Cleanup();
    }

    private void ValidateSerializedReferences()
    {
        TryResolveLegacyHudReferences();

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
        if (GetActiveTimeline() == null)
        {
            Debug.LogError("SpaceCombatSceneController: не назначен активный WaveTimelineSO. Укажите Current Timeline для ручного теста или EncounterSO с Wave Timeline через RunManager.", this);
        }
    }

    private void TryResolveLegacyHudReferences()
    {
        GameObject inspectorUi = FindSceneGameObject("InspectorUI");
        if (inspectorUi == null)
        {
            return;
        }

        if (healthBar == null)
        {
            healthBar = FindComponentInChildrenByName<Slider>(inspectorUi.transform, "HealthBar");
        }
        if (scoreText == null)
        {
            scoreText = FindComponentInChildrenByName<TMP_Text>(inspectorUi.transform, "ScoreText");
        }
        if (waveText == null)
        {
            waveText = FindComponentInChildrenByName<TMP_Text>(inspectorUi.transform, "WaveText");
        }
    }

    private static T FindComponentInChildrenByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private void EnsureDataAssets()
    {
        availableShips ??= new List<ShipDataSO>();
        availableShips.RemoveAll(ship => ship == null);
        backgroundLayers ??= new List<BackgroundLayerConfig>();
        if (shieldHitMaterial == null)
        {
            shieldHitMaterial = Resources.Load<Material>("Materials/ShieldHit_SG");
        }
    }

    private void Update()
    {
        if (gamePaused)
        {
            HandlePausedInput();
            UpdateHud();
            return;
        }

        if (!gameStarted)
        {
            HandleStartMenuInput();
            UpdateHud();
            return;
        }

        if (gameOver)
        {
            HandleGameOverInput();
            UpdateHud();
            return;
        }

        if (IsNonCombatPanelVisible())
        {
            HandleNonCombatInput();
            UpdateHud();
            return;
        }

        if (IsEncounterChoicePanelVisible())
        {
            HandleEncounterChoiceInput();
            UpdateHud();
            return;
        }

        float deltaTime = Time.deltaTime;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetPaused(true);
            UpdateHud();
            return;
        }

        if (levelUpPending)
        {
            UpdatePerkSelectionInput();
            UpdateHud();
            return;
        }

        HandleInput(deltaTime);
        UpdatePlayer(deltaTime);
        UpdateCombat(deltaTime);
        UpdateTimelineSpawner(deltaTime);
        TryCompleteEncounter();
        backgroundController?.Tick();
        UpdateEffects(deltaTime);
        UpdateVisuals();
        UpdateHud();
    }

    private void HandleStartMenuInput()
    {
        if (HandleConfirmationInput())
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (startMenuPage == StartMenuPage.Hangar || startMenuPage == StartMenuPage.Settings)
                {
                    if (pauseSettingsOpened && gamePaused)
                    {
                        pauseSettingsOpened = false;
                        ShowStartMenu(false);
                        ShowPauseMenu(true);
                    }
                    else
                    {
                        SetStartMenuPage(StartMenuPage.Main);
                    }
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
                if (IsButtonClicked(continueButtonView, position))
                {
                    ResumeRun();
                    return;
                }

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
                    RequestConfirmation(ConfirmAction.ExitGame, Localize("confirm_exit"));
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
                    if (pauseSettingsOpened && gamePaused)
                    {
                        pauseSettingsOpened = false;
                        ShowStartMenu(false);
                        ShowPauseMenu(true);
                    }
                    else
                    {
                        SetStartMenuPage(StartMenuPage.Main);
                    }
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
        if (combatCameraController != null)
        {
            combatCameraController.SetTarget(player != null ? player.Transform : null);
            combatCameraController.LateTick(Time.deltaTime, player != null ? player.Velocity : Vector2.zero);
            mainCamera = combatCameraController.CurrentCamera;
        }

        minimapController?.Tick();
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
        combatHudPresenter?.RefreshLocalizedTexts();
        if (combatLogPresenter != null) combatLogPresenter.SetTitle(Localize("combat_log"));
        if (gateHintText != null) gateHintText.text = Localize("warp_inactive");
        perkSelectionPresenter?.RefreshLocalizedTexts();
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
            "РЎР±Р°Р»Р°РЅСЃРёСЂРѕРІР°РЅРЅС‹Р№ С„СЂРµРіР°С‚",
            "Universal hull with reliable capacitor and solid survivability. Good first choice for learning the combat loop.",
            "РЈРЅРёРІРµСЂСЃР°Р»СЊРЅС‹Р№ РєРѕСЂРїСѓСЃ СЃ РЅР°РґРµР¶РЅРѕР№ СЌРЅРµСЂРіРµС‚РёРєРѕР№ Рё С…РѕСЂРѕС€РµР№ Р¶РёРІСѓС‡РµСЃС‚СЊСЋ.",
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
            1.2f,
            2,
            4,
            1f,
            1f,
            new Color(0.28f, 0.6f, 0.94f, 1f),
            new Color(0.38f, 0.76f, 1f, 0.72f)));

        availableShips.Add(CreateRuntimeShipData(
            "Bulwark",
            "Heavy Cruiser",
            "РўСЏР¶С‘Р»С‹Р№ РєСЂРµР№СЃРµСЂ",
            "Slow but durable platform with the best shields and armor. Repairs are stronger and capacitor is deep enough for long fights.",
            "РњРµРґР»РµРЅРЅС‹Р№, РЅРѕ РѕС‡РµРЅСЊ РїСЂРѕС‡РЅС‹Р№ РєРѕСЂР°Р±Р»СЊ СЃ РјРѕС‰РЅС‹РјРё С‰РёС‚Р°РјРё Рё Р±СЂРѕРЅС‘Р№.",
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
            1.15f,
            3,
            4,
            0.94f,
            1.22f,
            new Color(0.18f, 0.78f, 0.8f, 1f),
            new Color(0.42f, 1f, 0.92f, 0.72f)));

        availableShips.Add(CreateRuntimeShipData(
            "Raptor",
            "Strike Interceptor",
            "РЈРґР°СЂРЅС‹Р№ РїРµСЂРµС…РІР°С‚С‡РёРє",
            "Fast hunter with stronger volleys and snappier capacitor recovery. Lower defenses reward mobility and target focus.",
            "Р‘С‹СЃС‚СЂС‹Р№ РѕС…РѕС‚РЅРёРє СЃ РїРѕРІС‹С€РµРЅРЅС‹Рј СѓСЂРѕРЅРѕРј. РўСЂРµР±СѓРµС‚ РјРѕР±РёР»СЊРЅРѕСЃС‚Рё Рё РїСЂРёРѕСЂРёС‚РµС‚Р° С†РµР»РµР№.",
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
            1.3f,
            2,
            3,
            1.2f,
            0.9f,
            new Color(1f, 0.58f, 0.18f, 1f),
            new Color(1f, 0.75f, 0.36f, 0.72f)));
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
        float capacitorRechargeRate,
        int weaponSlotCount,
        int moduleSlotCount,
        float damageMultiplier,
        float repairMultiplier,
        Color accentColor,
        Color auraColor)
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
        data.capacitorRechargeRate = capacitorRechargeRate;
        data.scoreReward = 40;
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
            data.startingWeapons.Add(null);
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

        BuildGate();
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
        EnsurePlayerShipController();
        player = playerShipController.SpawnPlayer(worldRoot);

        if (combatAudioController != null)
        {
            combatAudioController.SetPlayerTransform(player != null ? player.Transform : null);
        }
        if (combatCameraController != null && player != null)
        {
            combatCameraController.SetTarget(player.Transform);
        }
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
        if (ship == null)
        {
            return;
        }

        EnsurePlayerShipController();
        if (playerShipController.Player == null)
        {
            playerShipController.SpawnPlayer(worldRoot);
        }

        playerShipController.ApplyShipDefinition(ship, resetProgress);
        player = playerShipController.Player;
        CreateModules(ship.moduleSlotCount, ship);
    }

    private void OnEnemyDamageApplied(EnemyShip enemy, DamageInfo info, DamageResolutionResult result)
    {
        if (enemy == null || result.AppliedShieldDamage <= 0f)
        {
            return;
        }

        if (enemy.ShieldVisual != null)
        {
            enemy.ShieldVisual.PlayImpact(info.HitPoint, result.AppliedShieldDamage);
        }
    }

    private void NotifyEquipmentStateChanged()
    {
        ShipEquipmentState state = CurrentEquipmentState;
        EquipmentStateChanged?.Invoke(state);
        if (equipmentUiController != null)
        {
            equipmentUiController.Refresh(state);
        }
    }

    private void StartRun(bool resetRunState = true)
    {
        if (availableShips == null || availableShips.Count == 0)
        {
            Debug.LogError("SpaceCombatSceneController: no ships configured in availableShips.");
            return;
        }
        selectedShipIndex = Mathf.Clamp(selectedShipIndex, 0, availableShips.Count - 1);
        EnsureEncounterFlowController();
        if (resetRunState && encounterFlowController != null)
        {
            encounterFlowController.StartRun();
        }

        ShowStartMenu(false);
        if (encounterFlowController != null)
        {
            encounterFlowController.ResetEncounterCompletionState();
        }
        gameStarted = true;
        gameOver = false;
        gamePaused = false;
        pauseSettingsOpened = false;
        pendingConfirmAction = ConfirmAction.None;
        confirmationDialogPresenter?.Hide();
        levelUpPending = false;
        wave = 1;
        enemySpawnSequence = 0;
        targetingController?.ClearTarget();
        activePerks.Clear();
        perkSelectionPresenter?.Hide();
        ShowGameOverPanel(false);
        if (combatLogPresenter != null)
        {
            combatLogPresenter.Clear();
        }
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
        if (timelineSpawnController != null)
        {
            timelineSpawnController.ResetRuntime();
            wave = timelineSpawnController.CurrentWave;
        }
        ResetModules();
        if (resetRunState)
        {
            ApplyShipDefinition(availableShips[selectedShipIndex], true);
        }
        else
        {
            PreparePlayerForNextEncounter();
        }
        encounterStartHullPercent = player != null ? player.HullPercent : 1f;
        LogMessage(Localize("log_launch") + availableShips[selectedShipIndex].displayName);
        LogMessage(Localize("log_sector_scan"));
    }

    private void PreparePlayerForNextEncounter()
    {
        playerShipController?.PrepareForNextEncounter();
        player = playerShipController != null ? playerShipController.Player : player;
    }

    private void ResumeRun()
    {
        if (!gameStarted || gameOver)
        {
            return;
        }

        SetPaused(false);
        ShowStartMenu(false);
    }

    private void SetPaused(bool paused)
    {
        if (!gameStarted || gameOver)
        {
            paused = false;
        }

        gamePaused = paused;
        ShowPauseMenu(paused);
        if (paused)
        {
            ShowStartMenu(false);
        }
    }

    private bool IsEncounterChoicePanelVisible()
    {
        return encounterFlowController != null && encounterFlowController.IsEncounterChoiceVisible;
    }

    private bool IsNonCombatPanelVisible()
    {
        return encounterFlowController != null && encounterFlowController.IsNonCombatVisible;
    }

    private void HandleNonCombatInput()
    {
        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition) &&
            encounterFlowController != null &&
            encounterFlowController.TryHandleNonCombatPointer(pointerPosition))
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            encounterFlowController?.CompleteActiveNonCombatEncounter();
        }
    }

    private void RestorePlayerHull(float percent)
    {
        playerShipController?.RestoreHull(percent);
        player = playerShipController != null ? playerShipController.Player : player;
    }

    private void TryCompleteEncounter()
    {
        if (gameOver || levelUpPending || encounterFlowController == null)
        {
            return;
        }

        bool timelineFinished = timelineSpawnController != null && timelineSpawnController.IsTimelineFinished;
        LocationNodeType completedNodeType = LocationNodeType.Combat;
        RunManager flowRunManager = encounterFlowController.RunManager;
        if (flowRunManager != null && flowRunManager.CurrentEncounter != null)
        {
            completedNodeType = flowRunManager.CurrentEncounter.nodeType;
        }

        float hullPercent = player != null ? player.HullPercent : 0f;
        float damageTaken = Mathf.Clamp01(encounterStartHullPercent - hullPercent);
        EncounterCompletionContext context = new EncounterCompletionContext(
            completedNodeType,
            hullPercent,
            damageTaken,
            timelineSpawnController != null ? timelineSpawnController.GameTimer : 0f,
            0, // TODO Stage 3+: expose killed enemy count from combat cleanup.
            timelineFinished,
            enemies.Count);

        encounterFlowController.TryCompleteCombatEncounter(context);
    }

    private void HandleEncounterChoiceInput()
    {
        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition) &&
            encounterFlowController != null &&
            encounterFlowController.TryHandleChoicePointer(pointerPosition))
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            encounterFlowController?.TrySelectChoiceIndex(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            encounterFlowController?.TrySelectChoiceIndex(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            encounterFlowController?.TrySelectChoiceIndex(2);
        }
    }

    private void StartSelectedCombatEncounter(EncounterSO encounter)
    {
        StartRun(false);
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
            equipmentUiController.Refresh(CurrentEquipmentState);
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
                string speedLabel = Localize("stat_speed");
                string shieldLabel = Localize("stat_shield");
                string armorLabel = Localize("stat_armor");
                string hullLabel = Localize("stat_hull");
                string capacitorLabel = Localize("stat_capacitor");
                string rechargeLabel = Localize("stat_recharge");
                string weaponSlotsLabel = Localize("stat_weapon_slots");
                string moduleSlotsLabel = Localize("stat_module_slots");

                startMenuStatsText.text =
                    speedLabel + ": " + ship.maxSpeed.ToString("0.0") +
                    "    " + shieldLabel + ": " + Mathf.RoundToInt(ship.maxShield) +
                    "    " + armorLabel + ": " + Mathf.RoundToInt(ship.maxArmor) +
                    "    " + hullLabel + ": " + Mathf.RoundToInt(ship.maxHull) +
                    "\n" + capacitorLabel + ": " + Mathf.RoundToInt(ship.capacitor) +
                    "    " + rechargeLabel + ": " + ship.capacitorRechargeTime.ToString("0") + "s" +
                    "    " + weaponSlotsLabel + ": " + Mathf.Max(0, ship.weaponSlotCount) +
                    "    " + moduleSlotsLabel + ": " + Mathf.Max(0, ship.moduleSlotCount);
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
                string shieldShort = Localize("stat_shield");
                string armorShort = Localize("stat_armor");
                string speedShort = Localize("stat_speed");
                string gunsShort = Localize("stat_guns");

                shipCardViews[i].Stats.text =
                    shieldShort + " " + Mathf.RoundToInt(cardShip.maxShield) +
                    "  " + armorShort + " " + Mathf.RoundToInt(cardShip.maxArmor) +
                    "\n" + speedShort + " " + cardShip.maxSpeed.ToString("0.0") +
                    "  " + gunsShort + " " + Mathf.Max(0, cardShip.weaponSlotCount);
            }
        }

        if (startMenuHintText != null)
        {
            startMenuHintText.text = useVirtualJoystick ? Localize("hangar_hint_mobile") : Localize("hangar_hint_desktop");
        }
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

    private void CreateModules(int moduleSlotCount, ShipDataSO ship)
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

    private string NextEnemyId()
    {
        enemySpawnSequence++;
        return "E-" + enemySpawnSequence.ToString("0000");
    }

    private bool SpawnEnemyFromTimeline(ShipDataSO shipData, Vector3 position, float levelScale)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            return false;
        }

        EnemyShip enemy = CreateEnemy(NextEnemyId(), shipData, position, levelScale);
        if (enemy == null)
        {
            return false;
        }

        enemies.Add(enemy);
        EnsureTargetingController();
        if (targetingController != null && !targetingController.HasPlayerTarget)
        {
            targetingController.SetTargetEnemy(enemy);
        }

        return true;
    }

    private void SpawnEnemyFromTimeline(ShipDataSO shipData, Vector3 position)
    {
        float levelScale = timelineSpawnController != null ? timelineSpawnController.GetLevelScale() : 1f;
        SpawnEnemyFromTimeline(shipData, position, levelScale);
    }

    public bool SpawnEnemyFromExternalShipData(ShipDataSO shipData, Vector3 position)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            return false;
        }

        SpawnEnemyFromTimeline(shipData, position);
        return true;
    }

    public bool SpawnEnemyFromExternalPrefab(GameObject enemyPrefab, Vector3 position)
    {
        if (enemyPrefab == null || availableShips == null)
        {
            return false;
        }

        for (int i = 0; i < availableShips.Count; i++)
        {
            ShipDataSO ship = availableShips[i];
            if (ship == null || ship.shipPrefab == null)
            {
                continue;
            }

            if (ship.shipPrefab == enemyPrefab)
            {
                SpawnEnemyFromTimeline(ship, position);
                return true;
            }
        }

        return false;
    }

    public void AddExternalExperience(int amount)
    {
        if (player == null || amount <= 0)
        {
            return;
        }

        player.AddExperience(amount);
        while (player.Experience >= player.ExperienceToNext && player.ExperienceToNext > 0)
        {
            BeginLevelUp();
            if (levelUpPending)
            {
                break;
            }
        }
    }

    private EnemyShip CreateEnemy(string id, ShipDataSO shipData, Vector3 position, float levelScale)
    {
        EnsureEnemySpawner();
        return enemySpawner != null
            ? enemySpawner.SpawnEnemy(id, shipData, position, levelScale, enemies.Count)
            : null;
    }

    private static void RefreshWeaponVisuals(List<WeaponDataSO> weapons, List<Transform> muzzles)
    {
        if (muzzles == null)
        {
            return;
        }

        for (int i = 0; i < muzzles.Count; i++)
        {
            WeaponDataSO weapon = weapons != null && i < weapons.Count ? weapons[i] : null;
            AttachWeaponVisual(weapon, muzzles[i]);
        }
    }

    private static void AttachWeaponVisual(WeaponDataSO weapon, Transform muzzleTransform)
    {
        Transform mountTransform = GetWeaponMountTransform(muzzleTransform);
        if (mountTransform == null)
        {
            return;
        }

        GameObject existingVisual = FindExistingWeaponVisual(mountTransform);
        if (existingVisual != null)
        {
            Destroy(existingVisual);
        }

        if (weapon == null || weapon.visualPrefab == null)
        {
            return;
        }

        if (weapon.projectilePrefab != null && weapon.visualPrefab == weapon.projectilePrefab)
        {
            // Prevent attaching flying projectile art as static muzzle visual.
            return;
        }

        GameObject visual = Instantiate(weapon.visualPrefab, mountTransform);
        visual.name = "WeaponVisualInstance";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = weapon.visualPrefab.transform.localRotation;
        visual.transform.localScale = Vector3.one;
    }

    private static Transform GetWeaponMountTransform(Transform muzzleTransform)
    {
        if (muzzleTransform == null)
        {
            return null;
        }

        return muzzleTransform.parent != null ? muzzleTransform.parent : muzzleTransform;
    }

    private static GameObject FindExistingWeaponVisual(Transform mountTransform)
    {
        if (mountTransform == null)
        {
            return null;
        }

        for (int i = 0; i < mountTransform.childCount; i++)
        {
            Transform child = mountTransform.GetChild(i);
            if (string.Equals(child.name, "WeaponVisualInstance", StringComparison.OrdinalIgnoreCase))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Transform FindWeaponMuzzle(Transform root, int index)
    {
        if (root == null)
        {
            return null;
        }

        Transform slotsRoot = FindDirectChild(root, "WeaponSlots");
        if (slotsRoot == null)
        {
            slotsRoot = root;
        }

        Transform indexedSlot = FindDirectChild(slotsRoot, "WeaponSlot_" + (index + 1));
        if (indexedSlot != null)
        {
            Transform muzzle = FindMuzzleTransform(indexedSlot, index);
            return muzzle != null ? muzzle : indexedSlot;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string name = children[i].name;
            if (name.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("weaponslot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return children[i];
            }
        }

        return root;
    }

    private static Transform FindMuzzleTransform(Transform slot, int index)
    {
        if (slot == null)
        {
            return null;
        }

        string indexedMountName = "WeaponMount_" + (index + 1);
        Transform indexedMount = FindDirectChild(slot, indexedMountName);
        if (indexedMount != null)
        {
            Transform indexedMountMuzzle = FindDirectChild(indexedMount, "Muzzle");
            if (indexedMountMuzzle != null)
            {
                return indexedMountMuzzle;
            }
        }

        Transform directMuzzle = FindDirectChild(slot, "Muzzle");
        if (directMuzzle != null)
        {
            return directMuzzle;
        }

        Transform[] children = slot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string childName = children[i].name;
            if (childName.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("firepoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("projectileorigin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return children[i];
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
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
        if (projectileRoot == null)
        {
            return;
        }

        List<Transform> children = new List<Transform>();
        for (int i = 0; i < projectileRoot.childCount; i++)
        {
            children.Add(projectileRoot.GetChild(i));
        }

        for (int i = 0; i < children.Count; i++)
        {
            ProjectileBehaviour projectile = children[i].GetComponent<ProjectileBehaviour>();
            if (projectile != null)
            {
                projectile.ForceDespawn();
            }
            else
            {
                Destroy(children[i].gameObject);
            }
        }
    }

    private void BuildHud()
    {
        Transform uiRoot = ResolveInspectorUiRoot();
        GameObject canvasObject = uiRoot.gameObject;
        hudCanvas = canvasObject.GetComponent<Canvas>();
        if (hudCanvas == null)
        {
            hudCanvas = canvasObject.AddComponent<Canvas>();
        }
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1400f, 900f);
        scaler.matchWidthOrHeight = 0.6f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (HasAuthoredInspectorHud(uiRoot))
        {
            EnsureCombatHudPresenter();
            combatHudPresenter.BindOrBuild(uiRoot);
            equipmentUiController = combatHudPresenter.EquipmentUiController;
            statusText = combatHudPresenter.StatusText;
            BindCombatLogPanel(uiRoot);
            BindGateHint(uiRoot);
            BindModulePanel(uiRoot);
            EnsurePauseMenuPresenter();
            pauseMenuPresenter.Bind(uiRoot);
            EnsurePerkSelectionPresenter();
            perkSelectionPresenter.Bind(uiRoot);
            EnsureGameOverPresenter();
            gameOverPresenter.Bind(uiRoot);
            EnsureConfirmationDialogPresenter();
            confirmationDialogPresenter.Bind(uiRoot);
            BindStartMenu(uiRoot);
            BindVirtualJoystick(uiRoot);
            EnsureMinimapController();
            minimapController?.Initialize(player, uiRoot);
            return;
        }

        Image rootBackground = CreateImage("Frame", uiRoot, new Color(0.01f, 0.015f, 0.02f, 0f));
        RectTransform rootRect = rootBackground.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        EnsureCombatHudPresenter();
        combatHudPresenter.BindOrBuild(uiRoot);
        equipmentUiController = combatHudPresenter.EquipmentUiController;
        statusText = combatHudPresenter.StatusText;
        CreateCombatLogPanel(uiRoot);
        CreateGateHint(uiRoot);
        CreateModuleHud(uiRoot);
        EnsurePauseMenuPresenter();
        pauseMenuPresenter.Build(uiRoot, uiFactory, uiFont, squareSprite);
        CreateStatusLabel(uiRoot);
        EnsurePerkSelectionPresenter();
        perkSelectionPresenter.Build(uiRoot, uiFactory, uiFont, squareSprite);
        EnsureGameOverPresenter();
        gameOverPresenter.Build(uiRoot, uiFactory, uiFont, squareSprite);
        EnsureConfirmationDialogPresenter();
        confirmationDialogPresenter.Build(uiRoot, uiFactory, uiFont, squareSprite);
        CreateStartMenu(uiRoot);
        if (useVirtualJoystick)
        {
            CreateVirtualJoystick(uiRoot);
        }

        EnsureMinimapController();
        minimapController?.Initialize(player, uiRoot);
    }

    private Transform ResolveInspectorUiRoot()
    {
        GameObject inspectorUi = FindSceneGameObject("InspectorUI");
        if (inspectorUi != null)
        {
            inspectorUi.SetActive(true);
            return inspectorUi.transform;
        }

        GameObject canvasObject = new GameObject("InspectorUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        return canvasObject.transform;
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
        {
            return activeObject;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.name != objectName || !candidate.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool HasAuthoredInspectorHud(Transform uiRoot)
    {
        return uiRoot != null &&
               (uiRoot.Find("OverviewPanel") != null ||
                uiRoot.Find("StartMenu") != null ||
                uiRoot.Find("PlayerStatus") != null);
    }

    private void BindCombatLogPanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("CombatLog");
        if (panel == null)
        {
            return;
        }

        TMP_Text titleText = FindText(panel, "Label");
        TMP_Text logText = FindText(panel, "Viewport/Content/Text");
        Transform content = panel.Find("Viewport/Content");
        RectTransform contentRect = content != null ? content.GetComponent<RectTransform>() : null;
        ScrollRect scrollRect = panel.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = panel.gameObject.AddComponent<ScrollRect>();
        }

        Transform viewport = panel.Find("Viewport");
        scrollRect.viewport = viewport != null ? viewport.GetComponent<RectTransform>() : null;
        EnsureCombatLogPresenter();
        combatLogPresenter.Configure(titleText, logText, scrollRect, contentRect);
    }

    private void BindGateHint(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("GateHint");
        gateHintText = FindText(panel, "Text");
    }

    private void BindModulePanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("ModulePanel");
        modulePanelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        BindModuleSlots();
    }

    private void BindStartMenu(Transform uiRoot)
    {
        Transform root = uiRoot.Find("StartMenu");
        startMenuObject = root != null ? root.gameObject : null;
        Transform panel = root != null ? root.Find("Panel") : null;
        Transform main = panel != null ? panel.Find("MainMenuPanel") : null;
        Transform hangar = panel != null ? panel.Find("HangarPanel") : null;
        Transform settings = panel != null ? panel.Find("SettingsPanel") : null;

        mainMenuPanelObject = main != null ? main.gameObject : null;
        hangarPanelObject = hangar != null ? hangar.gameObject : null;
        settingsPanelObject = settings != null ? settings.gameObject : null;

        mainMenuTitleText = FindText(main, "Title");
        mainMenuSubtitleText = FindText(main, "Subtitle");
        continueButtonView = BindMenuButton(main != null ? main.Find("main_continue") : null, "main_continue");
        newGameButtonView = BindMenuButton(main != null ? main.Find("main_new_game") : null, "main_new_game");
        settingsMenuButtonView = BindMenuButton(main != null ? main.Find("main_settings") : null, "main_settings");
        exitButtonView = BindMenuButton(main != null ? main.Find("main_exit") : null, "main_exit");

        hangarTitleText = FindText(hangar, "Title");
        hangarSubtitleText = FindText(hangar, "Subtitle");
        BindShipCards(hangar);
        Transform infoPanel = hangar != null ? hangar.Find("InfoPanel") : null;
        startMenuPreviewImage = FindImage(infoPanel, "Preview");
        startMenuShipNameText = FindText(infoPanel, "ShipName");
        startMenuRoleText = FindText(infoPanel, "Role");
        startMenuDescriptionText = FindText(infoPanel, "Description");
        startMenuStatsText = FindText(infoPanel, "Stats");
        startMenuHintText = FindText(hangar, "Hint");
        startButtonImage = FindImage(hangar, "StartButton");
        startButtonRect = startButtonImage != null ? startButtonImage.rectTransform : null;
        startButtonText = FindText(hangar, "StartButton/Label");
        EnsureButton(startButtonImage != null ? startButtonImage.transform : null);
        hangarBackButtonView = BindMenuButton(hangar != null ? hangar.Find("hangar_back") : null, "hangar_back");

        settingsTitleText = FindText(settings, "Title");
        settingsSubtitleText = FindText(settings, "Subtitle");
        Transform settingsBox = settings != null ? settings.Find("SettingsBox") : null;
        settingsLanguageLabelText = FindText(settingsBox, "LanguageLabel");
        languageRuButtonView = BindMenuButton(settingsBox != null ? settingsBox.Find("lang_ru") : null, "lang_ru");
        languageEngButtonView = BindMenuButton(settingsBox != null ? settingsBox.Find("lang_eng") : null, "lang_eng");
        settingsFpsLabelText = FindText(settingsBox, "FpsLabel");
        for (int i = 0; i < fpsOptions.Length; i++)
        {
            fpsButtonViews[i] = BindMenuButton(settingsBox != null ? settingsBox.Find("fps_" + fpsOptions[i]) : null, "fps_" + fpsOptions[i]);
        }
        settingsBackButtonView = BindMenuButton(settings != null ? settings.Find("settings_back") : null, "settings_back");
        SetStartMenuPage(StartMenuPage.Main);
    }

    private void BindShipCards(Transform hangar)
    {
        shipCardViews.Clear();
        Transform cardsRoot = hangar != null ? hangar.Find("Cards") : null;
        int shipCount = availableShips != null ? availableShips.Count : 0;
        for (int i = 0; i < shipCount; i++)
        {
            Transform card = cardsRoot != null ? cardsRoot.Find("ShipCard_" + i) : null;
            if (card == null)
            {
                continue;
            }

            EnsureButton(card);
            shipCardViews.Add(new ShipCardView
            {
                Rect = card.GetComponent<RectTransform>(),
                Background = card.GetComponent<Image>(),
                Title = FindText(card, "Title"),
                Stats = FindText(card, "Stats")
            });
        }
    }

    private void BindVirtualJoystick(Transform uiRoot)
    {
        Transform root = uiRoot.Find("VirtualJoystick");
        joystickRootObject = root != null ? root.gameObject : null;
        Transform baseTransform = root != null ? root.Find("Base") : null;
        joystickAreaRect = baseTransform != null ? baseTransform.GetComponent<RectTransform>() : null;
        joystickBaseImage = baseTransform != null ? baseTransform.GetComponent<Image>() : null;
        joystickKnobImage = FindImage(baseTransform, "Knob");
        joystickHintText = FindText(baseTransform, "Hint");
    }

    private UiButtonView BindMenuButton(Transform buttonTransform, string id)
    {
        if (buttonTransform == null)
        {
            return null;
        }

        EnsureButton(buttonTransform);
        EnsureButtonScaleAnimator(buttonTransform.gameObject);
        RectTransform buttonRect = buttonTransform.GetComponent<RectTransform>();
        NormalizeAuthoredRect(buttonRect);
        return new UiButtonView
        {
            Id = id,
            Rect = buttonRect,
            Background = buttonTransform.GetComponent<Image>(),
            Label = FindText(buttonTransform, "Label")
        };
    }

    private static void EnsureButton(Transform target)
    {
        if (target != null && target.GetComponent<Button>() == null)
        {
            target.gameObject.AddComponent<Button>();
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

        TMP_Text titleText = CreateText("Label", panel.transform, "COMBAT LOG", 16, FontStyle.Bold, new Color(0.52f, 0.8f, 1f));
        SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -8f), new Vector2(-10f, -30f));

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panel.transform, false);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;
        Mask viewportMask = viewportObject.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetAnchoredRect(viewportRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -36f));

        RectTransform contentRect = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        contentRect.SetParent(viewportRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 160f);

        TMP_Text logText = CreateText("Text", contentRect, string.Empty, 13, FontStyle.Normal, new Color(0.74f, 0.86f, 1f));
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Overflow;
        SetAnchoredRect(logText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -160f));

        ScrollRect scrollRect = panel.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        EnsureCombatLogPresenter();
        combatLogPresenter.Configure(titleText, logText, scrollRect, contentRect);
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
            slot.gameObject.AddComponent<Button>();

            TMP_Text key = CreateText("Key", slot.transform, string.Empty, 14, FontStyle.Bold, Color.white);
            SetAnchoredRect(key.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(0f, -28f));

            TMP_Text label = CreateText("Label", slot.transform, string.Empty, 11, FontStyle.Bold, new Color(0.84f, 0.92f, 1f));
            label.alignment = TextAlignmentOptions.Center;
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
        joystickHintText.alignment = TextAlignmentOptions.Center;
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
        statusText.gameObject.SetActive(false);
    }

    private void EnsureEncounterChoiceUi()
    {
        if (encounterChoicePresenter != null && encounterChoicePresenter.HasPanel)
        {
            return;
        }

        if (hudCanvas == null)
        {
            return;
        }

        EnsureEncounterChoicePresenter();
        GameObject panelObject = new GameObject("EncounterChoicePanel", typeof(RectTransform));
        panelObject.transform.SetParent(hudCanvas.transform, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image dim = CreateImage("Dimmer", panelObject.transform, new Color(0f, 0f, 0f, 0.58f));
        StretchToParent(dim.rectTransform);

        Image panel = CreateImage("Panel", panelObject.transform, new Color(0.04f, 0.08f, 0.12f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 360f);
        AddOutline(panel.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        TMP_Text titleText = CreateText("Title", panel.transform, "Выберите следующую локацию", 28, FontStyle.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -24f), new Vector2(-24f, -66f));

        TMP_Text bodyText = CreateText("Body", panel.transform, string.Empty, 16, FontStyle.Normal, new Color(0.74f, 0.86f, 0.96f));
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        SetAnchoredRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -76f), new Vector2(-34f, -124f));

        List<Button> buttons = new List<Button>();
        List<TMP_Text> labels = new List<TMP_Text>();
        for (int i = 0; i < 3; i++)
        {
            UiButtonView buttonView = CreateMenuButton(
                panel.transform,
                "encounter_choice_" + (i + 1),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 48f - i * 72f),
                new Vector2(450f, 58f));

            if (buttonView.Label != null)
            {
                buttonView.Label.fontSize = 17f;
                buttonView.Label.textWrappingMode = TextWrappingModes.Normal;
            }

            buttons.Add(buttonView.Rect.GetComponent<Button>());
            labels.Add(buttonView.Label);
        }

        encounterChoicePresenter.Configure(panelObject, titleText, bodyText, buttons, labels);
    }

    private void EnsureNonCombatUi()
    {
        if (nonCombatEncounterPresenter != null && nonCombatEncounterPresenter.HasPanel)
        {
            return;
        }

        if (hudCanvas == null)
        {
            return;
        }

        EnsureNonCombatEncounterPresenter();
        GameObject panelObject = new GameObject("NonCombatEncounterPanel", typeof(RectTransform));
        panelObject.transform.SetParent(hudCanvas.transform, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image dim = CreateImage("Dimmer", panelObject.transform, new Color(0f, 0f, 0f, 0.58f));
        StretchToParent(dim.rectTransform);

        Image panel = CreateImage("Panel", panelObject.transform, new Color(0.04f, 0.08f, 0.12f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 320f);
        AddOutline(panel.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        TMP_Text titleText = CreateText("Title", panel.transform, "Локация", 28, FontStyle.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -24f), new Vector2(-24f, -66f));

        TMP_Text bodyText = CreateText("Body", panel.transform, string.Empty, 17, FontStyle.Normal, new Color(0.82f, 0.92f, 1f));
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        SetAnchoredRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(38f, -84f), new Vector2(-38f, -214f));

        UiButtonView actionButtonView = CreateMenuButton(
            panel.transform,
            "non_combat_action",
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -112f),
            new Vector2(250f, 56f));
        actionButtonView.Label.text = "Продолжить";

        nonCombatEncounterPresenter.Configure(
            panelObject,
            titleText,
            bodyText,
            actionButtonView.Rect.GetComponent<Button>(),
            actionButtonView.Label);
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
        mainMenuTitleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(mainMenuTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -80f), new Vector2(-40f, -130f));

        mainMenuSubtitleText = CreateText("Subtitle", parent, string.Empty, 20, FontStyle.Normal, new Color(0.62f, 0.82f, 0.98f));
        mainMenuSubtitleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(mainMenuSubtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -132f), new Vector2(-60f, -170f));

        continueButtonView = CreateMenuButton(parent, "main_continue", new Vector2(0.5f, 0.5f), new Vector2(0f, 146f), new Vector2(280f, 56f));
        newGameButtonView = CreateMenuButton(parent, "main_new_game", new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(280f, 56f));
        settingsMenuButtonView = CreateMenuButton(parent, "main_settings", new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(280f, 56f));
        exitButtonView = CreateMenuButton(parent, "main_exit", new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(280f, 56f));
    }

    private void CreateHangarPanel(Transform parent)
    {
        hangarTitleText = CreateText("Title", parent, string.Empty, 34, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        hangarTitleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(hangarTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -24f), new Vector2(-26f, -64f));

        hangarSubtitleText = CreateText("Subtitle", parent, string.Empty, 18, FontStyle.Normal, new Color(0.58f, 0.8f, 0.96f));
        hangarSubtitleText.alignment = TextAlignmentOptions.Center;
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
        startMenuDescriptionText.alignment = TextAlignmentOptions.TopLeft;
        startMenuDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        startMenuDescriptionText.overflowMode = TextOverflowModes.Overflow;
        SetAnchoredRect(startMenuDescriptionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -96f), new Vector2(-26f, -156f));

        startMenuStatsText = CreateText("Stats", infoPanel.transform, "-", 15, FontStyle.Normal, new Color(0.92f, 0.95f, 1f));
        startMenuStatsText.alignment = TextAlignmentOptions.TopLeft;
        SetAnchoredRect(startMenuStatsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(200f, 24f), new Vector2(-26f, 84f));

        startMenuHintText = CreateText("Hint", parent, string.Empty, 16, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        startMenuHintText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(startMenuHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 74f), new Vector2(-26f, 102f));

        startButtonImage = CreateImage("StartButton", parent, new Color(0.12f, 0.3f, 0.42f, 1f));
        startButtonRect = startButtonImage.rectTransform;
        startButtonRect.anchorMin = new Vector2(0.5f, 0f);
        startButtonRect.anchorMax = new Vector2(0.5f, 0f);
        startButtonRect.pivot = new Vector2(0.5f, 0f);
        startButtonRect.sizeDelta = new Vector2(260f, 54f);
        startButtonRect.anchoredPosition = new Vector2(130f, 18f);
        AddOutline(startButtonImage.gameObject, new Color(0.52f, 0.82f, 1f, 1f));
        startButtonImage.gameObject.AddComponent<Button>();

        startButtonText = CreateText("Label", startButtonImage.transform, string.Empty, 20, FontStyle.Bold, Color.white);
        startButtonText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(startButtonText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));

        hangarBackButtonView = CreateMenuButton(parent, "hangar_back", new Vector2(0.5f, 0f), new Vector2(-130f, 18f), new Vector2(220f, 54f));
    }

    private void CreateSettingsPanel(Transform parent)
    {
        settingsTitleText = CreateText("Title", parent, string.Empty, 34, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        settingsTitleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(settingsTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -36f), new Vector2(-26f, -76f));

        settingsSubtitleText = CreateText("Subtitle", parent, string.Empty, 18, FontStyle.Normal, new Color(0.58f, 0.8f, 0.96f));
        settingsSubtitleText.alignment = TextAlignmentOptions.Center;
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
        buttonImage.gameObject.AddComponent<Button>();
        RectTransform rect = buttonImage.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        AddOutline(buttonImage.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));
        EnsureButtonScaleAnimator(buttonImage.gameObject);

        TMP_Text label = CreateText("Label", buttonImage.transform, id, 20, FontStyle.Bold, Color.white);
        label.alignment = TextAlignmentOptions.Center;
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
        if (continueButtonView != null)
        {
            continueButtonView.Label.text = Localize("menu_continue");
            continueButtonView.Rect.gameObject.SetActive(gameStarted && !gameOver);
        }
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
        gameOverPresenter?.RefreshLocalizedTexts();
        pauseMenuPresenter?.RefreshLocalizedTexts();
        confirmationDialogPresenter?.RefreshLocalizedTexts();
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

    private void HandlePausedInput()
    {
        if (HandleConfirmationInput())
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ResumeRun();
            return;
        }

        if (startMenuObject != null && startMenuObject.activeSelf)
        {
            HandleStartMenuInput();
            return;
        }

        Vector2 pointerPosition;
        if (!TryGetPrimaryPointerDown(out pointerPosition))
        {
            return;
        }

        pauseMenuPresenter?.HandlePointerDown(pointerPosition);
    }

    private void HandleGameOverInput()
    {
        if (HandleConfirmationInput())
        {
            return;
        }

        Vector2 pointerPosition;
        if (!TryGetPrimaryPointerDown(out pointerPosition))
        {
            return;
        }

        gameOverPresenter?.TickInput(pointerPosition);
    }

    private void ReturnToMainMenu()
    {
        if (startDirectlyFromExternalMenu && !string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            return;
        }

        gameStarted = false;
        gameOver = false;
        gamePaused = false;
        levelUpPending = false;
        activePerks.Clear();
        ClearEnemies();
        ClearProjectiles();
        ShowPauseMenu(false);
        ShowGameOverPanel(false);
        ShowStartMenu(true);
        SetStartMenuPage(StartMenuPage.Main);
        UpdateHud();
    }

    private void ShowGameOverPanel(bool show)
    {
        if (show)
        {
            gameOverPresenter?.Show();
        }
        else
        {
            gameOverPresenter?.Hide();
        }
    }

    private void ShowPauseMenu(bool show)
    {
        if (show)
        {
            pauseMenuPresenter?.Show();
        }
        else
        {
            pauseMenuPresenter?.Hide();
        }

        if (!show)
        {
            pauseSettingsOpened = false;
        }
    }

    private void OpenPauseSettings()
    {
        ShowPauseMenu(false);
        pauseSettingsOpened = true;
        ShowStartMenu(true);
        SetStartMenuPage(StartMenuPage.Settings);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RequestConfirmation(ConfirmAction action, string bodyText)
    {
        EnsureConfirmationDialogPresenter();
        if (hudCanvas != null)
        {
            confirmationDialogPresenter.Build(hudCanvas.transform, uiFactory, uiFont, squareSprite);
        }

        pendingConfirmAction = action;
        confirmationDialogPresenter?.Show(Localize("confirm_title"), bodyText, ExecutePendingConfirmation, CancelPendingConfirmation);
    }

    private bool HandleConfirmationInput()
    {
        if (pendingConfirmAction == ConfirmAction.None || confirmationDialogPresenter == null || !confirmationDialogPresenter.IsVisible)
        {
            return false;
        }

        Vector2 pointerPosition;
        if (!TryGetPrimaryPointerDown(out pointerPosition))
        {
            return true;
        }

        return confirmationDialogPresenter.HandlePointerDown(pointerPosition);
    }

    private void CancelPendingConfirmation()
    {
        pendingConfirmAction = ConfirmAction.None;
    }

    private void ExecutePendingConfirmation()
    {
        ConfirmAction action = pendingConfirmAction;
        pendingConfirmAction = ConfirmAction.None;

        if (action == ConfirmAction.ReturnToMainMenu)
        {
            ReturnToMainMenu();
        }
        else if (action == ConfirmAction.ExitGame)
        {
            ExitGame();
        }
    }

    private static void EnsureButtonScaleAnimator(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return;
        }

        if (buttonObject.GetComponent<UIButtonScaleAnimator>() == null)
        {
            buttonObject.AddComponent<UIButtonScaleAnimator>();
        }
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
        background.gameObject.AddComponent<Button>();

        TMP_Text title = CreateText("Title", background.transform, "-", 22, FontStyle.Bold, Color.white);
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -18f), new Vector2(-14f, -54f));

        TMP_Text stats = CreateText("Stats", background.transform, "-", 15, FontStyle.Normal, new Color(0.8f, 0.9f, 1f));
        stats.alignment = TextAlignmentOptions.TopLeft;
        stats.textWrappingMode = TextWrappingModes.Normal;
        stats.overflowMode = TextOverflowModes.Overflow;
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
            TMP_Text slotKey = slotTransform.Find("Key") != null ? slotTransform.Find("Key").GetComponent<TMP_Text>() : null;
            TMP_Text slotTitle = slotTransform.Find("Label") != null ? slotTransform.Find("Label").GetComponent<TMP_Text>() : null;

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

    private void HandleInput(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        UpdateVirtualJoystick();
        if (combatCameraController != null)
        {
            combatCameraController.Tick(deltaTime);
            mainCamera = combatCameraController.CurrentCamera;
        }

        Vector2 moveInput = GetMovementVector(keyboard);
        if (moveInput.sqrMagnitude > 0.01f)
        {
            player.MoveCommandActive = false;
        }

        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition))
        {
            if (pauseMenuPresenter != null && pauseMenuPresenter.IsHudButtonClicked(pointerPosition))
            {
                SetPaused(true);
                return;
            }

            if (TryToggleModuleFromHud(pointerPosition))
            {
                return;
            }

            if (TrySelectEnemyFromOverview(pointerPosition))
            {
                return;
            }

            if (!IsGameplayHudBlocked(pointerPosition) && TrySelectEnemyFromWorld(pointerPosition))
            {
                suppressPointerMovementUntilRelease = true;
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
        if (suppressPointerMovementUntilRelease && (!pointerState.HasPointer || !pointerState.PrimaryPressed))
        {
            suppressPointerMovementUntilRelease = false;
        }

        bool pointerBlocked = pointerState.HasPointer &&
                              (IsGameplayHudBlocked(pointerState.ScreenPosition) || suppressPointerMovementUntilRelease);
        Vector3 pointerWorldPosition = pointerState.HasPointer
            ? ScreenToWorldPosition(pointerState.ScreenPosition)
            : player.Transform.position;

        MovementUpdateContext movementContext = new MovementUpdateContext(
            moveInput,
            pointerBlocked,
            pointerState,
            pointerWorldPosition);
        movementService.UpdateMovement(player, movementContext, deltaTime);
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

    private bool TryToggleModuleFromHud(Vector2 screenPosition)
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
        return combatHudPresenter != null && combatHudPresenter.TrySelectEnemyFromOverview(screenPosition, targetingController);
    }

    private bool TrySelectEnemyFromWorld(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        return targetingController != null && targetingController.TrySelectFromWorld(worldPosition);
    }

    private bool IsGameplayHudBlocked(Vector2 screenPosition)
    {
        if (combatHudPresenter != null && combatHudPresenter.IsOverviewBlocked(screenPosition))
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

    private static SpriteRenderer FindChildSpriteRendererContaining(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token))
        {
            return null;
        }

        string lowerToken = token.ToLowerInvariant();
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (renderer.name.ToLowerInvariant().Contains(lowerToken))
            {
                return renderer;
            }
        }

        return null;
    }

    private ShipShieldVisual EnsureShieldVisual(GameObject owner, SpriteRenderer renderer, Color baseColor, float pulseOffset)
    {
        if (owner == null || renderer == null)
        {
            return null;
        }

        ShipShieldVisual shieldVisual = owner.GetComponentInChildren<ShipShieldVisual>(true);
        if (shieldVisual == null)
        {
            shieldVisual = owner.AddComponent<ShipShieldVisual>();
        }

        shieldVisual.Initialize(renderer, shieldHitMaterial, ringSprite, baseColor, pulseOffset);
        return shieldVisual;
    }

    private void UpdatePlayer(float deltaTime)
    {
        player.UpdateCapacitor(deltaTime);
        player.HitFlashTimer = Mathf.Max(0f, player.HitFlashTimer - deltaTime * 3.6f);
        player.ThrusterPulse += deltaTime * (2.4f + player.Velocity.magnitude * 0.4f);
        if (!player.IsAlive())
        {
            gameOver = true;
            ShowGameOverPanel(true);
            LogMessage(Localize("log_hull_breach"), "critical");
            statusText.text = Localize("status_gameover");
        }

    }

    private void UpdateCombat(float deltaTime)
    {
        Vector3 targetPoint = Vector3.zero;
        bool hasPlayerTarget = targetingController != null && targetingController.TryGetPlayerTargetPosition(out targetPoint);
        if (combatAudioController != null)
        {
            combatAudioController.SetPlayerTransform(player != null ? player.Transform : null);
        }
        Action<WeaponDataSO, Vector3, CombatFaction> playWeaponShot = combatAudioController != null
            ? combatAudioController.PlayWeaponShot
            : null;

        CombatUpdateContext context = new CombatUpdateContext
        {
            Player = player,
            Enemies = enemies,
            Modules = modules,
            EquipmentState = CurrentEquipmentState,
            TargetEnemy = targetingController != null ? targetingController.TargetEnemy : null,
            HasPlayerTarget = hasPlayerTarget,
            PlayerTargetPosition = targetPoint,
            ProjectileRoot = projectileRoot,
            PoolService = poolService,
            Wave = wave,
            Localize = Localize,
            LogMessage = LogMessage,
            UpdateModuleVisual = UpdateModuleVisual,
            PlayWeaponShot = playWeaponShot
        };

        CombatUpdateResult result = combatService.UpdateFrame(context, deltaTime);
        if (targetingController != null)
        {
            targetingController.SetTargetEnemy(result.TargetEnemy);
        }

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

        perkSelectionPresenter?.Show(activePerks);
        LogMessage(Localize("log_levelup"), "warning");
    }

    private void UpdatePerkSelectionInput()
    {
        perkSelectionPresenter?.TickInput();
    }

    private void ApplyPerk(int index)
    {
        if (index < 0 || index >= activePerks.Count)
        {
            return;
        }

        activePerks[index].Apply?.Invoke();
        levelUpPending = false;
        perkSelectionPresenter?.Hide();
        LogMessage(Localize("log_perk_selected") + activePerks[index].Label, "warning");
        activePerks.Clear();
    }

    private void UpdateTimelineSpawner(float deltaTime)
    {
        WaveTimelineSO activeTimeline = GetActiveTimeline();
        if (timelineSpawnController == null)
        {
            return;
        }

        timelineSpawnController.SetCamera(mainCamera != null ? mainCamera : Camera.main);
        timelineSpawnController.Tick(
            deltaTime,
            activeTimeline,
            player != null && player.Transform != null ? player.Transform.position : Vector3.zero);
        wave = timelineSpawnController.CurrentWave;

        if (activeTimeline == null || activeTimeline.events == null || activeTimeline.events.Count == 0)
        {
            if (gateHintText != null)
            {
                gateHintText.transform.parent.gameObject.SetActive(true);
                gateHintText.text = Localize("timeline_missing");
            }
            return;
        }

        if (timelineSpawnController.SpawnedThisFrame > 0)
        {
            targetingController?.UpdateTargetState();
            LogMessage(Localize("log_hostiles") + enemies.Count);
        }

        if (gateHintText != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(true);
            float nextEventTime = timelineSpawnController.NextEventTime;
            if (nextEventTime < 0f)
            {
                gateHintText.text = Localize("timeline_complete");
            }
            else
            {
                float timeLeft = Mathf.Max(0f, nextEventTime - timelineSpawnController.GameTimer);
                gateHintText.text = Localize("timeline_next_event") + timeLeft.ToString("0.0") + Localize("seconds_short");
            }
        }
    }

    private void UpdateEffects(float deltaTime)
    {
    }

    private void UpdateVisuals()
    {
        if (player != null && player.ShieldVisual != null)
        {
            player.ShieldVisual.SetShieldState(player.ShieldPercent, player.HitFlashTimer);
        }
        else if (player != null && player.AuraRenderer != null)
        {
            ApplyShieldVisual(player.AuraRenderer, player.BaseAuraColor, player.ShieldPercent, player.HitFlashTimer, 0f);
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
        }
        if (player != null && player.ThrusterEffect != null)
        {
            float thrustAmount = Mathf.Clamp01(player.Velocity.magnitude / Mathf.Max(0.1f, player.Speed * player.SpeedMultiplier));
            player.ThrusterEffect.SetIntensity(thrustAmount);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy.ShieldVisual != null)
            {
                float shieldHitFlash = Mathf.Max(enemy.HitFlashTimer, enemy.AttackFlashTimer * 0.35f);
                enemy.ShieldVisual.SetShieldState(enemy.ShieldPercent, shieldHitFlash);
            }
            else if (enemy.ShieldRenderer != null)
            {
                float shieldHitFlash = Mathf.Max(enemy.HitFlashTimer, enemy.AttackFlashTimer * 0.35f);
                ApplyShieldVisual(enemy.ShieldRenderer, enemy.BaseShieldColor, enemy.ShieldPercent, shieldHitFlash, i * 0.47f);
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
            if (enemy.ThrusterEffect != null)
            {
                enemy.ThrusterEffect.SetIntensity(0.55f + enemy.AttackFlashTimer * 0.35f);
            }

        }

        targetingController?.TickVisuals();
        moveCommandVisualController?.Tick();
    }

    private void ApplyShieldVisual(SpriteRenderer renderer, Color baseColor, float shieldPercent, float hitFlash, float pulseOffset)
    {
        if (renderer == null)
        {
            return;
        }

        float clampedPercent = Mathf.Clamp01(shieldPercent);
        float hit = Mathf.Clamp01(hitFlash);
        float pulse = 1f + Mathf.Sin((Time.time + pulseOffset) * shieldPulseSpeed) * shieldPulseAlpha;
        float alpha = baseColor.a * clampedPercent * pulse * (1f + hit * shieldHitAlphaBoost);

        Color shieldColor = Color.Lerp(baseColor, shieldHitTint, hit * shieldHitTintStrength);
        shieldColor.a = Mathf.Clamp01(alpha);
        renderer.color = shieldColor;
        renderer.enabled = shieldColor.a > 0.001f;
    }

    private void UpdateHud()
    {
        if (combatLogPresenter != null)
        {
            combatLogPresenter.Refresh();
        }
        pauseMenuPresenter?.SetHudButtonVisible(gameStarted && !gameOver && !gamePaused && !levelUpPending);

        string shipName = (availableShips != null && availableShips.Count > 0 && selectedShipIndex >= 0 && selectedShipIndex < availableShips.Count)
            ? availableShips[selectedShipIndex].displayName
            : "-";
        combatHudPresenter?.Tick(new CombatHudContext
        {
            Player = player,
            Enemies = enemies,
            Modules = modules,
            EquipmentState = CurrentEquipmentState,
            TargetingController = targetingController,
            CurrentWave = wave,
            GameStarted = gameStarted,
            GameOver = gameOver,
            LevelUpPending = levelUpPending,
            UseVirtualJoystick = useVirtualJoystick,
            ShipName = shipName
        });

        if (statusText != null && (combatHudPresenter == null || combatHudPresenter.StatusText == null))
        {
            statusText.text = !gameStarted
                ? Localize("status_menu")
                : gameOver
                    ? Localize("status_gameover")
                    : levelUpPending
                        ? Localize("status_levelup")
                        : useVirtualJoystick ? Localize("status_play_mobile") : Localize("status_play_desktop");
        }

        if (!gameStarted && gateHintText != null && gateHintText.transform.parent != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }
    }

    private void UpdateModuleVisual(ModuleState module)
    {
        combatHudPresenter?.UpdateModuleVisual(module);
    }

    private void LogMessage(string message, string kind = "info")
    {
        if (combatLogPresenter != null)
        {
            combatLogPresenter.LogMessage(message, kind);
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

    private TMP_Text CreateText(string objectName, Transform parent, string content, int fontSize, FontStyle fontStyle, Color color)
    {
        return uiFactory.CreateText(objectName, parent, uiFont, content, fontSize, fontStyle, color);
    }

    private void AddOutline(GameObject target, Color color)
    {
        uiFactory.AddOutline(target, color);
    }

    private void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        uiFactory.SetAnchoredRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
    }

}
