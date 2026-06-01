using HarmonyLib;
using SubCraftica.Services.Resources;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace SubCraftica.Patches;

internal static class uGUIInventoryStorageToggleShared
{
    private const string BackgroundObjectName = "Background";
    private const string FillObjectName = "Fill";
    private const string CheckmarkObjectName = "Checkmark";

    private static readonly Color AccentYellow = new Color(1f, 0.8196f, 0f, 0.98f); // #FFD100
    private static readonly Color BaseBlue = new Color(0.498f, 0.863f, 1f, 0.94f); // #7FDCFF
    private static readonly Type InventoryTabType = AccessTools.TypeByName("uGUI_InventoryTab");
    private static readonly FieldInfo StorageLabelField = InventoryTabType != null
        ? AccessTools.Field(InventoryTabType, "storageLabel")
        : null;

    internal static MethodBase TargetMethod()
    {
        return InventoryTabType != null
            ? AccessTools.Method(InventoryTabType, "OnOpenPDA", new[] { typeof(PDATab), typeof(bool) })
            : null;
    }

    internal static void EnsureToggle(object tab, string toggleObjectName, string labelObjectName, Vector2 anchoredPosition, string labelText, UnityEngine.Events.UnityAction<bool> handler, Type tooltipType)
    {
        var labelTransform = GetStorageLabelTransform(tab);
        if (labelTransform == null)
        {
            return;
        }

        var existing = labelTransform.Find(toggleObjectName);
        if (existing != null)
        {
            EnsureLabel(labelTransform, labelObjectName, labelText, anchoredPosition);
            return;
        }

        var root = new GameObject(toggleObjectName, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(labelTransform, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(22f, 22f);

        if (tooltipType != null)
        {
            root.AddComponent(tooltipType);
        }

        var background = new GameObject(BackgroundObjectName, typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(20f, 20f);
        var bgImage = background.GetComponent<Image>();
        bgImage.color = AccentYellow;
        bgImage.raycastTarget = true;
        bgImage.sprite = null;
        bgImage.type = Image.Type.Simple;

        var fill = new GameObject(FillObjectName, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(16f, 16f);
        var fillImage = fill.GetComponent<Image>();
        fillImage.color = BaseBlue;
        fillImage.raycastTarget = true;

        var checkmark = new GameObject(CheckmarkObjectName, typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(fill.transform, false);
        var checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(10f, 10f);
        var checkImage = checkmark.GetComponent<Image>();
        checkImage.color = AccentYellow;
        checkImage.raycastTarget = false;
        checkImage.sprite = null;
        checkImage.type = Image.Type.Simple;

        var toggle = root.GetComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.targetGraphic = fillImage;
        toggle.graphic = checkImage;

        ApplyVisualStyle(root.transform);

        toggle.onValueChanged.AddListener(handler);

        EnsureLabel(labelTransform, labelObjectName, labelText, anchoredPosition);
    }

    internal static bool TryPrepareToggleState(object tab, string toggleObjectName, string labelObjectName, out Toggle toggle, out string storageId)
    {
        toggle = FindToggle(tab, toggleObjectName);
        storageId = null;
        if (toggle == null)
        {
            return false;
        }

        ApplyVisualStyle(toggle.transform);

        var hasStorage = TryResolveActiveStorageId(out storageId);
        SetVisibility(tab, toggleObjectName, labelObjectName, hasStorage);
        return hasStorage;
    }

    internal static Toggle FindToggle(object tab, string toggleObjectName)
    {
        var labelTransform = GetStorageLabelTransform(tab);
        if (labelTransform == null)
        {
            return null;
        }

        var rootTransform = labelTransform.Find(toggleObjectName);
        return rootTransform != null ? rootTransform.GetComponent<Toggle>() : null;
    }

    internal static void SetVisibility(object tab, string toggleObjectName, string labelObjectName, bool isVisible)
    {
        var toggle = FindToggle(tab, toggleObjectName);
        if (toggle != null && toggle.gameObject != null)
        {
            toggle.gameObject.SetActive(isVisible);
        }

        var labelTransform = GetStorageLabelTransform(tab);
        if (labelTransform == null)
        {
            return;
        }

        var label = labelTransform.Find(labelObjectName);
        if (label != null)
        {
            label.gameObject.SetActive(isVisible);
        }
    }

    internal static void ApplyVisualStyle(Transform toggleTransform)
    {
        if (toggleTransform == null)
        {
            return;
        }

        var background = toggleTransform.Find(BackgroundObjectName);
        if (background != null)
        {
            var fill = background.Find(FillObjectName);
            var fillImage = fill != null ? fill.GetComponent<Image>() : null;

            var toggle = toggleTransform.GetComponent<Toggle>();
            if (toggle != null && fillImage != null)
            {
                var colors = toggle.colors;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                colors.normalColor = BaseBlue;
                colors.highlightedColor = Color.Lerp(BaseBlue, Color.white, 0.08f);
                colors.pressedColor = Color.Lerp(BaseBlue, Color.black, 0.10f);
                colors.selectedColor = Color.Lerp(BaseBlue, AccentYellow, 0.10f);
                colors.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.5f);
                toggle.colors = colors;

                if (!toggle.isOn)
                {
                    fillImage.color = colors.normalColor;
                }
            }
        }

        var checkmark = toggleTransform.Find(BackgroundObjectName + "/" + CheckmarkObjectName);
        if (checkmark != null)
        {
            var checkImage = checkmark.GetComponent<Image>();
            if (checkImage != null)
            {
                checkImage.color = AccentYellow;
            }
        }
    }

    internal static bool TryResolveActiveStorageId(out string id)
    {
        id = null;

        if (Plugin.Services == null)
        {
            return false;
        }

        var activeContainer = GetActiveStorageContainer();
        if (activeContainer == null)
        {
            return false;
        }

        var owner = Plugin.Services.NearbyStorage.GetOwner(activeContainer);
        if (owner == null)
        {
            return false;
        }

        id = StorageIdentifierResolver.GetStorageId(owner);
        return !string.IsNullOrWhiteSpace(id);
    }

    private static Transform GetStorageLabelTransform(object tab)
    {
        if (tab == null || StorageLabelField == null)
        {
            return null;
        }

        var component = StorageLabelField.GetValue(tab) as Component;
        return component != null ? component.transform : null;
    }

    private static ItemsContainer GetActiveStorageContainer()
    {
        var inventory = Inventory.main;
        if (inventory == null || inventory.GetUsedStorageCount() <= 0)
        {
            return null;
        }

        return inventory.GetUsedStorage(0) as ItemsContainer;
    }

    private static void EnsureLabel(Transform labelTransform, string labelObjectName, string labelText, Vector2 togglePosition)
    {
        if (labelTransform == null)
        {
            return;
        }

        var existing = labelTransform.Find(labelObjectName);
        if (existing != null)
        {
            var existingText = existing.GetComponent<Text>();
            if (existingText != null)
            {
                existingText.text = labelText;
            }

            return;
        }

        var labelObj = new GameObject(labelObjectName, typeof(RectTransform), typeof(Text));
        labelObj.transform.SetParent(labelTransform, false);
        var rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(togglePosition.x + 26f, togglePosition.y);
        rect.sizeDelta = new Vector2(70f, 18f);

        var text = labelObj.GetComponent<Text>();
        text.text = labelText;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 12;
        text.color = BaseBlue;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
