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

    private readonly List<QueueEntry> entries = new List<QueueEntry>();
    private QueueEntry activeEntry; // direct reference — no TechType search needed

    private const float LineHeight = 28f;
    private const float BottomPadding = 18f; // extra gap between our lines and vanilla messages
    private float vanillaOffsetY = -1f; // cached on first use

    // Active craft: warm amber; pending slots: muted steel blue
    private const string ColorActive  = "#D4A017";
    private const string ColorPending = "#5B8DB8";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a pending queue entry showing 0/total immediately.
    /// Always creates a new entry — duplicate TechTypes are allowed (separate queue slots).
    /// </summary>
    public void RegisterPending(TechType techType, int total)
    {
        if (techType == TechType.None || total <= 0)
            return;

        // Always create a new entry — duplicate TechTypes are allowed (separate queue slots).
        var label = TryCreateLabel(BuildText(techType, 0, total, false));
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

        if (activeEntry != null)
        {
            // Update the active entry in-place — regardless of TechType
            activeEntry.Current = current;
            activeEntry.Total = total;
            if (activeEntry.Label != null)
                activeEntry.Label.text = BuildText(techType, current, total, true);
            return;
        }

        // No active entry yet — promote the first pending entry of this type,
        // or create a new one if none exists.
        var pending = FindFirstPending(techType);
        if (pending != null)
        {
            pending.Current = current;
            pending.Total = total;
            if (pending.Label != null)
                pending.Label.text = BuildText(techType, current, total, true);
            // Move to position 0 so the active craft is always on top
            entries.Remove(pending);
            entries.Insert(0, pending);
            activeEntry = pending;
            RelayoutAll();
            return;
        }

        // Entry not pre-registered (first item, no pending line existed)
        var label = TryCreateLabel(BuildText(techType, current, total, true));
        if (label == null)
            return;

        var entry = new QueueEntry(techType, current, total, label);
        entries.Insert(0, entry);
        activeEntry = entry;
        RelayoutAll();
    }

    public void RemoveProgress(TechType techType)
    {
        var entry = activeEntry;
        if (entry == null)
            return;

        // Only remove when the slot is fully done.
        // Mid-slot continuations (e.g. 2/4 → 3/4) must not remove the line.
        if (entry.Current < entry.Total)
            return;

        activeEntry = null;
        entries.Remove(entry);

        if (entry.Label != null)
            Object.Destroy(entry.Label.gameObject);

        RelayoutAll();
    }

    public void Clear()
    {
        activeEntry = null;
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

    private QueueEntry FindFirstPending(TechType techType)
    {
        foreach (var e in entries)
        {
            if (e.TechType == techType)
                return e;
        }
        return null;
    }

    private static ErrorMessage GetMain() => FiMain?.GetValue(null) as ErrorMessage;

    private static string BuildText(TechType techType, int current, int total, bool isActive)
    {
        var name = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(techType, false))
            : TechTypeExtensions.AsString(techType, false);
        var color = isActive ? ColorActive : ColorPending;
        return $"<color={color}>{name} ({current}/{total})</color>";
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
