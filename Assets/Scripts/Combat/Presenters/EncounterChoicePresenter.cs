using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EncounterChoicePresenter : MonoBehaviour
{
    [Header("Выбор следующей локации")]
    [Tooltip("Корневой объект панели выбора следующей локации. Включается при показе вариантов и выключается при скрытии.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Текст заголовка панели выбора. Обычно показывает приглашение выбрать следующую локацию.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Текст описания панели выбора. Используется для краткого пояснения источника вариантов.")]
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Кнопки вариантов локаций. Используются первые 1-3 кнопки по количеству доступных EncounterSO.")]
    [SerializeField] private List<Button> choiceButtons = new List<Button>();
    [Tooltip("Текстовые подписи кнопок вариантов. Индекс подписи должен соответствовать индексу кнопки.")]
    [SerializeField] private List<TMP_Text> choiceLabels = new List<TMP_Text>();

    private readonly List<EncounterSO> currentChoices = new List<EncounterSO>();
    private Action<EncounterSO> onSelected;

    public bool HasPanel => panelObject != null;
    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Configure(GameObject panel, TMP_Text title, TMP_Text body, IReadOnlyList<Button> buttons, IReadOnlyList<TMP_Text> labels)
    {
        panelObject = panel;
        titleText = title;
        bodyText = body;
        choiceButtons.Clear();
        choiceLabels.Clear();

        if (buttons != null)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                choiceButtons.Add(buttons[i]);
            }
        }

        if (labels != null)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                choiceLabels.Add(labels[i]);
            }
        }

        Hide();
    }

    public void ShowChoices(IReadOnlyList<EncounterSO> choices, Action<EncounterSO> selectedCallback)
    {
        currentChoices.Clear();
        onSelected = selectedCallback;

        if (choices != null)
        {
            for (int i = 0; i < choices.Count && i < 3; i++)
            {
                if (choices[i] != null)
                {
                    currentChoices.Add(choices[i]);
                }
            }
        }

        if (currentChoices.Count == 0)
        {
            Hide();
            return;
        }

        if (titleText != null)
        {
            titleText.text = "Выберите следующую локацию";
        }

        if (bodyText != null)
        {
            bodyText.text = "Маршрут формируется из пула локаций. Если директор не настроен, используется тестовый список.";
        }

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            bool isActive = i < currentChoices.Count;
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(isActive);
            if (!isActive)
            {
                continue;
            }

            int choiceIndex = i;
            button.onClick.AddListener(() => SelectChoice(choiceIndex));

            TMP_Text label = i < choiceLabels.Count ? choiceLabels[i] : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                EncounterSO encounter = currentChoices[i];
                string encounterName = string.IsNullOrWhiteSpace(encounter.displayName) ? encounter.name : encounter.displayName;
                label.text = (i + 1) + ". " + encounterName + " [" + GetNodeTypeDisplayName(encounter.nodeType) + "]";
            }
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    public void Hide()
    {
        ClearButtonListeners();
        currentChoices.Clear();
        onSelected = null;

        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    public bool TrySelectAt(Vector2 screenPosition)
    {
        for (int i = 0; i < currentChoices.Count && i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect != null && rect.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
            {
                SelectChoice(i);
                return true;
            }
        }

        return false;
    }

    public bool SelectByIndex(int index)
    {
        if (index < 0 || index >= currentChoices.Count)
        {
            return false;
        }

        SelectChoice(index);
        return true;
    }

    private void SelectChoice(int index)
    {
        if (index < 0 || index >= currentChoices.Count)
        {
            return;
        }

        onSelected?.Invoke(currentChoices[index]);
    }

    private void ClearButtonListeners()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.RemoveAllListeners();
            }
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
