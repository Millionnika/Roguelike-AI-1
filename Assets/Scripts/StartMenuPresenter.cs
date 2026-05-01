using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum StartMenuPage
{
    Main,
    Hangar,
    Settings
}

[DisallowMultipleComponent]
public sealed class StartMenuPresenter : MonoBehaviour
{
    [Header("Стартовое меню")]
    [Tooltip("Корневой объект стартового меню. Если не назначен, presenter создаст runtime-меню.")]
    [SerializeField] private GameObject startMenuObject;

    private GameObject mainMenuPanelObject;
    private GameObject hangarPanelObject;
    private GameObject settingsPanelObject;
    private readonly List<ShipCardView> shipCardViews = new List<ShipCardView>();
    private readonly UiButtonView[] fpsButtonViews = new UiButtonView[4];

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

    private ISpaceCombatUiFactory uiFactory;
    private Font uiFont;
    private Sprite squareSprite;
    private Func<string, string> localize;
    private Func<ShipDataSO, string> getShipRoleText;
    private Func<ShipDataSO, string> getShipDescriptionText;
    private int[] fpsOptions = Array.Empty<int>();
    private int activeShipCount;

    public Action OnContinueRequested;
    public Action OnNewGameRequested;
    public Action OnSettingsRequested;
    public Action OnExitRequested;
    public Action<int> OnShipSelected;
    public Action OnStartRunRequested;
    public Action<int> OnLanguageToggleRequested;
    public Action<int> OnFpsToggleRequested;
    public Action OnBackRequested;

    public StartMenuPage CurrentPage { get; private set; } = StartMenuPage.Main;
    public bool IsVisible => startMenuObject != null && startMenuObject.activeSelf;

    internal void Initialize(
        ISpaceCombatUiFactory uiFactory,
        Font uiFont,
        Sprite squareSprite,
        Func<string, string> localizeCallback,
        Func<ShipDataSO, string> roleTextCallback,
        Func<ShipDataSO, string> descriptionTextCallback,
        int[] fpsOptions)
    {
        this.uiFactory = uiFactory;
        this.uiFont = uiFont;
        this.squareSprite = squareSprite;
        localize = localizeCallback;
        getShipRoleText = roleTextCallback;
        getShipDescriptionText = descriptionTextCallback;
        this.fpsOptions = fpsOptions ?? Array.Empty<int>();
    }

    public void Bind(Transform uiRoot, IReadOnlyList<ShipDataSO> ships)
    {
        Transform root = uiRoot != null ? uiRoot.Find("StartMenu") : null;
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
        BindShipCards(hangar, ships);
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
        for (int i = 0; i < fpsOptions.Length && i < fpsButtonViews.Length; i++)
        {
            fpsButtonViews[i] = BindMenuButton(settingsBox != null ? settingsBox.Find("fps_" + fpsOptions[i]) : null, "fps_" + fpsOptions[i]);
        }

        settingsBackButtonView = BindMenuButton(settings != null ? settings.Find("settings_back") : null, "settings_back");
        SetPage(StartMenuPage.Main);
    }

    internal void Build(Transform parent, IReadOnlyList<ShipDataSO> ships)
    {
        if (startMenuObject != null || parent == null || uiFactory == null)
        {
            return;
        }

        startMenuObject = new GameObject("StartMenu", typeof(RectTransform));
        startMenuObject.transform.SetParent(parent, false);
        RectTransform startMenuRect = startMenuObject.GetComponent<RectTransform>();
        StretchToParent(startMenuRect);

        Image dim = CreateImage("Dimmer", startMenuObject.transform, new Color(0f, 0f, 0f, 0.62f));
        StretchToParent(dim.rectTransform);

        Image panel = CreateImage("Panel", startMenuObject.transform, new Color(0.04f, 0.08f, 0.12f, 0.96f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 620f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.2f, 0.42f, 0.58f, 1f));

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
        CreateHangarPanel(hangarPanelObject.transform, ships);
        CreateSettingsPanel(settingsPanelObject.transform);
        SetPage(StartMenuPage.Main);
    }

    public void Show()
    {
        if (startMenuObject != null)
        {
            startMenuObject.SetActive(true);
        }

        SetPage(StartMenuPage.Main);
    }

    public void Hide()
    {
        if (startMenuObject != null)
        {
            startMenuObject.SetActive(false);
        }
    }

    public void SetPage(StartMenuPage page)
    {
        CurrentPage = page;
        if (mainMenuPanelObject != null) mainMenuPanelObject.SetActive(page == StartMenuPage.Main);
        if (hangarPanelObject != null) hangarPanelObject.SetActive(page == StartMenuPage.Hangar);
        if (settingsPanelObject != null) settingsPanelObject.SetActive(page == StartMenuPage.Settings);
    }

    public void Refresh(
        IReadOnlyList<ShipDataSO> ships,
        int selectedShipIndex,
        bool canContinue,
        bool isRussian,
        int selectedFpsIndex,
        bool useVirtualJoystick)
    {
        activeShipCount = ships != null ? ships.Count : 0;
        RefreshTexts(canContinue, useVirtualJoystick);
        RefreshSettingsButtons(isRussian, selectedFpsIndex);
        UpdateVisuals(ships, selectedShipIndex, useVirtualJoystick);
    }

    public void TickInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame && (CurrentPage == StartMenuPage.Hangar || CurrentPage == StartMenuPage.Settings))
            {
                OnBackRequested?.Invoke();
                return;
            }

            if (CurrentPage == StartMenuPage.Hangar)
            {
                int hotkeyShipIndex = ReadShipHotkey(keyboard);
                if (hotkeyShipIndex >= 0 && hotkeyShipIndex < activeShipCount)
                {
                    OnShipSelected?.Invoke(hotkeyShipIndex);
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                {
                    OnStartRunRequested?.Invoke();
                }
            }
            else if (CurrentPage == StartMenuPage.Settings)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) OnLanguageToggleRequested?.Invoke(0);
                if (keyboard.digit2Key.wasPressedThisFrame) OnLanguageToggleRequested?.Invoke(1);
                if (keyboard.f1Key.wasPressedThisFrame) OnFpsToggleRequested?.Invoke(0);
                if (keyboard.f2Key.wasPressedThisFrame) OnFpsToggleRequested?.Invoke(1);
                if (keyboard.f3Key.wasPressedThisFrame) OnFpsToggleRequested?.Invoke(2);
                if (keyboard.f4Key.wasPressedThisFrame) OnFpsToggleRequested?.Invoke(3);
            }
        }

        Vector2 position;
        if (!TryGetPrimaryPointerDown(out position))
        {
            return;
        }

        if (CurrentPage == StartMenuPage.Main)
        {
            if (IsButtonClicked(continueButtonView, position)) OnContinueRequested?.Invoke();
            else if (IsButtonClicked(newGameButtonView, position)) OnNewGameRequested?.Invoke();
            else if (IsButtonClicked(settingsMenuButtonView, position)) OnSettingsRequested?.Invoke();
            else if (IsButtonClicked(exitButtonView, position)) OnExitRequested?.Invoke();
        }
        else if (CurrentPage == StartMenuPage.Hangar)
        {
            for (int i = 0; i < shipCardViews.Count; i++)
            {
                if (shipCardViews[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(shipCardViews[i].Rect, position, null))
                {
                    OnShipSelected?.Invoke(i);
                    return;
                }
            }

            if (startButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(startButtonRect, position, null))
            {
                OnStartRunRequested?.Invoke();
            }
            else if (IsButtonClicked(hangarBackButtonView, position))
            {
                OnBackRequested?.Invoke();
            }
        }
        else if (CurrentPage == StartMenuPage.Settings)
        {
            if (IsButtonClicked(languageRuButtonView, position)) OnLanguageToggleRequested?.Invoke(0);
            else if (IsButtonClicked(languageEngButtonView, position)) OnLanguageToggleRequested?.Invoke(1);
            else
            {
                for (int i = 0; i < fpsButtonViews.Length; i++)
                {
                    if (IsButtonClicked(fpsButtonViews[i], position))
                    {
                        OnFpsToggleRequested?.Invoke(i);
                        return;
                    }
                }

                if (IsButtonClicked(settingsBackButtonView, position))
                {
                    OnBackRequested?.Invoke();
                }
            }
        }
    }

    private void RefreshTexts(bool canContinue, bool useVirtualJoystick)
    {
        if (mainMenuTitleText != null) mainMenuTitleText.text = Localize("main_title");
        if (mainMenuSubtitleText != null) mainMenuSubtitleText.text = Localize("main_subtitle");
        if (continueButtonView != null)
        {
            continueButtonView.Label.text = Localize("menu_continue");
            continueButtonView.Rect.gameObject.SetActive(canContinue);
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
        for (int i = 0; i < fpsButtonViews.Length && i < fpsOptions.Length; i++)
        {
            if (fpsButtonViews[i] != null)
            {
                fpsButtonViews[i].Label.text = fpsOptions[i].ToString();
            }
        }
    }

    private void RefreshSettingsButtons(bool isRussian, int selectedFpsIndex)
    {
        UpdateButtonState(languageRuButtonView, isRussian, new Color(0.45f, 0.72f, 1f, 1f));
        UpdateButtonState(languageEngButtonView, !isRussian, new Color(0.45f, 0.72f, 1f, 1f));
        for (int i = 0; i < fpsButtonViews.Length; i++)
        {
            UpdateButtonState(fpsButtonViews[i], i == selectedFpsIndex, new Color(1f, 0.7f, 0.36f, 1f));
        }
    }

    private void UpdateVisuals(IReadOnlyList<ShipDataSO> ships, int selectedShipIndex, bool useVirtualJoystick)
    {
        if (ships == null || ships.Count == 0 || selectedShipIndex < 0 || selectedShipIndex >= ships.Count)
        {
            return;
        }

        ShipDataSO ship = ships[selectedShipIndex];
        if (ship == null)
        {
            return;
        }

        if (startMenuShipNameText != null)
        {
            startMenuShipNameText.text = ship.displayName;
            if (startMenuRoleText != null) startMenuRoleText.text = getShipRoleText != null ? getShipRoleText(ship) : string.Empty;
            if (startMenuDescriptionText != null) startMenuDescriptionText.text = getShipDescriptionText != null ? getShipDescriptionText(ship) : string.Empty;
            if (startMenuStatsText != null)
            {
                startMenuStatsText.text =
                    Localize("stat_speed") + ": " + ship.maxSpeed.ToString("0.0") +
                    "    " + Localize("stat_shield") + ": " + Mathf.RoundToInt(ship.maxShield) +
                    "    " + Localize("stat_armor") + ": " + Mathf.RoundToInt(ship.maxArmor) +
                    "    " + Localize("stat_hull") + ": " + Mathf.RoundToInt(ship.maxHull) +
                    "\n" + Localize("stat_capacitor") + ": " + Mathf.RoundToInt(ship.capacitor) +
                    "    " + Localize("stat_recharge") + ": " + ship.capacitorRechargeTime.ToString("0") + "s" +
                    "    " + Localize("stat_weapon_slots") + ": " + Mathf.Max(0, ship.weaponSlotCount) +
                    "    " + Localize("stat_module_slots") + ": " + Mathf.Max(0, ship.moduleSlotCount);
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

        int cardCount = Mathf.Min(shipCardViews.Count, ships.Count);
        for (int i = 0; i < cardCount; i++)
        {
            ShipDataSO cardShip = ships[i];
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
                string role = getShipRoleText != null ? getShipRoleText(cardShip) : string.Empty;
                shipCardViews[i].Title.text = cardShip.displayName + "\n<size=16>" + role + "</size>";
                shipCardViews[i].Title.color = isSelected ? cardShip.accentColor : Color.white;
            }
            if (shipCardViews[i].Stats != null)
            {
                shipCardViews[i].Stats.text =
                    Localize("stat_shield") + " " + Mathf.RoundToInt(cardShip.maxShield) +
                    "  " + Localize("stat_armor") + " " + Mathf.RoundToInt(cardShip.maxArmor) +
                    "\n" + Localize("stat_speed") + " " + cardShip.maxSpeed.ToString("0.0") +
                    "  " + Localize("stat_guns") + " " + Mathf.Max(0, cardShip.weaponSlotCount);
            }
        }

        if (startMenuHintText != null)
        {
            startMenuHintText.text = useVirtualJoystick ? Localize("hangar_hint_mobile") : Localize("hangar_hint_desktop");
        }
    }

    private void BindShipCards(Transform hangar, IReadOnlyList<ShipDataSO> ships)
    {
        shipCardViews.Clear();
        Transform cardsRoot = hangar != null ? hangar.Find("Cards") : null;
        int shipCount = ships != null ? ships.Count : 0;
        for (int i = 0; i < shipCount; i++)
        {
            Transform card = cardsRoot != null ? cardsRoot.Find("ShipCard_" + i) : null;
            if (card == null)
            {
                continue;
            }

            EnsureButton(card);
            EnsureButtonScaleAnimator(card.gameObject);
            shipCardViews.Add(new ShipCardView
            {
                Rect = card.GetComponent<RectTransform>(),
                Background = card.GetComponent<Image>(),
                Title = FindText(card, "Title"),
                Stats = FindText(card, "Stats")
            });
        }
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

    private void CreateHangarPanel(Transform parent, IReadOnlyList<ShipDataSO> ships)
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
        int shipCount = ships != null ? ships.Count : 0;
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
        uiFactory.AddOutline(infoPanel.gameObject, new Color(0.16f, 0.34f, 0.48f, 1f));

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
        uiFactory.AddOutline(startButtonImage.gameObject, new Color(0.52f, 0.82f, 1f, 1f));
        startButtonImage.gameObject.AddComponent<Button>();
        EnsureButtonScaleAnimator(startButtonImage.gameObject);

        startButtonText = CreateText("Label", startButtonImage.transform, string.Empty, 20, FontStyle.Bold, Color.white);
        startButtonText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(startButtonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

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
        uiFactory.AddOutline(settingsBox.gameObject, new Color(0.16f, 0.34f, 0.48f, 1f));

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

        for (int i = 0; i < fpsOptions.Length && i < fpsButtonViews.Length; i++)
        {
            UiButtonView button = CreateMenuButton(settingsBox.transform, "fps_" + fpsOptions[i], new Vector2(0f, 1f), new Vector2(28f + i * 164f, -240f), new Vector2(140f, 50f));
            button.Rect.anchorMax = new Vector2(0f, 1f);
            button.Rect.pivot = new Vector2(0f, 1f);
            fpsButtonViews[i] = button;
        }

        settingsBackButtonView = CreateMenuButton(parent, "settings_back", new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(240f, 54f));
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
        uiFactory.AddOutline(background.gameObject, new Color(0.14f, 0.28f, 0.38f, 1f));
        background.gameObject.AddComponent<Button>();
        EnsureButtonScaleAnimator(background.gameObject);

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
        uiFactory.AddOutline(buttonImage.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));
        EnsureButtonScaleAnimator(buttonImage.gameObject);

        TMP_Text label = CreateText("Label", buttonImage.transform, id, 20, FontStyle.Bold, Color.white);
        label.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return new UiButtonView
        {
            Id = id,
            Rect = rect,
            Background = buttonImage,
            Label = label
        };
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

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        return uiFactory.CreateImage(objectName, parent, squareSprite, color);
    }

    private TMP_Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle fontStyle, Color color)
    {
        return uiFactory.CreateText(objectName, parent, uiFont, text, fontSize, fontStyle, color);
    }

    private void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        uiFactory.SetAnchoredRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }

    private static void UpdateButtonState(UiButtonView button, bool active, Color accent)
    {
        if (button == null)
        {
            return;
        }

        button.Background.color = active ? Color.Lerp(new Color(0.08f, 0.16f, 0.22f, 1f), accent, 0.55f) : new Color(0.08f, 0.16f, 0.22f, 0.98f);
        button.Label.color = active ? Color.white : new Color(0.88f, 0.94f, 1f);
    }

    private static bool IsButtonClicked(UiButtonView button, Vector2 screenPosition)
    {
        return button != null && button.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(button.Rect, screenPosition, null);
    }

    private static void EnsureButton(Transform target)
    {
        if (target != null && target.GetComponent<Button>() == null)
        {
            target.gameObject.AddComponent<Button>();
        }
    }

    private static void EnsureButtonScaleAnimator(GameObject buttonObject)
    {
        if (buttonObject != null && buttonObject.GetComponent<UIButtonScaleAnimator>() == null)
        {
            buttonObject.AddComponent<UIButtonScaleAnimator>();
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

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static int ReadShipHotkey(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return -1;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame) return 4;
        if (keyboard.digit6Key.wasPressedThisFrame) return 5;
        return -1;
    }

    private static bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var touch = touchscreen.touches[i];
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }
}
