using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatLogPresenter : MonoBehaviour
{
    [Header("Журнал боя")]
    [Tooltip("Заголовок панели журнала боя. Если поле не назначено, заголовок просто не будет обновляться.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Текстовое поле, куда выводятся строки журнала боя. Если поле не назначено, сообщения будут храниться, но не будут видны в интерфейсе.")]
    [SerializeField] private TMP_Text logText;
    [Tooltip("ScrollRect панели журнала. Используется для автоматической прокрутки вниз после новой записи.")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("RectTransform содержимого ScrollRect. Нужен, чтобы высота текста корректно подстраивалась под количество строк.")]
    [SerializeField] private RectTransform contentRect;
    [Tooltip("Максимальное количество строк в журнале. Старые строки удаляются первыми, чтобы журнал не рос бесконечно. Рекомендуемый диапазон: 50-120.")]
    [SerializeField, Min(1)] private int maxLogEntries = 80;

    private readonly List<string> entries = new List<string>();
    private bool shouldSnapToBottom;

    private void OnValidate()
    {
        maxLogEntries = Mathf.Max(1, maxLogEntries);
    }

    public void Configure(TMP_Text newTitleText, TMP_Text newLogText, ScrollRect newScrollRect, RectTransform newContentRect)
    {
        titleText = newTitleText;
        logText = newLogText;
        scrollRect = newScrollRect;
        contentRect = newContentRect;
        ConfigureScrollRect();
        Refresh();
    }

    public void SetTitle(string title)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    public void Clear()
    {
        entries.Clear();
        shouldSnapToBottom = true;
        Refresh();
    }

    public void LogMessage(string message)
    {
        LogMessage(message, "info");
    }

    public void LogMessage(string message, string kind)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        entries.Add(GetPrefix(kind) + message);
        while (entries.Count > maxLogEntries)
        {
            entries.RemoveAt(0);
        }

        shouldSnapToBottom = true;
        Refresh();
    }

    public void Refresh()
    {
        if (logText == null)
        {
            return;
        }

        logText.text = string.Join("\n", entries);
        UpdateScroll();
    }

    private void ConfigureScrollRect()
    {
        if (scrollRect == null)
        {
            return;
        }

        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
    }

    private void UpdateScroll()
    {
        if (logText == null || contentRect == null)
        {
            return;
        }

        float preferredHeight = Mathf.Max(160f, logText.preferredHeight + 8f);
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, preferredHeight);
        SetAnchoredRect(logText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -preferredHeight));

        if (shouldSnapToBottom && scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            shouldSnapToBottom = false;
        }
    }

    private static string GetPrefix(string kind)
    {
        switch (kind)
        {
            case "hit":
                return "[HIT] ";
            case "miss":
                return "[MISS] ";
            case "critical":
                return "[ALERT] ";
            case "warning":
                return "[WARN] ";
            default:
                return "[INFO] ";
        }
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
