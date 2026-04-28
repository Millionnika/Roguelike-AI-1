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
    [Header("UI References")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private EquipmentUIController equipmentUiController;
    [SerializeField] private SlotUI slotUiPrefab;
    [SerializeField] private Material shieldHitMaterial;

    [Header("Data")]
    [SerializeField] private List<ShipDataSO> availableShips = new List<ShipDataSO>();
    [SerializeField] public WaveTimelineSO currentTimeline;

    [Header("Background Layers")]
    [SerializeField] private List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();

    [Header("Timeline Spawner")]
    [SerializeField, Range(0.01f, 0.5f)] private float offscreenViewportMargin = 0.1f;
    [SerializeField, Min(1f)] private float timelinePhaseDuration = 30f;
    [SerializeField, Min(0f)] private float timelineDifficultyPerPhase = 0.14f;

    [Header("Targeting Visuals")]
    [SerializeField] private Sprite targetFrameSourceSprite;
    [SerializeField] private Color targetFrameColor = new Color(0.45f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color targetLineColor = new Color(1f, 1f, 1f, 0.58f);
    [SerializeField, Min(0f)] private float targetFramePadding = 0.35f;
    [SerializeField, Min(0f)] private float targetWorldClickPadding = 0.25f;
    [SerializeField, Min(0.01f)] private float targetLineWidth = 0.035f;
    [SerializeField] private int targetLineSortingOrder = 1;

    [Header("Shield Visuals")]
    [Tooltip("Амплитуда пульсации прозрачности щита (fallback, если ShipShieldVisual не назначен).")]
    [SerializeField, Range(0f, 0.6f)] private float shieldPulseAlpha = 0.12f;
    [Tooltip("Скорость пульсации щита (fallback, если ShipShieldVisual не назначен).")]
    [SerializeField, Min(0.1f)] private float shieldPulseSpeed = 3.2f;
    [Tooltip("Дополнительная яркость щита в момент попадания (fallback).")]
    [SerializeField, Range(0f, 2f)] private float shieldHitAlphaBoost = 0.55f;
    [Tooltip("Сила подкрашивания щита при попадании (fallback).")]
    [SerializeField, Range(0f, 1f)] private float shieldHitTintStrength = 0.65f;
    [Tooltip("Цвет подсветки щита при попадании (fallback).")]
    [SerializeField] private Color shieldHitTint = new Color(0.72f, 0.95f, 1f, 1f);

    [Header("Camera")]
    [SerializeField, Min(1f)] private float cameraDefaultOrthographicSize = 9f;
    [SerializeField, Min(1f)] private float cameraMinOrthographicSize = 5f;
    [SerializeField, Min(1f)] private float cameraMaxOrthographicSize = 16f;
    [SerializeField, Min(0.1f)] private float cameraZoomStep = 1.2f;
    [SerializeField, Min(0.1f)] private float cameraZoomSmoothing = 10f;
    [SerializeField, Min(0.1f)] private float cameraFollowSmoothing = 6f;
    [SerializeField, Range(0f, 1f)] private float cameraVelocityLookAhead = 0.15f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float shotBaseVolume = 0.85f;
    [SerializeField, Range(0f, 0.5f)] private float shotPitchRandomRange = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float shotVolumeRandomRange = 0.12f;
    [SerializeField, Min(1)] private int shotAudioVoices = 4;

    private sealed class SpawnEventRuntimeState
    {
        public bool oneShotExecuted;
        public float continuousAccumulator;
    }

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
    private readonly List<string> combatLog = new List<string>();
    private readonly List<EnemyRow> enemyRows = new List<EnemyRow>();
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
    private EnemyShip targetEnemy;
    private Transform worldRoot;
    private Transform starRoot;
    private Transform enemyRoot;
    private Transform projectileRoot;
    private Transform gateTransform;
    private Transform weaponSlotsRoot;
    private GameObject playerVisualInstance;
    private GameObject targetFrameObject;
    private SpriteRenderer targetFrameRenderer;
    private Sprite runtimeTargetFrameSprite;
    private LineRenderer targetLineRenderer;
    private Material targetingMaterial;

    private Canvas hudCanvas;
    private TMP_Text combatLogText;
    private ScrollRect combatLogScrollRect;
    private RectTransform combatLogContentRect;
    private TMP_Text gateHintText;
    private TMP_Text statusText;
    private TMP_Text overviewTitleText;
    private TMP_Text enemyHeaderText;
    private TMP_Text combatLogTitleText;
    private TMP_Text playerStatusTitleText;
    private TMP_Text targetNameText;
    private TMP_Text targetDistanceText;
    private TMP_Text targetDisplayText;
    private TMP_Text capacitorText;
    private TMP_Text levelText;
    private TMP_Text experienceText;
    private TMP_Text shipText;
    private TMP_Text perkTitleText;
    private TMP_Text perkHintText;
    private readonly TMP_Text[] perkOptionTexts = new TMP_Text[3];
    private readonly RectTransform[] perkOptionRects = new RectTransform[3];
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
    private GameObject perkPanelObject;
    private GameObject gameOverPanelObject;
    private GameObject pauseMenuObject;
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
    private UiButtonView retryButtonView;
    private UiButtonView gameOverMenuButtonView;
    private UiButtonView gameOverExitButtonView;
    private UiButtonView pauseHudButtonView;
    private UiButtonView pauseResumeButtonView;
    private UiButtonView pauseSettingsButtonView;
    private UiButtonView pauseMenuButtonView;
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
    private bool levelUpPending;
    private bool gameOver;
    private bool gameStarted;
    private bool gamePaused;
    private bool combatLogShouldSnapToBottom;
    private float gameTimer;
    private int selectedShipIndex;
    private int selectedFpsIndex = 2;
    private LanguageOption currentLanguage = LanguageOption.RU;
    private StartMenuPage startMenuPage = StartMenuPage.Main;
    private bool useVirtualJoystick;
    private bool joystickDragging;
    private Vector2 joystickVector;
    private bool suppressPointerMovementUntilRelease;
    private GameObject runtimeStarLayerPrefab;
    private GameObject runtimeNebulaLayerPrefab;
    private AudioSource[] shotAudioSources;
    private int nextShotAudioSourceIndex;
    private int enemySpawnSequence;
    private float targetCameraOrthographicSize;
    private readonly List<SpawnEventRuntimeState> spawnEventStates = new List<SpawnEventRuntimeState>();

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
        if (runtimeTargetFrameSprite != null)
        {
            Destroy(runtimeTargetFrameSprite);
        }
        if (targetingMaterial != null)
        {
            Destroy(targetingMaterial);
        }
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
        if (currentTimeline == null)
        {
            Debug.LogError("SpaceCombatSceneController: currentTimeline is not assigned.", this);
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
        cameraMinOrthographicSize = Mathf.Max(1f, cameraMinOrthographicSize);
        cameraMaxOrthographicSize = Mathf.Max(cameraMinOrthographicSize, cameraMaxOrthographicSize);
        cameraDefaultOrthographicSize = Mathf.Clamp(cameraDefaultOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        if (shieldHitMaterial == null)
        {
            shieldHitMaterial = Resources.Load<Material>("Materials/ShieldHit_SG");
        }
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
            Vector3 lookAhead = new Vector3(player.Velocity.x, player.Velocity.y, 0f) * cameraVelocityLookAhead;
            Vector3 targetPosition = new Vector3(current.x, current.y, -10f) + new Vector3(lookAhead.x, lookAhead.y, 0f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, cameraFollowSmoothing * Time.deltaTime);
        }
    }

    private void ConfigureCamera()
    {
        mainCamera.orthographic = true;
        targetCameraOrthographicSize = Mathf.Clamp(cameraDefaultOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        mainCamera.orthographicSize = targetCameraOrthographicSize;
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
        TeamMember playerTeam = playerObject.GetComponent<TeamMember>();
        if (playerTeam == null)
        {
            playerTeam = playerObject.AddComponent<TeamMember>();
        }
        playerTeam.SetFaction(CombatFaction.Player);

        player = new PlayerShip
        {
            Transform = playerObject.transform,
            TeamMember = playerTeam
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
        player.CapacitorRechargeRate = Mathf.Max(0.1f, ship.capacitorRechargeRate);
        player.Transform.position = Vector3.zero;
        player.Transform.rotation = Quaternion.identity;
        player.Velocity = Vector2.zero;
        player.MoveCommandActive = false;

        ApplyShipVisualFromPrefab(ship);

        ConfigureEquipment(ship);
        CreateModules(ship.moduleSlotCount, ship);
        ConfigurePlayerDamageReceiver();

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
            equipmentState.RuntimeWeapons[i] = configuredWeapon != null
                ? new WeaponInstance(
                    configuredWeapon,
                    player.Transform,
                    i < equipmentState.WeaponMuzzles.Count ? equipmentState.WeaponMuzzles[i] : player.Transform,
                    CombatFaction.Player,
                    player.Transform.gameObject)
                : null;
        }

        RefreshWeaponVisuals(equipmentState.InstalledWeapons, equipmentState.WeaponMuzzles);

        for (int i = 0; i < equipmentState.InstalledModules.Count; i++)
        {
            ModuleDataSO moduleData = ship.startingModules != null && i < ship.startingModules.Count
                ? ship.startingModules[i]
                : null;
            equipmentState.InstalledModules[i] = moduleData;
        }

        NotifyEquipmentStateChanged();
    }

    private void ConfigurePlayerDamageReceiver()
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        ShipDamageReceiver receiver = player.Transform.GetComponent<ShipDamageReceiver>();
        if (receiver == null)
        {
            receiver = player.Transform.gameObject.AddComponent<ShipDamageReceiver>();
        }

        receiver.Initialize(
            CombatFaction.Player,
            ReadPlayerDurability,
            WritePlayerDurability);
        receiver.DamageApplied += OnPlayerDamageApplied;

        TeamMember teamMember = player.Transform.GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = player.Transform.gameObject.AddComponent<TeamMember>();
        }
        teamMember.SetFaction(CombatFaction.Player);
        CombatLayerUtility.ApplyShipLayer(player.Transform.gameObject, CombatFaction.Player);

        player.DamageReceiver = receiver;
        player.TeamMember = teamMember;
    }

    private ShipDurabilityState ReadPlayerDurability()
    {
        return new ShipDurabilityState
        {
            MaxShield = player.MaxShield,
            Shield = player.Shield,
            MaxArmor = player.MaxArmor,
            Armor = player.Armor,
            MaxHull = player.MaxHull,
            Hull = player.Hull
        };
    }

    private void WritePlayerDurability(ShipDurabilityState state)
    {
        player.MaxShield = state.MaxShield;
        player.Shield = state.Shield;
        player.MaxArmor = state.MaxArmor;
        player.Armor = state.Armor;
        player.MaxHull = state.MaxHull;
        player.Hull = state.Hull;
    }

    private void OnPlayerDamageApplied(DamageInfo info, DamageResolutionResult result)
    {
        if (result.AppliedShieldDamage <= 0f)
        {
            return;
        }

        if (player != null && player.ShieldVisual != null)
        {
            player.ShieldVisual.PlayImpact(info.HitPoint, result.AppliedShieldDamage);
        }
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
        player.ShieldVisual = null;
        player.ThrusterEffect = null;

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
        player.ThrusterEffect = EnsureThrusterEffect(playerVisualInstance);

        player.BaseBodyColor = body != null ? body.color : ship.accentColor;
        player.BaseAuraColor = aura != null && aura.color.a > 0.001f ? aura.color : ship.auraColor;
        player.ShieldVisual = EnsureShieldVisual(playerVisualInstance, player.AuraRenderer, player.BaseAuraColor, 0f);
    }

    private static ShipThrusterEffect EnsureThrusterEffect(GameObject shipObject)
    {
        if (shipObject == null)
        {
            return null;
        }

        ShipThrusterEffect effect = shipObject.GetComponent<ShipThrusterEffect>();
        if (effect == null)
        {
            effect = shipObject.AddComponent<ShipThrusterEffect>();
        }

        return effect;
    }

    private static void ResolvePlayerVisualRenderers(Transform visualRoot, out SpriteRenderer body, out SpriteRenderer aura, out SpriteRenderer thruster)
    {
        body = null;
        aura = null;
        thruster = null;
        SpriteRenderer shieldCandidate = null;
        SpriteRenderer auraCandidate = null;

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
            else if (shieldCandidate == null && lowerName.Contains("shield"))
            {
                shieldCandidate = renderers[i];
            }
            else if (auraCandidate == null && lowerName.Contains("aura"))
            {
                auraCandidate = renderers[i];
            }
            else if (thruster == null && (lowerName.Contains("thruster") || lowerName.Contains("engine")))
            {
                thruster = renderers[i];
            }
        }

        aura = shieldCandidate != null ? shieldCandidate : auraCandidate;

        if (body == null && renderers.Length > 0)
        {
            body = renderers[0];
        }
    }

    private void RebuildWeaponSlots(int weaponSlotCount)
    {
        int slotCount = Mathf.Max(0, weaponSlotCount);
        Transform prefabSlotsRoot = playerVisualInstance != null ? FindDirectChild(playerVisualInstance.transform, "WeaponSlots") : null;

        if (prefabSlotsRoot != null)
        {
            weaponSlotsRoot = prefabSlotsRoot;
            for (int i = 0; i < slotCount; i++)
            {
                Transform prefabSlot = FindWeaponMuzzle(prefabSlotsRoot, i);
                if (i < equipmentState.WeaponMuzzles.Count)
                {
                    equipmentState.WeaponMuzzles[i] = prefabSlot != null ? prefabSlot : player.Transform;
                }
            }

            return;
        }

        if (weaponSlotsRoot == null || weaponSlotsRoot == prefabSlotsRoot)
        {
            weaponSlotsRoot = new GameObject("WeaponSlots").transform;
            weaponSlotsRoot.SetParent(player.Transform, false);
        }

        for (int i = 0; i < slotCount; i++)
        {
            Transform slotTransform = FindDirectChild(weaponSlotsRoot, "WeaponSlot_" + (i + 1));
            if (slotTransform == null)
            {
                GameObject slotObject = new GameObject("WeaponSlot_" + (i + 1));
                slotObject.transform.SetParent(weaponSlotsRoot, false);
                slotTransform = slotObject.transform;
            }

            float lerp = slotCount <= 1 ? 0.5f : i / (float)(slotCount - 1);
            float x = Mathf.Lerp(-0.38f, 0.38f, lerp);
            float y = Mathf.Lerp(0.58f, 0.66f, 1f - Mathf.Abs(lerp - 0.5f) * 2f);
            slotTransform.localPosition = new Vector3(x, y, 0f);
            slotTransform.localRotation = Quaternion.identity;
            Transform muzzleTransform = EnsureWeaponMount(slotTransform, i);

            if (i < equipmentState.WeaponMuzzles.Count)
            {
                equipmentState.WeaponMuzzles[i] = muzzleTransform != null ? muzzleTransform : slotTransform;
            }
        }
    }

    private static Transform EnsureWeaponMount(Transform slotTransform, int index)
    {
        if (slotTransform == null)
        {
            return null;
        }

        string mountName = "WeaponMount_" + (index + 1);
        Transform mountTransform = FindDirectChild(slotTransform, mountName);
        if (mountTransform == null)
        {
            GameObject mountObject = new GameObject(mountName);
            mountObject.transform.SetParent(slotTransform, false);
            mountTransform = mountObject.transform;
        }

        Transform muzzleTransform = FindDirectChild(mountTransform, "Muzzle");
        if (muzzleTransform == null)
        {
            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.transform.SetParent(mountTransform, false);
            muzzleTransform = muzzleObject.transform;
        }

        muzzleTransform.localPosition = Vector3.zero;
        muzzleTransform.localRotation = Quaternion.identity;
        return muzzleTransform;
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
        gamePaused = false;
        levelUpPending = false;
        wave = 1;
        gameTimer = 0f;
        enemySpawnSequence = 0;
        targetEnemy = null;
        activePerks.Clear();
        perkPanelObject.SetActive(false);
        ShowGameOverPanel(false);
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
        ResetTimelineRuntime();
        ResetModules();
        ApplyShipDefinition(availableShips[selectedShipIndex], true);
        LogMessage(Localize("log_launch") + availableShips[selectedShipIndex].displayName);
        LogMessage(Localize("log_sector_scan"));
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

    private void ResetTimelineRuntime()
    {
        spawnEventStates.Clear();
        if (currentTimeline == null || currentTimeline.events == null)
        {
            return;
        }

        for (int i = 0; i < currentTimeline.events.Count; i++)
        {
            spawnEventStates.Add(new SpawnEventRuntimeState());
        }
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

    private float GetTimelineLevelScale()
    {
        return 1f + (wave - 1) * timelineDifficultyPerPhase;
    }

    private string NextEnemyId()
    {
        enemySpawnSequence++;
        return "E-" + enemySpawnSequence.ToString("0000");
    }

    private void SpawnEnemyFromTimeline(ShipDataSO shipData, Vector3 position)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            return;
        }

        EnemyShip enemy = CreateEnemy(NextEnemyId(), shipData, position, GetTimelineLevelScale());
        if (enemy == null)
        {
            return;
        }

        enemies.Add(enemy);
        if (targetEnemy == null)
        {
            targetEnemy = enemy;
        }
    }

    private void ExecuteOneShotPattern(SpawnEvent spawnEvent)
    {
        if (spawnEvent == null || spawnEvent.shipData == null)
        {
            return;
        }

        int count = Mathf.Max(0, spawnEvent.count);
        if (count <= 0)
        {
            return;
        }

        switch (spawnEvent.pattern)
        {
            case SpawnPatternType.Burst:
                ExecuteBurstPattern(spawnEvent.shipData, count);
                break;
            case SpawnPatternType.Ring:
                ExecuteRingPattern(spawnEvent.shipData, count);
                break;
            case SpawnPatternType.Wall:
                ExecuteWallPattern(spawnEvent.shipData, count);
                break;
            case SpawnPatternType.Continuous:
            default:
                break;
        }
    }

    private void ExecuteBurstPattern(ShipDataSO shipData, int count)
    {
        Vector3 center = GetRandomOffscreenSpawnPosition();
        const float scatterRadius = 1.1f;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
            SpawnEnemyFromTimeline(shipData, center + new Vector3(offset.x, offset.y, 0f));
        }
    }

    private void ExecuteRingPattern(ShipDataSO shipData, int count)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null || player == null || player.Transform == null)
        {
            return;
        }

        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        float radius = Vector2.Distance(min, max) * 0.55f;
        Vector3 center = player.Transform.position;
        float startAngle = UnityEngine.Random.value * Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + ((Mathf.PI * 2f) * i / Mathf.Max(1, count));
            Vector3 position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            SpawnEnemyFromTimeline(shipData, position);
        }
    }

    private void ExecuteWallPattern(ShipDataSO shipData, int count)
    {
        int side = UnityEngine.Random.Range(0, 4);
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            SpawnEnemyFromTimeline(shipData, GetOffscreenSpawnPoint(side, t, offscreenViewportMargin));
        }
    }

    private EnemyShip CreateEnemy(string id, ShipDataSO shipData, Vector3 position, float levelScale)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            Debug.LogError("SpaceCombatSceneController: ShipDataSO or ship prefab is missing for enemy " + id + ".");
            return null;
        }

        string typeName = string.IsNullOrEmpty(shipData.displayName) ? shipData.name : shipData.displayName;
        GameObject enemyPrefab = shipData.shipPrefab;

        GameObject enemyObject = poolService.Get(enemyPrefab, enemyRoot);
        if (enemyObject == null)
        {
            return null;
        }

        enemyObject.name = id;
        enemyObject.transform.position = position;
        AssignEnemyIdentity(enemyObject);

        SpriteRenderer bodyRenderer = enemyObject.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer shieldRenderer = FindChildSpriteRenderer(enemyObject.transform, "Shield");
        if (shieldRenderer == null)
        {
            shieldRenderer = FindChildSpriteRendererContaining(enemyObject.transform, "shield");
        }
        if (shieldRenderer == null)
        {
            shieldRenderer = FindChildSpriteRendererContaining(enemyObject.transform, "aura");
        }
        SpriteRenderer targetRenderer = FindChildSpriteRenderer(enemyObject.transform, "TargetRing");
        if (targetRenderer != null)
        {
            targetRenderer.gameObject.SetActive(false);
        }
        SpriteRenderer thrusterRenderer = FindChildSpriteRenderer(enemyObject.transform, "Thruster");
        ShipThrusterEffect thrusterEffect = EnsureThrusterEffect(enemyObject);

        float shieldValue = Mathf.Max(1f, shipData.maxShield * levelScale);
        float armorValue = Mathf.Max(1f, shipData.maxArmor * levelScale);
        float hullValue = Mathf.Max(1f, shipData.maxHull * levelScale);
        float enemyMoveSpeed = Mathf.Max(0.5f, shipData.maxSpeed * 0.22f) + levelScale * 0.2f;
        List<WeaponDataSO> compatibleWeapons = new List<WeaponDataSO>();
        if (shipData.startingWeapons != null)
        {
            for (int i = 0; i < shipData.startingWeapons.Count; i++)
            {
                WeaponDataSO slotWeapon = shipData.startingWeapons[i];
                if (slotWeapon == null || !CanShipUseWeapon(shipData.shipClass, slotWeapon))
                {
                    continue;
                }

                compatibleWeapons.Add(slotWeapon);
            }
        }

        WeaponDataSO enemyWeapon = compatibleWeapons.Count > 0 ? compatibleWeapons[0] : GetPrimaryWeapon(shipData);
        float enemyDamage = enemyWeapon != null
            ? Mathf.Max(1f, enemyWeapon.damage * Mathf.Max(0.1f, shipData.damageMultiplier) * levelScale)
            : Mathf.Max(6f, 10f * levelScale);

        float weaponCooldown = enemyWeapon != null
            ? Mathf.Max(0.05f, enemyWeapon.cooldown > 0f ? enemyWeapon.cooldown : enemyWeapon.fireRate)
            : UnityEngine.Random.Range(1.15f, 1.8f);
        float weaponRange = enemyWeapon != null
            ? Mathf.Max(enemyWeapon.maxRange, enemyWeapon.projectileMaxDistance)
            : 5.2f;
        weaponRange = Mathf.Max(4.5f, weaponRange);
        float preferredDistance = Mathf.Clamp(weaponRange * UnityEngine.Random.Range(0.68f, 0.82f), 3.9f, 7.5f);
        float retreatDistance = Mathf.Max(2.8f, preferredDistance * 0.72f);
        float reengageDistance = Mathf.Max(retreatDistance + 0.6f, preferredDistance * 0.94f);

        EnemyShip enemy = new EnemyShip
        {
            Id = id,
            Type = typeName,
            Transform = enemyObject.transform,
            BodyRenderer = bodyRenderer,
            ShieldRenderer = shieldRenderer,
            TargetRenderer = targetRenderer,
            ThrusterRenderer = thrusterRenderer,
            ThrusterEffect = thrusterEffect,
            OrbitDistance = preferredDistance,
            OrbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            OrbitSpeed = UnityEngine.Random.Range(0.4f, 0.95f),
            RetreatDistance = retreatDistance,
            ReengageDistance = reengageDistance,
            DistanceResponsiveness = UnityEngine.Random.Range(1.25f, 1.75f),
            RetreatSpeedMultiplier = UnityEngine.Random.Range(1.8f, 2.35f),
            AttackCooldown = weaponCooldown,
            AttackTimer = UnityEngine.Random.Range(0f, 0.7f),
            Damage = enemyDamage,
            ScoreValue = shipData.scoreReward > 0 ? shipData.scoreReward : 40,
            DriftSpeed = enemyMoveSpeed,
            MaxShield = shieldValue,
            Shield = shieldValue,
            MaxArmor = armorValue,
            Armor = armorValue,
            MaxHull = hullValue,
            Hull = hullValue,
            WeaponDamageMultiplier = Mathf.Max(0.1f, shipData.damageMultiplier) * levelScale,
            Prefab = enemyPrefab,
            BaseBodyColor = bodyRenderer != null ? bodyRenderer.color : Color.white,
            BaseShieldColor = shieldRenderer != null && shieldRenderer.color.a > 0.001f ? shieldRenderer.color : shipData.auraColor
        };
        enemy.ShieldVisual = EnsureShieldVisual(enemyObject, enemy.ShieldRenderer, enemy.BaseShieldColor, enemies.Count * 0.47f);

        if (compatibleWeapons.Count == 0 && enemyWeapon != null)
        {
            compatibleWeapons.Add(enemyWeapon);
        }

        ShipDamageReceiver receiver = enemyObject.GetComponent<ShipDamageReceiver>();
        if (receiver == null)
        {
            receiver = enemyObject.AddComponent<ShipDamageReceiver>();
        }
        receiver.Initialize(
            CombatFaction.Enemy,
            () => new ShipDurabilityState
            {
                MaxShield = enemy.MaxShield,
                Shield = enemy.Shield,
                MaxArmor = enemy.MaxArmor,
                Armor = enemy.Armor,
                MaxHull = enemy.MaxHull,
                Hull = enemy.Hull
            },
            state =>
            {
                enemy.MaxShield = state.MaxShield;
                enemy.Shield = state.Shield;
                enemy.MaxArmor = state.MaxArmor;
                enemy.Armor = state.Armor;
                enemy.MaxHull = state.MaxHull;
                enemy.Hull = state.Hull;
            });
        receiver.DamageApplied += (info, result) => OnEnemyDamageApplied(enemy, info, result);
        enemy.DamageReceiver = receiver;
        enemy.TeamMember = enemyObject.GetComponent<TeamMember>();

        for (int i = 0; i < compatibleWeapons.Count; i++)
        {
            WeaponDataSO weapon = compatibleWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            WeaponInstance instance = new WeaponInstance(
                weapon,
                enemyObject.transform,
                FindWeaponMuzzle(enemyObject.transform, i),
                CombatFaction.Enemy,
                enemyObject);

            if (instance.BeginFire())
            {
                // Warmup then reset to randomized readiness.
                instance.Tick(UnityEngine.Random.Range(0f, instance.EffectiveCooldown));
            }

            enemy.WeaponInstances.Add(instance);
            AttachWeaponVisual(weapon, instance.MuzzleTransform);
        }

        return enemy;
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
            existingVisual.SetActive(weapon != null);
            return;
        }

        if (weapon == null || weapon.visualPrefab == null)
        {
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

    private static void AssignEnemyIdentity(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        try
        {
            enemyObject.tag = "Enemy";
        }
        catch (UnityException)
        {
            // Enemy tag may be absent in project settings.
        }

        TeamMember teamMember = enemyObject.GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = enemyObject.AddComponent<TeamMember>();
        }
        teamMember.SetFaction(CombatFaction.Enemy);

        CombatLayerUtility.ApplyShipLayer(enemyObject, CombatFaction.Enemy);
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
            BindAuthoredInspectorHud(uiRoot);
            return;
        }

        Image rootBackground = CreateImage("Frame", uiRoot, new Color(0.01f, 0.015f, 0.02f, 0f));
        RectTransform rootRect = rootBackground.rectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateRightOverviewPanel(uiRoot);
        CreateCombatLogPanel(uiRoot);
        CreateGateHint(uiRoot);
        CreatePlayerStatusHud(uiRoot);
        if (uiRoot.Find("EquipmentPanel") == null)
        {
            CreateEquipmentHud(uiRoot);
        }
        CreateModuleHud(uiRoot);
        CreatePauseHudButton(uiRoot);
        CreateStatusLabel(uiRoot);
        CreatePerkPanel(uiRoot);
        CreateGameOverPanel(uiRoot);
        CreatePauseMenu(uiRoot);
        CreateStartMenu(uiRoot);
        if (useVirtualJoystick)
        {
            CreateVirtualJoystick(uiRoot);
        }
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

    private void BindAuthoredInspectorHud(Transform uiRoot)
    {
        BindOverviewPanel(uiRoot);
        BindCombatLogPanel(uiRoot);
        BindGateHint(uiRoot);
        BindPlayerStatusPanel(uiRoot);
        BindEquipmentPanel(uiRoot);
        BindModulePanel(uiRoot);
        pauseHudButtonView = BindMenuButton(uiRoot.Find("PauseButton"), "PauseButton");
        statusText = FindText(uiRoot, "StatusLabel");
        BindPerkPanel(uiRoot);
        BindGameOverPanel(uiRoot);
        BindPauseMenu(uiRoot);
        BindStartMenu(uiRoot);
        BindVirtualJoystick(uiRoot);
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
        targetPanel = FindImage(panel, "TargetPanel");
        Transform target = panel.Find("TargetPanel");
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

            enemyRows.Add(new EnemyRow
            {
                RootTransform = rowTransform.GetComponent<RectTransform>(),
                RootText = FindText(rowTransform, "RowText"),
                ShieldFill = FindIndexedFill(rowTransform, "BarBg", 0),
                ArmorFill = FindIndexedFill(rowTransform, "BarBg", 1),
                HullFill = FindIndexedFill(rowTransform, "BarBg", 2)
            });
        }
    }

    private void BindCombatLogPanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("CombatLog");
        if (panel == null)
        {
            return;
        }

        combatLogTitleText = FindText(panel, "Label");
        combatLogText = FindText(panel, "Viewport/Content/Text");
        Transform content = panel.Find("Viewport/Content");
        combatLogContentRect = content != null ? content.GetComponent<RectTransform>() : null;
        combatLogScrollRect = panel.GetComponent<ScrollRect>();
        if (combatLogScrollRect == null)
        {
            combatLogScrollRect = panel.gameObject.AddComponent<ScrollRect>();
        }

        Transform viewport = panel.Find("Viewport");
        combatLogScrollRect.viewport = viewport != null ? viewport.GetComponent<RectTransform>() : null;
        combatLogScrollRect.content = combatLogContentRect;
        combatLogScrollRect.horizontal = false;
        combatLogScrollRect.vertical = true;
        combatLogScrollRect.movementType = ScrollRect.MovementType.Clamped;
        combatLogScrollRect.scrollSensitivity = 24f;
    }

    private void BindGateHint(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("GateHint");
        gateHintText = FindText(panel, "Text");
    }

    private void BindPlayerStatusPanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("PlayerStatus");
        if (panel == null)
        {
            return;
        }

        playerStatusTitleText = FindText(panel, "Label");
        playerLevelBadgeText = FindText(panel, "LevelBadge");
        playerShieldFill = FindImage(panel, "ShieldBg/ShieldFill");
        playerArmorFill = FindImage(panel, "ArmorBg/ArmorFill");
        playerHullFill = FindImage(panel, "HullBg/HullFill");
        playerExperienceFill = FindImage(panel, "XPBg/XPFill");
        playerShieldValueText = FindText(panel, "ShieldBg/Value");
        playerArmorValueText = FindText(panel, "ArmorBg/Value");
        playerHullValueText = FindText(panel, "HullBg/Value");
        playerExperienceValueText = FindText(panel, "XPBg/Value");
        capacitorFill = FindImage(panel, "CapacitorFill");
        capacitorValueText = FindText(panel, "CapValue");
    }

    private void BindEquipmentPanel(Transform uiRoot)
    {
        Transform authoredPanel = uiRoot.Find("EquipmentPanel");
        Transform runtimePanel = uiRoot.Find("EquipmentPanelRuntime");
        Transform panel = authoredPanel;
        if (panel == null)
        {
            if (runtimePanel != null)
            {
                runtimePanel.gameObject.SetActive(false);
            }
            return;
        }

        panel.gameObject.SetActive(true);
        if (runtimePanel != null)
        {
            runtimePanel.gameObject.SetActive(false);
        }

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        bool panelHadInvertedAnchors = NormalizeAuthoredRect(panelRect);
        AlignEquipmentPanelToModulePanelIfNeeded(uiRoot, panelRect, panelHadInvertedAnchors);

        DisableDuplicateEquipmentPanels(uiRoot, panel);

        EquipmentUIController runtimeController = panel.GetComponent<EquipmentUIController>();
        if (runtimeController == null)
        {
            runtimeController = panel.gameObject.AddComponent<EquipmentUIController>();
        }
        else
        {
            runtimeController.enabled = true;
        }

        Transform weaponsRow = panel.Find("WeaponsRow");
        Transform modulesRow = panel.Find("ModulesRow");
        runtimeController.Configure(
            this,
            slotUiPrefab,
            weaponsRow != null ? weaponsRow.GetComponent<RectTransform>() : null,
            modulesRow != null ? modulesRow.GetComponent<RectTransform>() : null);
        equipmentUiController = runtimeController;
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

    private void BindModulePanel(Transform uiRoot)
    {
        Transform panel = uiRoot.Find("ModulePanel");
        modulePanelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        BindModuleSlots();
    }

    private void BindPerkPanel(Transform uiRoot)
    {
        Transform root = uiRoot.Find("PerkPanel");
        perkPanelObject = root != null ? root.gameObject : null;
        Transform content = root != null ? root.Find("Content") : null;
        perkTitleText = FindText(content, "Title");
        perkHintText = FindText(content, "Choices");
        for (int i = 0; i < perkOptionTexts.Length; i++)
        {
            Transform option = content != null ? content.Find("PerkOption_" + i) : null;
            perkOptionRects[i] = option != null ? option.GetComponent<RectTransform>() : null;
            perkOptionTexts[i] = FindText(option, "Label");
            EnsureButton(option);
        }
        if (perkPanelObject != null)
        {
            perkPanelObject.SetActive(false);
        }
    }

    private void BindGameOverPanel(Transform uiRoot)
    {
        Transform root = uiRoot.Find("GameOverPanel");
        gameOverPanelObject = root != null ? root.gameObject : null;
        Transform content = root != null ? root.Find("Content") : null;
        retryButtonView = BindMenuButton(content != null ? content.Find("gameover_retry") : null, "gameover_retry");
        gameOverMenuButtonView = BindMenuButton(content != null ? content.Find("gameover_menu") : null, "gameover_menu");
        gameOverExitButtonView = BindMenuButton(content != null ? content.Find("gameover_exit") : null, "gameover_exit");
        if (gameOverPanelObject != null)
        {
            gameOverPanelObject.SetActive(false);
        }
    }

    private void BindPauseMenu(Transform uiRoot)
    {
        Transform root = uiRoot.Find("PauseMenu");
        pauseMenuObject = root != null ? root.gameObject : null;
        Transform panel = root != null ? root.Find("Panel") : null;
        pauseResumeButtonView = BindMenuButton(panel != null ? panel.Find("pause_resume") : null, "pause_resume");
        pauseSettingsButtonView = BindMenuButton(panel != null ? panel.Find("pause_settings") : null, "pause_settings");
        pauseMenuButtonView = BindMenuButton(panel != null ? panel.Find("pause_menu") : null, "pause_menu");
        if (pauseMenuObject != null)
        {
            pauseMenuObject.SetActive(false);
        }
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
        targetShieldValueText = CreateBarValueText(targetShieldFill, 252f);
        targetArmorValueText = CreateBarValueText(targetArmorFill, 252f);
        targetHullValueText = CreateBarValueText(targetHullFill, 252f);

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

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panel.transform, false);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;
        Mask viewportMask = viewportObject.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetAnchoredRect(viewportRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -36f));

        combatLogContentRect = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        combatLogContentRect.SetParent(viewportRect, false);
        combatLogContentRect.anchorMin = new Vector2(0f, 1f);
        combatLogContentRect.anchorMax = new Vector2(1f, 1f);
        combatLogContentRect.pivot = new Vector2(0f, 1f);
        combatLogContentRect.anchoredPosition = Vector2.zero;
        combatLogContentRect.sizeDelta = new Vector2(0f, 160f);

        combatLogText = CreateText("Text", combatLogContentRect, string.Empty, 13, FontStyle.Normal, new Color(0.74f, 0.86f, 1f));
        combatLogText.alignment = TextAlignmentOptions.TopLeft;
        combatLogText.textWrappingMode = TextWrappingModes.Normal;
        combatLogText.overflowMode = TextOverflowModes.Overflow;
        SetAnchoredRect(combatLogText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -160f));

        combatLogScrollRect = panel.gameObject.AddComponent<ScrollRect>();
        combatLogScrollRect.viewport = viewportRect;
        combatLogScrollRect.content = combatLogContentRect;
        combatLogScrollRect.horizontal = false;
        combatLogScrollRect.vertical = true;
        combatLogScrollRect.movementType = ScrollRect.MovementType.Clamped;
        combatLogScrollRect.scrollSensitivity = 24f;
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
        playerShieldValueText = CreateBarValueText(playerShieldFill, 180f);
        playerArmorValueText = CreateBarValueText(playerArmorFill, 180f);
        playerHullValueText = CreateBarValueText(playerHullFill, 180f);
        playerExperienceValueText = CreateBarValueText(playerExperienceFill, 180f);

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

    private void CreatePauseHudButton(Transform parent)
    {
        pauseHudButtonView = CreateMenuButton(parent, "PauseButton", new Vector2(0f, 1f), new Vector2(46f, -34f), new Vector2(76f, 38f));
        pauseHudButtonView.Label.text = "MENU";
        pauseHudButtonView.Label.fontSize = 14f;
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
        perkHintText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(perkHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 18f), new Vector2(-24f, 44f));

        for (int i = 0; i < perkOptionTexts.Length; i++)
        {
            Image option = CreateImage("PerkOption_" + i, panel.transform, new Color(0.08f, 0.16f, 0.22f, 0.96f));
            option.gameObject.AddComponent<Button>();
            RectTransform optionRect = option.rectTransform;
            optionRect.anchorMin = new Vector2(0f, 1f);
            optionRect.anchorMax = new Vector2(1f, 1f);
            optionRect.pivot = new Vector2(0.5f, 1f);
            optionRect.sizeDelta = new Vector2(-48f, 42f);
            optionRect.anchoredPosition = new Vector2(0f, -72f - i * 50f);
            AddOutline(option.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

            TMP_Text optionText = CreateText("Label", option.transform, string.Empty, 17, FontStyle.Bold, Color.white);
            optionText.alignment = TextAlignmentOptions.Center;
            SetAnchoredRect(optionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            perkOptionRects[i] = optionRect;
            perkOptionTexts[i] = optionText;
        }

        perkPanelObject.SetActive(false);
    }

    private void CreateGameOverPanel(Transform parent)
    {
        gameOverPanelObject = new GameObject("GameOverPanel", typeof(RectTransform));
        gameOverPanelObject.transform.SetParent(parent, false);
        RectTransform rootRect = gameOverPanelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = CreateImage("Dimmer", gameOverPanelObject.transform, new Color(0f, 0f, 0f, 0.58f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = CreateImage("Content", gameOverPanelObject.transform, new Color(0.06f, 0.1f, 0.14f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 300f);
        AddOutline(panel.gameObject, new Color(0.55f, 0.18f, 0.18f, 1f));

        TMP_Text title = CreateText("Title", panel.transform, currentLanguage == LanguageOption.RU ? "КОРАБЛЬ УНИЧТОЖЕН" : "SHIP DESTROYED", 28, FontStyle.Bold, new Color(1f, 0.42f, 0.36f));
        title.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -24f), new Vector2(-20f, -64f));

        retryButtonView = CreateMenuButton(panel.transform, "gameover_retry", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(260f, 52f));
        gameOverMenuButtonView = CreateMenuButton(panel.transform, "gameover_menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(260f, 52f));
        gameOverExitButtonView = CreateMenuButton(panel.transform, "gameover_exit", new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(260f, 52f));

        gameOverPanelObject.SetActive(false);
    }

    private void CreatePauseMenu(Transform parent)
    {
        pauseMenuObject = new GameObject("PauseMenu", typeof(RectTransform));
        pauseMenuObject.transform.SetParent(parent, false);
        RectTransform rootRect = pauseMenuObject.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image dim = CreateImage("Dimmer", pauseMenuObject.transform, new Color(0f, 0f, 0f, 0.42f));
        RectTransform dimRect = dim.rectTransform;
        StretchToParent(dimRect);

        Image panel = CreateImage("Panel", pauseMenuObject.transform, new Color(0.04f, 0.08f, 0.12f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 270f);
        AddOutline(panel.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        TMP_Text title = CreateText("Title", panel.transform, "PAUSE", 28, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -22f), new Vector2(-18f, -62f));

        pauseResumeButtonView = CreateMenuButton(panel.transform, "pause_resume", new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(260f, 52f));
        pauseSettingsButtonView = CreateMenuButton(panel.transform, "pause_settings", new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(260f, 52f));
        pauseMenuButtonView = CreateMenuButton(panel.transform, "pause_menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(260f, 52f));

        pauseMenuObject.SetActive(false);
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
        if (retryButtonView != null) retryButtonView.Label.text = currentLanguage == LanguageOption.RU ? "Повторить" : "Retry";
        if (gameOverMenuButtonView != null) gameOverMenuButtonView.Label.text = currentLanguage == LanguageOption.RU ? "В меню" : "Main menu";
        if (gameOverExitButtonView != null) gameOverExitButtonView.Label.text = currentLanguage == LanguageOption.RU ? "Выйти" : "Exit";
        if (pauseResumeButtonView != null) pauseResumeButtonView.Label.text = Localize("menu_continue");
        if (pauseSettingsButtonView != null) pauseSettingsButtonView.Label.text = Localize("menu_settings");
        if (pauseMenuButtonView != null) pauseMenuButtonView.Label.text = Localize("pause_to_menu");
        if (pauseHudButtonView != null) pauseHudButtonView.Label.text = currentLanguage == LanguageOption.RU ? "МЕНЮ" : "MENU";
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
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ResumeRun();
            return;
        }

        HandleStartMenuInput();

        Vector2 pointerPosition;
        if (!TryGetPrimaryPointerDown(out pointerPosition))
        {
            return;
        }

        if (IsButtonClicked(pauseResumeButtonView, pointerPosition))
        {
            ResumeRun();
            return;
        }

        if (IsButtonClicked(pauseSettingsButtonView, pointerPosition))
        {
            ShowPauseMenu(false);
            ShowStartMenu(true);
            SetStartMenuPage(StartMenuPage.Settings);
            return;
        }

        if (IsButtonClicked(pauseMenuButtonView, pointerPosition))
        {
            ShowPauseMenu(false);
            ShowStartMenu(true);
            SetStartMenuPage(StartMenuPage.Main);
        }
    }

    private void HandleGameOverInput()
    {
        Vector2 pointerPosition;
        if (!TryGetPrimaryPointerDown(out pointerPosition))
        {
            return;
        }

        if (IsButtonClicked(retryButtonView, pointerPosition))
        {
            StartRun();
            return;
        }

        if (IsButtonClicked(gameOverMenuButtonView, pointerPosition))
        {
            ReturnToMainMenu();
            return;
        }

        if (IsButtonClicked(gameOverExitButtonView, pointerPosition))
        {
            ExitGame();
        }
    }

    private void ReturnToMainMenu()
    {
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
        if (gameOverPanelObject != null)
        {
            gameOverPanelObject.SetActive(show);
        }
    }

    private void ShowPauseMenu(bool show)
    {
        if (pauseMenuObject != null)
        {
            pauseMenuObject.SetActive(show);
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

        TMP_Text text = CreateText("RowText", rowBackground.transform, string.Empty, 12, FontStyle.Bold, new Color(0.85f, 0.92f, 1f));
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
        HandleCameraZoom(deltaTime);

        Vector2 moveInput = GetMovementVector(keyboard);
        if (moveInput.sqrMagnitude > 0.01f)
        {
            player.MoveCommandActive = false;
        }

        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition))
        {
            if (IsButtonClicked(pauseHudButtonView, pointerPosition))
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

    private void HandleCameraZoom(float deltaTime)
    {
        if (mainCamera == null)
        {
            return;
        }

        float scrollY = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            targetCameraOrthographicSize -= Mathf.Sign(scrollY) * cameraZoomStep;
            targetCameraOrthographicSize = Mathf.Clamp(targetCameraOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        }

        mainCamera.orthographicSize = Mathf.Lerp(
            mainCamera.orthographicSize,
            targetCameraOrthographicSize,
            cameraZoomSmoothing * deltaTime);
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
                    SelectTargetEnemy(enemy);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TrySelectEnemyFromWorld(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        EnemyShip selectedEnemy = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive())
            {
                continue;
            }

            Bounds bounds;
            if (!TryCalculateTargetBounds(enemy, out bounds))
            {
                continue;
            }

            bounds.Expand(new Vector3(targetWorldClickPadding, targetWorldClickPadding, 0f));
            if (!bounds.Contains(worldPosition))
            {
                continue;
            }

            float distanceSqr = ((Vector2)bounds.center - (Vector2)worldPosition).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                selectedEnemy = enemy;
            }
        }

        if (selectedEnemy == null)
        {
            return false;
        }

        SelectTargetEnemy(selectedEnemy);
        return true;
    }

    private void SelectTargetEnemy(EnemyShip enemy)
    {
        if (enemy == null || !enemy.IsAlive())
        {
            return;
        }

        targetEnemy = enemy;
        UpdateTargetState();
        LogMessage(Localize("log_target_locked") + enemy.Id);
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

    private void EnsureSpawnEventRuntimeStates()
    {
        if (currentTimeline == null || currentTimeline.events == null)
        {
            spawnEventStates.Clear();
            return;
        }

        while (spawnEventStates.Count < currentTimeline.events.Count)
        {
            spawnEventStates.Add(new SpawnEventRuntimeState());
        }

        while (spawnEventStates.Count > currentTimeline.events.Count)
        {
            spawnEventStates.RemoveAt(spawnEventStates.Count - 1);
        }
    }

    private Vector3 GetRandomOffscreenSpawnPosition()
    {
        int side = UnityEngine.Random.Range(0, 4);
        float t = UnityEngine.Random.value;
        return GetOffscreenSpawnPoint(side, t, offscreenViewportMargin);
    }

    private Vector3 GetOffscreenSpawnPoint(int side, float edgeLerp, float viewportMargin)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null)
        {
            return player != null ? player.Transform.position : Vector3.zero;
        }

        float margin = Mathf.Max(0.01f, viewportMargin);
        float t = Mathf.Clamp01(edgeLerp);
        float depth = Mathf.Abs(camera.transform.position.z);

        float x;
        float y;
        switch (Mathf.Abs(side) % 4)
        {
            case 0: // top
                x = Mathf.Lerp(-margin, 1f + margin, t);
                y = 1f + margin;
                break;
            case 1: // bottom
                x = Mathf.Lerp(-margin, 1f + margin, t);
                y = -margin;
                break;
            case 2: // left
                x = -margin;
                y = Mathf.Lerp(-margin, 1f + margin, t);
                break;
            default: // right
                x = 1f + margin;
                y = Mathf.Lerp(-margin, 1f + margin, t);
                break;
        }

        Vector3 world = camera.ViewportToWorldPoint(new Vector3(x, y, depth));
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
        CombatUpdateContext context = new CombatUpdateContext
        {
            Player = player,
            Enemies = enemies,
            Modules = modules,
            EquipmentState = equipmentState,
            TargetEnemy = targetEnemy,
            ProjectileRoot = projectileRoot,
            PoolService = poolService,
            Wave = wave,
            Localize = Localize,
            LogMessage = LogMessage,
            UpdateModuleVisual = UpdateModuleVisual,
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

        for (int i = 0; i < activePerks.Count; i++)
        {
            if (i < perkOptionTexts.Length && perkOptionTexts[i] != null)
            {
                perkOptionTexts[i].text = (i + 1) + ". " + activePerks[i].Label;
                perkOptionTexts[i].transform.parent.gameObject.SetActive(true);
            }
        }

        for (int i = activePerks.Count; i < perkOptionTexts.Length; i++)
        {
            if (perkOptionTexts[i] != null)
            {
                perkOptionTexts[i].text = string.Empty;
                perkOptionTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }

        perkHintText.text = Localize("perk_pick");
        LogMessage(Localize("log_levelup"), "warning");
    }

    private void UpdatePerkSelectionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) ApplyPerk(0);
            if (keyboard.digit2Key.wasPressedThisFrame) ApplyPerk(1);
            if (keyboard.digit3Key.wasPressedThisFrame) ApplyPerk(2);
        }

        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition))
        {
            TryApplyPerkFromPointer(pointerPosition);
        }
    }

    private bool TryApplyPerkFromPointer(Vector2 screenPosition)
    {
        for (int i = 0; i < activePerks.Count && i < perkOptionRects.Length; i++)
        {
            RectTransform optionRect = perkOptionRects[i];
            if (optionRect != null && RectTransformUtility.RectangleContainsScreenPoint(optionRect, screenPosition, null))
            {
                ApplyPerk(i);
                return true;
            }
        }

        return false;
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

    private void UpdateTimelineSpawner(float deltaTime)
    {
        float previousTime = gameTimer;
        gameTimer += Mathf.Max(0f, deltaTime);
        wave = Mathf.Max(1, 1 + Mathf.FloorToInt(gameTimer / Mathf.Max(1f, timelinePhaseDuration)));

        EnsureSpawnEventRuntimeStates();
        if (currentTimeline == null || currentTimeline.events == null || currentTimeline.events.Count == 0)
        {
            if (gateHintText != null)
            {
                gateHintText.transform.parent.gameObject.SetActive(true);
                gateHintText.text = currentLanguage == LanguageOption.RU
                    ? "Таймлайн волн не назначен."
                    : "Wave timeline is not assigned.";
            }
            return;
        }

        int spawnedThisFrame = 0;
        for (int i = 0; i < currentTimeline.events.Count; i++)
        {
            SpawnEvent spawnEvent = currentTimeline.events[i];
            SpawnEventRuntimeState state = spawnEventStates[i];
            if (spawnEvent == null || spawnEvent.shipData == null || spawnEvent.shipData.shipPrefab == null)
            {
                continue;
            }

            float startTime = Mathf.Max(0f, spawnEvent.startTime);
            int count = Mathf.Max(0, spawnEvent.count);
            if (count <= 0)
            {
                continue;
            }

            if (spawnEvent.pattern == SpawnPatternType.Continuous)
            {
                float duration = Mathf.Max(0f, spawnEvent.duration);
                if (duration <= 0f)
                {
                    continue;
                }

                float endTime = startTime + duration;
                float activeStart = Mathf.Max(previousTime, startTime);
                float activeEnd = Mathf.Min(gameTimer, endTime);
                if (activeEnd <= activeStart)
                {
                    continue;
                }

                state.continuousAccumulator += (activeEnd - activeStart) * count;
                int spawnCount = Mathf.FloorToInt(state.continuousAccumulator);
                if (spawnCount <= 0)
                {
                    continue;
                }

                state.continuousAccumulator -= spawnCount;
                for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
                {
                    SpawnEnemyFromTimeline(spawnEvent.shipData, GetRandomOffscreenSpawnPosition());
                    spawnedThisFrame++;
                }

                continue;
            }

            if (state.oneShotExecuted || gameTimer < startTime)
            {
                continue;
            }

            int enemyCountBefore = enemies.Count;
            ExecuteOneShotPattern(spawnEvent);
            state.oneShotExecuted = true;
            int spawnedInPattern = enemies.Count - enemyCountBefore;
            if (spawnedInPattern > 0)
            {
                spawnedThisFrame += spawnedInPattern;
            }
        }

        if (spawnedThisFrame > 0)
        {
            UpdateTargetState();
            LogMessage(Localize("log_hostiles") + enemies.Count);
        }

        if (gateHintText != null)
        {
            gateHintText.transform.parent.gameObject.SetActive(true);
            float nextEventTime = GetNextTimelineEventTime(gameTimer);
            if (nextEventTime < 0f)
            {
                gateHintText.text = currentLanguage == LanguageOption.RU
                    ? "Таймлайн завершён."
                    : "Timeline complete.";
            }
            else
            {
                float timeLeft = Mathf.Max(0f, nextEventTime - gameTimer);
                gateHintText.text = currentLanguage == LanguageOption.RU
                    ? "Следующий эвент через " + timeLeft.ToString("0.0") + "с"
                    : "Next event in " + timeLeft.ToString("0.0") + "s";
            }
        }
    }

    private float GetNextTimelineEventTime(float now)
    {
        if (currentTimeline == null || currentTimeline.events == null || currentTimeline.events.Count == 0)
        {
            return -1f;
        }

        float nextTime = float.MaxValue;
        for (int i = 0; i < currentTimeline.events.Count; i++)
        {
            SpawnEvent spawnEvent = currentTimeline.events[i];
            if (spawnEvent == null)
            {
                continue;
            }

            float startTime = Mathf.Max(0f, spawnEvent.startTime);
            if (spawnEvent.pattern == SpawnPatternType.Continuous)
            {
                float duration = Mathf.Max(0f, spawnEvent.duration);
                if (duration <= 0f)
                {
                    continue;
                }

                float endTime = startTime + duration;
                if (now <= endTime)
                {
                    if (now < startTime)
                    {
                        nextTime = Mathf.Min(nextTime, startTime);
                    }
                    else
                    {
                        return now;
                    }
                }
                continue;
            }

            if (i < spawnEventStates.Count && spawnEventStates[i].oneShotExecuted)
            {
                continue;
            }

            if (now < startTime)
            {
                nextTime = Mathf.Min(nextTime, startTime);
            }
            else
            {
                return now;
            }
        }

        return nextTime == float.MaxValue ? -1f : nextTime;
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

            if (enemy.TargetRenderer != null)
            {
                enemy.TargetRenderer.gameObject.SetActive(enemy == targetEnemy);
                enemy.TargetRenderer.transform.Rotate(0f, 0f, 65f * Time.deltaTime);
            }
        }

        UpdateTargetingVisuals();
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

    private void UpdateTargetingVisuals()
    {
        bool hasTarget = targetEnemy != null && targetEnemy.IsAlive() && player != null && player.Transform != null;
        if (!hasTarget)
        {
            SetTargetingVisualsActive(false);
            return;
        }

        EnsureTargetingVisuals();

        Bounds targetBounds;
        if (!TryCalculateTargetBounds(targetEnemy, out targetBounds))
        {
            targetBounds = new Bounds(targetEnemy.Transform.position, Vector3.one);
        }

        if (targetFrameRenderer != null && targetFrameRenderer.sprite != null)
        {
            Vector3 size = targetBounds.size + new Vector3(targetFramePadding, targetFramePadding, 0f);
            size.x = Mathf.Max(0.6f, size.x);
            size.y = Mathf.Max(0.6f, size.y);
            Vector3 spriteSize = targetFrameRenderer.sprite.bounds.size;
            targetFrameObject.SetActive(true);
            targetFrameObject.transform.position = new Vector3(targetBounds.center.x, targetBounds.center.y, 0f);
            targetFrameObject.transform.rotation = Quaternion.identity;
            targetFrameObject.transform.localScale = new Vector3(
                spriteSize.x > 0.001f ? size.x / spriteSize.x : 1f,
                spriteSize.y > 0.001f ? size.y / spriteSize.y : 1f,
                1f);
            targetFrameRenderer.color = targetFrameColor;
        }

        if (targetLineRenderer != null)
        {
            targetLineRenderer.gameObject.SetActive(true);
            targetLineRenderer.startColor = targetLineColor;
            targetLineRenderer.endColor = targetLineColor;
            targetLineRenderer.startWidth = targetLineWidth;
            targetLineRenderer.endWidth = targetLineWidth;
            targetLineRenderer.sortingOrder = targetLineSortingOrder;
            Vector3 start = player.Transform.position;
            Vector3 end = targetBounds.center;
            start.z = 0f;
            end.z = 0f;
            targetLineRenderer.SetPosition(0, start);
            targetLineRenderer.SetPosition(1, end);
        }
    }

    private void EnsureTargetingVisuals()
    {
        Transform parent = worldRoot != null ? worldRoot : transform;
        if (targetFrameObject == null)
        {
            targetFrameObject = new GameObject("TargetFrame");
            targetFrameObject.transform.SetParent(parent, false);
            targetFrameRenderer = targetFrameObject.AddComponent<SpriteRenderer>();
            targetFrameRenderer.sortingOrder = 40;
        }

        if (targetFrameRenderer != null && targetFrameRenderer.sprite == null)
        {
            targetFrameRenderer.sprite = GetTargetFrameSprite();
        }

        if (targetLineRenderer == null)
        {
            GameObject lineObject = new GameObject("TargetLine");
            lineObject.transform.SetParent(parent, false);
            targetLineRenderer = lineObject.AddComponent<LineRenderer>();
            targetLineRenderer.positionCount = 2;
            targetLineRenderer.useWorldSpace = true;
            targetLineRenderer.alignment = LineAlignment.View;
            targetLineRenderer.textureMode = LineTextureMode.Stretch;
            targetLineRenderer.numCapVertices = 4;
            targetLineRenderer.sortingOrder = targetLineSortingOrder;
            targetLineRenderer.material = GetTargetingMaterial();
        }
    }

    private Sprite GetTargetFrameSprite()
    {
        if (runtimeTargetFrameSprite != null)
        {
            return runtimeTargetFrameSprite;
        }

        if (targetFrameSourceSprite == null || targetFrameSourceSprite.texture == null)
        {
            return null;
        }

        Texture2D texture = targetFrameSourceSprite.texture;
        runtimeTargetFrameSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            targetFrameSourceSprite.pixelsPerUnit);
        runtimeTargetFrameSprite.name = "TargetFrame_Runtime";
        return runtimeTargetFrameSprite;
    }

    private Material GetTargetingMaterial()
    {
        if (targetingMaterial != null)
        {
            return targetingMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        targetingMaterial = shader != null ? new Material(shader) : null;
        return targetingMaterial;
    }

    private void SetTargetingVisualsActive(bool active)
    {
        if (targetFrameObject != null)
        {
            targetFrameObject.SetActive(active);
        }
        if (targetLineRenderer != null)
        {
            targetLineRenderer.gameObject.SetActive(active);
        }
    }

    private static bool TryCalculateTargetBounds(EnemyShip enemy, out Bounds bounds)
    {
        bounds = default;
        if (enemy == null || enemy.Transform == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = enemy.Transform.GetComponentsInChildren<SpriteRenderer>(false);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || IsNonTargetBodyRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        bounds = new Bounds(enemy.Transform.position, Vector3.one);
        return true;
    }

    private static bool IsNonTargetBodyRenderer(SpriteRenderer renderer)
    {
        string lowerName = renderer.name.ToLowerInvariant();
        return lowerName.Contains("target") ||
               lowerName.Contains("aura") ||
               lowerName.Contains("shield") ||
               lowerName.Contains("thruster") ||
               lowerName.Contains("engine_fire") ||
               lowerName.Contains("enginefire");
    }

    private void UpdateHud()
    {
        combatLogText.text = string.Join("\n", combatLog);
        UpdateCombatLogScroll();
        if (pauseHudButtonView != null)
        {
            pauseHudButtonView.Rect.gameObject.SetActive(gameStarted && !gameOver && !gamePaused && !levelUpPending);
        }

        capacitorText.text = Localize("capacitor") + Mathf.RoundToInt(player.CapacitorPercent * 100f) + "%";
        if (capacitorValueText != null)
        {
            capacitorValueText.text = FormatBarValue(player.Capacitor, player.MaxCapacitor);
        }
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
            if (targetShieldValueText != null) targetShieldValueText.text = FormatBarValue(targetEnemy.Shield, targetEnemy.MaxShield);
            if (targetArmorValueText != null) targetArmorValueText.text = FormatBarValue(targetEnemy.Armor, targetEnemy.MaxArmor);
            if (targetHullValueText != null) targetHullValueText.text = FormatBarValue(targetEnemy.Hull, targetEnemy.MaxHull);
        }

        SetFillWidth(playerShieldFill.rectTransform, player.ShieldPercent, 180f);
        SetFillWidth(playerArmorFill.rectTransform, player.ArmorPercent, 180f);
        SetFillWidth(playerHullFill.rectTransform, player.HullPercent, 180f);
        float experiencePercent = player.ExperienceToNext <= 0 ? 0f : player.Experience / (float)player.ExperienceToNext;
        SetFillWidth(playerExperienceFill.rectTransform, experiencePercent, 180f);
        if (playerShieldValueText != null) playerShieldValueText.text = FormatBarValue(player.Shield, player.MaxShield);
        if (playerArmorValueText != null) playerArmorValueText.text = FormatBarValue(player.Armor, player.MaxArmor);
        if (playerHullValueText != null) playerHullValueText.text = FormatBarValue(player.Hull, player.MaxHull);
        if (playerExperienceValueText != null) playerExperienceValueText.text = player.Experience + " / " + player.ExperienceToNext;
        if (playerLevelBadgeText != null) playerLevelBadgeText.text = "LVL " + player.Level;
        SetFillHeight(capacitorFill.rectTransform, player.CapacitorPercent, 60f);

        UpdateEnemyRows();
        statusText.text = !gameStarted
            ? Localize("status_menu")
            : gameOver
                ? Localize("status_gameover")
                : levelUpPending
                    ? Localize("status_levelup")
                    : useVirtualJoystick ? Localize("status_play_mobile") : Localize("status_play_desktop");

        if (!gameStarted)
        {
            gateHintText.transform.parent.gameObject.SetActive(false);
        }

        if (equipmentUiController != null)
        {
            equipmentUiController.RefreshCooldowns(equipmentState);
        }

        UpdateUI();
    }

    private void UpdateCombatLogScroll()
    {
        if (combatLogText == null || combatLogContentRect == null)
        {
            return;
        }

        float preferredHeight = Mathf.Max(160f, combatLogText.preferredHeight + 8f);
        combatLogContentRect.sizeDelta = new Vector2(combatLogContentRect.sizeDelta.x, preferredHeight);
        SetAnchoredRect(combatLogText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -preferredHeight));

        if (combatLogShouldSnapToBottom && combatLogScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            combatLogScrollRect.verticalNormalizedPosition = 0f;
            combatLogShouldSnapToBottom = false;
        }
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
        while (combatLog.Count > 80)
        {
            combatLog.RemoveAt(0);
        }
        combatLogShouldSnapToBottom = true;
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

    private Image CreateBar(Transform parent, Vector2 anchoredPosition, Color fillColor)
    {
        return uiFactory.CreateBar(parent, squareSprite, anchoredPosition, fillColor);
    }

    private Image CreateLabeledBar(Transform parent, string label, Vector2 anchoredPosition, Color fillColor)
    {
        return uiFactory.CreateLabeledBar(parent, squareSprite, uiFont, label, anchoredPosition, fillColor);
    }

    private TMP_Text CreateBarValueText(Image fillImage, float width)
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

    private static string FormatBarValue(float current, float max)
    {
        return Mathf.RoundToInt(Mathf.Max(0f, current)) + " / " + Mathf.RoundToInt(Mathf.Max(0f, max));
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
