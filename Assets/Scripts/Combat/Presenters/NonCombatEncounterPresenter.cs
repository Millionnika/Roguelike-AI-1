using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NonCombatEncounterPresenter : MonoBehaviour
{
    [Header("Небоевая локация")]
    [Tooltip("Корневой объект панели небоевой локации. Включается при показе заглушки и выключается после завершения действия.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Заголовок панели. Показывает название локации и ее тип.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Основной текст панели. Показывает описание EncounterSO и временный эффект действия.")]
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Кнопка основного действия: ремонт, отдых, сбор ресурсов или продолжение.")]
    [SerializeField] private Button actionButton;
    [Tooltip("Текст на кнопке основного действия.")]
    [SerializeField] private TMP_Text actionButtonText;

    private Action onAction;

    public bool HasPanel => panelObject != null;
    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Configure(GameObject panel, TMP_Text title, TMP_Text body, Button button, TMP_Text buttonText)
    {
        panelObject = panel;
        titleText = title;
        bodyText = body;
        actionButton = button;
        actionButtonText = buttonText;
        Hide();
    }

    public void Show(EncounterSO encounter, string description, Action actionCallback)
    {
        onAction = actionCallback;

        if (titleText != null)
        {
            string encounterName = encounter != null && !string.IsNullOrWhiteSpace(encounter.displayName)
                ? encounter.displayName
                : encounter != null ? encounter.name : "Локация";
            LocationNodeType nodeType = encounter != null ? encounter.nodeType : LocationNodeType.Event;
            titleText.text = encounterName + " [" + GetNodeTypeDisplayName(nodeType) + "]";
        }

        if (bodyText != null)
        {
            bodyText.text = description ?? string.Empty;
        }

        if (actionButtonText != null)
        {
            actionButtonText.text = encounter != null ? GetActionText(encounter.nodeType) : "Продолжить";
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(InvokeAction);
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
        }

        onAction = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    public bool TryActivateAt(Vector2 screenPosition)
    {
        RectTransform rect = actionButton != null ? actionButton.GetComponent<RectTransform>() : null;
        if (rect == null || !rect.gameObject.activeSelf)
        {
            return false;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
        {
            return false;
        }

        InvokeAction();
        return true;
    }

    public void InvokeAction()
    {
        onAction?.Invoke();
    }

    private static string GetActionText(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Repair:
                return "Ремонт";
            case LocationNodeType.Rest:
                return "Отдохнуть";
            case LocationNodeType.Resource:
                return "Собрать";
            case LocationNodeType.Shop:
            case LocationNodeType.Event:
            default:
                return "Продолжить";
        }
    }

    private static string GetNodeTypeDisplayName(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Combat:
                return "Бой";
            case LocationNodeType.Elite:
                return "Элита";
            case LocationNodeType.Shop:
                return "Магазин";
            case LocationNodeType.Repair:
                return "Ремонт";
            case LocationNodeType.Event:
                return "Событие";
            case LocationNodeType.Rest:
                return "Отдых";
            case LocationNodeType.Resource:
                return "Ресурсы";
            case LocationNodeType.Boss:
                return "Босс";
            default:
                return nodeType.ToString();
        }
    }
}
