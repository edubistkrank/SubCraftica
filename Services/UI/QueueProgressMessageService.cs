using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace SubCraftica.Services.UI;

/// <summary>
/// Renders persistent per-queue-item progress labels on ErrorMessage's own canvas.
/// Our labels are pinned at the top of the message area; vanilla messages are pushed
/// down dynamically by adjusting ErrorMessage.main.offset.y.
/// </summary>
internal sealed class QueueProgressMessageService
{
    private static readonly FieldInfo FiMain = typeof(ErrorMessage)
        .GetField("main", BindingFlags.NonPublic | BindingFlags.Static);

    // Ordered list of active queue entries (preserves display order)
    private readonly List<QueueEntry> entries = new List<QueueEntry>();

    private const float LineHeight = 28f;
    private const float BottomPadding = 18f; // extra gap between our lines and vanilla messages
    private float vanillaOffsetY = -1f; // cached on first use

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a pending queue entry showing 0/total immediately.
    /// For total == 1 no progress line is needed (there is nothing to track).
    /// Safe to call even if the entry already exists (updates total if different).
    /// </summary>
    public void RegisterPending(TechType techType, int total)
    {
        if (techType == TechType.None || total <= 0)
            return;

        var existing = FindEntry(techType);
        if (existing != null)
        {
            // Already registered — update total in case a new batch was added
            existing.Total = total;
            if (existing.Label != null)
                existing.Label.text = BuildText(techType, existing.Current, total);
            return;
        }

        var label = TryCreateLabel(BuildText(techType, 0, total));
        if (label == null)
            return;

        entries.Add(new QueueEntry(techType, 0, total, label));
        RelayoutAll();
    }

    /// <summary>
    /// Updates (or creates if missing) the progress line for an active craft.
    /// </summary>
    public void SetProgress(TechType techType, int current, int total)
    {
        if (techType == TechType.None || total <= 0 || current <= 0)
            return;

        var existing = FindEntry(techType);
        if (existing != null)
        {
            existing.Current = current;
            existing.Total = total;
            if (existing.Label != null)
                existing.Label.text = BuildText(techType, current, total);
        }
        else
        {
            // Entry not pre-registered (first item in queue — never went through RegisterPending).
            // Insert at index 0 so the active craft always appears above pending items.
            var label = TryCreateLabel(BuildText(techType, current, total));
            if (label == null)
                return;

            entries.Insert(0, new QueueEntry(techType, current, total, label));
            RelayoutAll();
        }
    }

    public void RemoveProgress(TechType techType)
    {
        var entry = FindEntry(techType);
        if (entry == null)
            return;

        entries.Remove(entry);

        if (entry.Label != null)
            Object.Destroy(entry.Label.gameObject);

        RelayoutAll();
    }

    public void Clear()
    {
        foreach (var e in entries)
        {
            if (e.Label != null)
                Object.Destroy(e.Label.gameObject);
        }
        entries.Clear();
        RelayoutAll();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void RelayoutAll()
    {
        var em = GetMain();
        if (em == null)
            return;

        // Cache vanilla's original offset.y on first use
        if (vanillaOffsetY < 0f)
            vanillaOffsetY = em.offset.y;

        var rect = em.messageCanvas.rect;

        // Position each of our labels in the top-left of the message area
        for (var i = 0; i < entries.Count; i++)
        {
            var label = entries[i].Label;
            if (label == null)
                continue;

            label.rectTransform.localPosition = new Vector3(
                rect.x + em.offset.x,
                -rect.y - vanillaOffsetY - i * LineHeight,
                0f);
        }

        // Push vanilla messages down so they don't overlap our labels.
        // When no entries remain, restore the original offset.
        var newOffsetY = entries.Count > 0
            ? vanillaOffsetY + entries.Count * LineHeight + BottomPadding
            : vanillaOffsetY;
        em.offset = new Vector2(em.offset.x, newOffsetY);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextMeshProUGUI TryCreateLabel(string text)
    {
        var em = GetMain();
        if (em == null || em.prefabMessage == null || em.messageCanvas == null)
            return null;

        var go = Object.Instantiate(em.prefabMessage);
        go.transform.SetParent(em.messageCanvas, false);
        go.SetActive(true);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = text;

        return tmp;
    }

    private QueueEntry FindEntry(TechType techType)
    {
        foreach (var e in entries)
        {
            if (e.TechType == techType)
                return e;
        }
        return null;
    }

    private static ErrorMessage GetMain() => FiMain?.GetValue(null) as ErrorMessage;

    private static string BuildText(TechType techType, int current, int total)
    {
        var name = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(techType, false))
            : TechTypeExtensions.AsString(techType, false);
        return $"{name} ({current}/{total})";
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class QueueEntry
    {
        public TechType TechType { get; }
        public int Current { get; set; }
        public int Total { get; set; }
        public TextMeshProUGUI Label { get; }

        public QueueEntry(TechType techType, int current, int total, TextMeshProUGUI label)
        {
            TechType = techType;
            Current = current;
            Total = total;
            Label = label;
        }
    }
}
