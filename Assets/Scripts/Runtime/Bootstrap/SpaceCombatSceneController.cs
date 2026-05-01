using System;
using System.Collections.Generic;
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
    [Tooltip("Компонент, владеющий runtime-модулями игрока: создание, биндинг HUD-слотов и переключение клавишами/кликом.")]
    [SerializeField] private PlayerModuleController playerModuleController;
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
    [Tooltip("Компонент прогресса игрока: опыт, повышение уровня, выбор улучшений и блокировка gameplay во время выбора.")]
    [SerializeField] private PlayerProgressionController playerProgressionController;
    [Tooltip("Presenter стартового меню, ангара и настроек. Отвечает за UI, страницы и обработку нажатий меню.")]
    [SerializeField] private StartMenuPresenter startMenuPresenter;
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

    private enum LanguageOption
    {
        RU,
        ENG
    }

    private readonly List<EnemyShip> enemies = new List<EnemyShip>();
    private readonly ShipEquipmentState equipmentState = new ShipEquipmentState();
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
    private TMP_Text joystickHintText;
    private GameObject joystickRootObject;
    private RectTransform modulePanelRect;
    private RectTransform joystickAreaRect;
    private Image joystickKnobImage;

    private Font uiFont;
    private Sprite squareSprite;
    private Sprite circleSprite;
    private Sprite ringSprite;

    private int wave = 1;
    private bool gameOver;
    private bool gameStarted;
    private bool gamePaused;
    private bool pauseSettingsOpened;
    private float encounterStartHullPercent = 1f;
    private int selectedShipIndex;
    private int selectedFpsIndex = 2;
    private LanguageOption currentLanguage = LanguageOption.RU;
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

    private void EnsurePlayerModuleController()
    {
        if (playerModuleController == null)
        {
            playerModuleController = GetComponent<PlayerModuleController>();
        }

        if (playerModuleController == null)
        {
            playerModuleController = FindAnyObjectByType<PlayerModuleController>(FindObjectsInactive.Include);
        }

        if (playerModuleController == null)
        {
            playerModuleController = gameObject.AddComponent<PlayerModuleController>();
        }

        playerModuleController.Initialize(Localize, LogMessage, UpdateModuleVisual, RefreshEquipmentUi);
        playerModuleController.SetPlayer(player);
        if (modulePanelRect != null)
        {
            playerModuleController.BindModuleSlots(modulePanelRect);
        }
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
    }

    private void EnsurePlayerProgressionController()
    {
        if (playerProgressionController == null)
        {
            playerProgressionController = GetComponent<PlayerProgressionController>();
        }

        if (playerProgressionController == null)
        {
            playerProgressionController = FindAnyObjectByType<PlayerProgressionController>(FindObjectsInactive.Include);
        }

        if (playerProgressionController == null)
        {
            playerProgressionController = gameObject.AddComponent<PlayerProgressionController>();
        }

        playerProgressionController.Initialize(perkSelectionPresenter, Localize, LogMessage);
        playerProgressionController.SetPlayer(player);
    }

    private void EnsureStartMenuPresenter()
    {
        if (startMenuPresenter == null)
        {
            startMenuPresenter = GetComponent<StartMenuPresenter>();
        }

        if (startMenuPresenter == null)
        {
            startMenuPresenter = FindAnyObjectByType<StartMenuPresenter>(FindObjectsInactive.Include);
        }

        if (startMenuPresenter == null)
        {
            startMenuPresenter = gameObject.AddComponent<StartMenuPresenter>();
        }

        startMenuPresenter.Initialize(uiFactory, uiFont, squareSprite, Localize, GetShipRoleText, GetShipDescriptionText, fpsOptions);
        startMenuPresenter.OnContinueRequested = ResumeRun;
        startMenuPresenter.OnNewGameRequested = () => startMenuPresenter.SetPage(StartMenuPage.Hangar);
        startMenuPresenter.OnSettingsRequested = () => startMenuPresenter.SetPage(StartMenuPage.Settings);
        startMenuPresenter.OnExitRequested = () => RequestConfirmation(ConfirmAction.ExitGame, Localize("confirm_exit"));
        startMenuPresenter.OnShipSelected = SelectShip;
        startMenuPresenter.OnStartRunRequested = () => StartRun();
        startMenuPresenter.OnLanguageToggleRequested = SetLanguageByIndex;
        startMenuPresenter.OnFpsToggleRequested = SetFpsIndex;
        startMenuPresenter.OnBackRequested = HandleStartMenuBackRequested;
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

    [ContextMenu("Автонастроить ссылки сцены")]
    private void AutoWireSceneReferences()
    {
        combatCameraController = GetOrAddSceneComponent<CombatCameraController>();
        encounterFlowController = GetOrAddSceneComponent<EncounterFlowController>();
        timelineSpawnController = GetOrAddSceneComponent<TimelineSpawnController>();
        enemySpawner = GetOrAddSceneComponent<EnemySpawner>();
        targetingController = GetOrAddSceneComponent<TargetingController>();
        moveCommandVisualController = GetOrAddSceneComponent<MoveCommandVisualController>();
        minimapController = GetOrAddSceneComponent<MinimapController>();
        backgroundController = GetOrAddSceneComponent<BackgroundController>();
        playerShipController = GetOrAddSceneComponent<PlayerShipController>();
        playerModuleController = GetOrAddSceneComponent<PlayerModuleController>();
        combatHudPresenter = GetOrAddSceneComponent<CombatHudPresenter>();
        pauseMenuPresenter = GetOrAddSceneComponent<PauseMenuPresenter>();
        confirmationDialogPresenter = GetOrAddSceneComponent<ConfirmationDialogPresenter>();
        gameOverPresenter = GetOrAddSceneComponent<GameOverPresenter>();
        perkSelectionPresenter = GetOrAddSceneComponent<PerkSelectionPresenter>();
        playerProgressionController = GetOrAddSceneComponent<PlayerProgressionController>();
        startMenuPresenter = GetOrAddSceneComponent<StartMenuPresenter>();
        encounterChoicePresenter = GetOrAddSceneComponent<EncounterChoicePresenter>();
        nonCombatEncounterPresenter = GetOrAddSceneComponent<NonCombatEncounterPresenter>();
        combatAudioController = GetOrAddSceneComponent<CombatAudioController>();
        combatLogPresenter = GetOrAddSceneComponent<CombatLogPresenter>();

        Debug.Log("SpaceCombatSceneController: ссылки сцены автонастроены. Проверьте Inspector и сохраните сцену.", this);
    }

    private T GetOrAddSceneComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
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
        EnsurePlayerProgressionController();
        EnsureStartMenuPresenter();
        EnsureEncounterFlowController();
        EnsureTimelineSpawnController();
        ValidateSerializedReferences();
        useVirtualJoystick = platformService.ShouldUseVirtualJoystick();
        CreateSprites();
        CreateStarterShips();
        BuildWorld();
        EnsurePlayerShipController();
        EnsurePlayerModuleController();
        EnsurePlayerProgressionController();
        EnsureEnemySpawner();
        EnsureTargetingController();
        EnsureMoveCommandVisualController();
        EnsureMinimapController();
        SpawnPlayer();
        EnsurePlayerModuleController();
        EnsurePlayerProgressionController();
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

        if (IsLevelUpPending())
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
        UpdateVisuals();
        UpdateHud();
    }

    private void HandleStartMenuInput()
    {
        if (HandleConfirmationInput())
        {
            return;
        }

        startMenuPresenter?.TickInput();
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

    private void SetLanguageByIndex(int index)
    {
        SetLanguage(index == 0 ? LanguageOption.RU : LanguageOption.ENG);
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
        gameOverPresenter?.RefreshLocalizedTexts();
        pauseMenuPresenter?.RefreshLocalizedTexts();
        confirmationDialogPresenter?.RefreshLocalizedTexts();
        if (joystickHintText != null) joystickHintText.text = Localize("joystick_hint");
        RefreshStartMenuPresenter();
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
        playerModuleController?.SetPlayer(player);
        playerProgressionController?.SetPlayer(player);
    }

    private void SelectShip(int index)
    {
        if (availableShips == null || availableShips.Count == 0)
        {
            return;
        }

        selectedShipIndex = Mathf.Clamp(index, 0, availableShips.Count - 1);
        ApplyShipDefinition(availableShips[selectedShipIndex], false);
        RefreshStartMenuPresenter();
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
        EnsurePlayerModuleController();
        playerModuleController.SetPlayer(player);
        playerModuleController.CreateModules(ship.moduleSlotCount, ship);
        EnsurePlayerProgressionController();
        playerProgressionController.SetPlayer(player);
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
        RefreshEquipmentUi();
    }

    private void RefreshEquipmentUi()
    {
        if (equipmentUiController != null)
        {
            equipmentUiController.Refresh(CurrentEquipmentState);
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
        EnsurePlayerProgressionController();
        playerProgressionController?.ResetRunState();
        wave = 1;
        enemySpawnSequence = 0;
        targetingController?.ClearTarget();
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
        playerModuleController?.ResetModules();
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
        if (gameOver || IsLevelUpPending() || encounterFlowController == null)
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

    private void ShowStartMenu(bool show)
    {
        if (show)
        {
            startMenuPresenter?.Show();
        }
        else
        {
            startMenuPresenter?.Hide();
        }

        if (joystickRootObject != null)
        {
            joystickRootObject.SetActive(!show && useVirtualJoystick);
        }

        if (show)
        {
            RefreshStartMenuPresenter();
        }
    }

    private void RefreshStartMenuPresenter()
    {
        startMenuPresenter?.Refresh(
            availableShips,
            selectedShipIndex,
            gameStarted && !gameOver,
            currentLanguage == LanguageOption.RU,
            selectedFpsIndex,
            useVirtualJoystick);
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
        playerProgressionController?.AddExternalExperience(amount);
    }

    private EnemyShip CreateEnemy(string id, ShipDataSO shipData, Vector3 position, float levelScale)
    {
        EnsureEnemySpawner();
        return enemySpawner != null
            ? enemySpawner.SpawnEnemy(id, shipData, position, levelScale, enemies.Count)
            : null;
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
            EnsureStartMenuPresenter();
            startMenuPresenter.Bind(uiRoot, availableShips);
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
        EnsureStartMenuPresenter();
        startMenuPresenter.Build(uiRoot, availableShips);
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
        EnsurePlayerModuleController();
        playerModuleController.BindModuleSlots(modulePanelRect);
    }

    private void BindVirtualJoystick(Transform uiRoot)
    {
        Transform root = uiRoot.Find("VirtualJoystick");
        joystickRootObject = root != null ? root.gameObject : null;
        Transform baseTransform = root != null ? root.Find("Base") : null;
        joystickAreaRect = baseTransform != null ? baseTransform.GetComponent<RectTransform>() : null;
        joystickKnobImage = FindImage(baseTransform, "Knob");
        joystickHintText = FindText(baseTransform, "Hint");
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

        }

        EnsurePlayerModuleController();
        playerModuleController.BindModuleSlots(modulePanelRect);
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

        if (startMenuPresenter != null && startMenuPresenter.IsVisible)
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
        EnsurePlayerProgressionController();
        playerProgressionController?.ResetRunState();
        ClearEnemies();
        ClearProjectiles();
        ShowPauseMenu(false);
        ShowGameOverPanel(false);
        ShowStartMenu(true);
        startMenuPresenter?.SetPage(StartMenuPage.Main);
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
        startMenuPresenter?.SetPage(StartMenuPage.Settings);
    }

    private void HandleStartMenuBackRequested()
    {
        if (pauseSettingsOpened && gamePaused)
        {
            pauseSettingsOpened = false;
            ShowStartMenu(false);
            ShowPauseMenu(true);
        }
        else
        {
            startMenuPresenter?.SetPage(StartMenuPage.Main);
        }
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

            if (playerModuleController != null && playerModuleController.TryToggleModuleFromHud(pointerPosition))
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

        playerModuleController?.HandleHotkeys(keyboard);

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

        if (playerModuleController != null && playerModuleController.IsModulePanelBlocked(screenPosition))
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
            Modules = playerModuleController != null ? playerModuleController.Modules : null,
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
            playerProgressionController?.HandleLevelUpRequested();
        }
    }

    private void UpdatePerkSelectionInput()
    {
        playerProgressionController?.TickPerkSelectionInput();
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
        bool levelUpPending = IsLevelUpPending();
        pauseMenuPresenter?.SetHudButtonVisible(gameStarted && !gameOver && !gamePaused && !levelUpPending);

        string shipName = (availableShips != null && availableShips.Count > 0 && selectedShipIndex >= 0 && selectedShipIndex < availableShips.Count)
            ? availableShips[selectedShipIndex].displayName
            : "-";
        combatHudPresenter?.Tick(new CombatHudContext
        {
            Player = player,
            Enemies = enemies,
            Modules = playerModuleController != null ? playerModuleController.ReadOnlyModules : null,
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

    private bool IsLevelUpPending()
    {
        return playerProgressionController != null && playerProgressionController.LevelUpPending;
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
