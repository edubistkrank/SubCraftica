using System;
using System.Collections.Generic;
using System.Reflection;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SubCraftica.Services.UI;

internal static class RecipeOwnedIngredientsTooltipService
{
    private const string RootName = "SubCraftica_OwnedIngredientsTooltip";
    private const string InventoryBadgeRootName = "SubCraftica_InventoryOwnedBadge";
    private const string InventoryBadgeTextName = "SubCraftica_InventoryOwnedBadgeText";
    private const string InventoryBadgeIconName = "SubCraftica_InventoryOwnedBadgeIcon";
    private const float IconSize = 48f;
    private const float NestedIconSize = 40f;
    private const int RootTextSize = 20;
    private const int NestedTextSize = 16;
    private const float RootPaddingX = 8f;
    private const float RootPaddingY = 8f;
    private const float NestedPaddingX = 10f;
    private const float NestedPaddingY = 0f;
    private const float PanelGap = 3f;
    private const float RootLayoutSpacing = 2f;
    private const float NestedLayoutSpacing = 1f;
    private const float BasicColumnSpacing = 2f;
    private const float BasicRowSpacing = 2f;
    private const float VanillaMarginX = 10f;
    private const float BackgroundCornerScale = 1f;
    private const int MaxNestedDepth = 4;
    private const float InventoryBadgeFontSize = 14f;
    private const float InventoryBadgeIconSize = 16f;
    private const float InventoryBadgeRootWidth = 26f;
    private const float InventoryBadgeRootHeight = 34f;
    private const float InventoryBadgeOffsetX = 15f;
    private const float InventoryBadgeOffsetY = 10f;
    private const float InventoryBadgeIconYOffset = 2f;
    private const float InventoryBadgeNumberGap = -1f;
    private const string InventoryBadgeSpriteName = "Pda";
    private static readonly Vector2 StabilizationParkingPosition = new Vector2(200000f, -200000f);

    private static readonly FieldInfo TooltipPrefabIconEntryField =
        typeof(uGUI_Tooltip).GetField("prefabIconEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo TooltipPositionField =
        typeof(uGUI_Tooltip).GetField("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo CraftingMenuIsOpenField =
        typeof(uGUI_CraftingMenu).GetField("isOpen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo PdaLogMappingField =
        typeof(PDALog).GetField("mapping", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo PdaLogEntryDataIconField =
        typeof(PDALog.EntryData).GetField("icon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static TechType _trackedTechType = TechType.None;
    private static bool _trackedLocked;
    private static int _trackedAmount = 1;
    private static bool _contentDirty = true;
    private static int _lastRenderedTooltipMode = int.MinValue;
    private static bool _needsStabilizationPass;
    private static int _dataVersion;

    private static GameObject _root;
    private static RectTransform _rootRect;
    private static RectTransform _contentRect;
    private static Image _backgroundImage;
    private static readonly Dictionary<string, List<IngredientNode>> MainNodesCache = new Dictionary<string, List<IngredientNode>>();
    private static readonly Stack<uGUI_TooltipIcon> IconPool = new Stack<uGUI_TooltipIcon>();

    private const int MaxMainNodesCacheEntries = 48;

    internal static void Track(TechType techType, bool locked, int amount = 1)
    {
        var normalizedAmount = Mathf.Max(1, amount);
        var changed = _trackedTechType != techType || _trackedLocked != locked || _trackedAmount != normalizedAmount;

        _trackedTechType = techType;
        _trackedLocked = locked;
        _trackedAmount = normalizedAmount;

        if (changed)
        {
            _contentDirty = true;
            _needsStabilizationPass = _root == null || !_root.activeSelf;
        }

        if (locked || techType == TechType.None)
        {
            Hide();
        }
    }

    internal static void ResetTrack()
    {
        _trackedTechType = TechType.None;
        _trackedLocked = false;
        _trackedAmount = 1;
        _contentDirty = true;
        _lastRenderedTooltipMode = int.MinValue;
        _needsStabilizationPass = false;
        Hide();
    }

    internal static void MarkDataDirty()
    {
        _dataVersion++;
        _contentDirty = true;

        if (MainNodesCache.Count > MaxMainNodesCacheEntries)
        {
            MainNodesCache.Clear();
        }
    }

    internal static void Prewarm()
    {
        var tooltip = GetTooltipInstance();
        if (tooltip == null || tooltip.gameObject == null)
        {
            return;
        }

        EnsureRoot(tooltip);
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    internal static void Hide()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    internal static void Update()
    {
        if (_trackedTechType == TechType.None || _trackedLocked)
        {
            Hide();
            return;
        }

        var tooltipMode = Plugin.Services?.Config != null
            ? Plugin.Services.Config.CraftingTooltipMode.Value
            : ModConfig.CraftingTooltipModeAdvanced;

        if (tooltipMode == ModConfig.CraftingTooltipModeDisabled)
        {
            Hide();
            return;
        }

        if (!IsSupportedTooltipContextActive())
        {
            ResetTrack();
            return;
        }

        var tooltip = GetTooltipInstance();
        if (tooltip == null || tooltip.gameObject == null || !tooltip.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        EnsureRoot(tooltip);
        if (_rootRect == null || _contentRect == null)
        {
            Hide();
            return;
        }

        if (_lastRenderedTooltipMode != tooltipMode)
        {
            _contentDirty = true;
        }

        if (_contentDirty || _contentRect.childCount == 0)
        {
            ClearContent();

            var nodes = BuildMainNodes(tooltipMode);
            if (nodes.Count == 0)
            {
                Hide();
                return;
            }

            var mainPanel = BuildPanel(_contentRect, nodes, false, 0, tooltipMode == ModConfig.CraftingTooltipModeBasic);
            if (mainPanel == null)
            {
                Hide();
                return;
            }

            mainPanel.anchoredPosition = Vector2.zero;
            RefreshPanelSize(mainPanel);

            _contentDirty = false;
            _lastRenderedTooltipMode = tooltipMode;

            if (_needsStabilizationPass)
            {
                _needsStabilizationPass = false;
                _contentDirty = true;

                _root.layer = tooltip.gameObject.layer;
                _rootRect.anchorMin = new Vector2(0f, 1f);
                _rootRect.anchorMax = new Vector2(0f, 1f);
                _rootRect.pivot = new Vector2(0f, 1f);
                _rootRect.anchoredPosition = StabilizationParkingPosition;
                _root.SetActive(true);

                return;
            }
        }

        PositionRootLeftOfVanilla(tooltip);

        _root.layer = tooltip.gameObject.layer;
        _root.SetActive(true);
    }

    private static bool IsSupportedTooltipContextActive()
    {
        var craftingMenu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        if (IsCraftingMenuOpen(craftingMenu))
        {
            return true;
        }

        return uGUI_BuilderMenu.IsOpen();
    }

    private static bool IsCraftingMenuOpen(uGUI_CraftingMenu menu)
    {
        if (menu == null || menu.gameObject == null || !menu.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (CraftingMenuIsOpenField == null)
        {
            return true;
        }

        var value = CraftingMenuIsOpenField.GetValue(menu);
        return value is bool open && open;
    }

    private static void EnsureRoot(uGUI_Tooltip tooltip)
    {
        if (_root != null)
        {
            return;
        }

        _root = new GameObject(RootName, typeof(RectTransform));
        _rootRect = _root.GetComponent<RectTransform>();
        _rootRect.SetParent(tooltip.rectTransform, false);
        _rootRect.anchorMin = new Vector2(0f, 1f);
        _rootRect.anchorMax = new Vector2(0f, 1f);
        _rootRect.pivot = new Vector2(0f, 1f);
        _rootRect.anchoredPosition = Vector2.zero;
        _rootRect.localScale = Vector3.one;

        var backgroundObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var backgroundRect = backgroundObj.GetComponent<RectTransform>();
        backgroundRect.SetParent(_rootRect, false);
        backgroundRect.anchorMin = new Vector2(0f, 0f);
        backgroundRect.anchorMax = new Vector2(1f, 1f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundRect.localScale = Vector3.one;

        _backgroundImage = backgroundObj.GetComponent<Image>();
        ApplyBackgroundVisualStyle(tooltip, _backgroundImage);

        var contentObj = new GameObject("Content", typeof(RectTransform));
        _contentRect = contentObj.GetComponent<RectTransform>();
        _contentRect.SetParent(_rootRect, false);
        _contentRect.anchorMin = new Vector2(0f, 1f);
        _contentRect.anchorMax = new Vector2(0f, 1f);
        _contentRect.pivot = new Vector2(0f, 1f);
        _contentRect.anchoredPosition = new Vector2(RootPaddingX, -RootPaddingY);
        _contentRect.localScale = Vector3.one;

        _root.SetActive(false);
    }

    private static void ClearContent()
    {
        if (_contentRect == null)
        {
            return;
        }

        for (var i = 0; i < _contentRect.childCount; i++)
        {
            var child = _contentRect.GetChild(i);
            if (child != null)
            {
                CollectIconsForPool(child);
            }
        }

        for (var i = _contentRect.childCount - 1; i >= 0; i--)
        {
            var child = _contentRect.GetChild(i);
            if (child != null)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private static void CollectIconsForPool(Transform root)
    {
        if (root == null)
        {
            return;
        }

        var icon = root.GetComponent<uGUI_TooltipIcon>();
        if (icon != null)
        {
            icon.gameObject.SetActive(false);
            icon.rectTransform.SetParent(_rootRect, false);
            IconPool.Push(icon);
        }

        for (var i = 0; i < root.childCount; i++)
        {
            CollectIconsForPool(root.GetChild(i));
        }
    }

    private static List<IngredientNode> BuildMainNodes(int tooltipMode)
    {
        var cacheKey = tooltipMode + ":" + (int)_trackedTechType + ":" + _trackedAmount + ":" + _dataVersion;
        if (MainNodesCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var result = new List<IngredientNode>();
        if (Plugin.Services == null)
        {
            return result;
        }

        var ingredients = TechData.GetIngredients(_trackedTechType);
        if (ingredients == null || ingredients.Count == 0)
        {
            return result;
        }

        var playerContainer = Inventory.main != null ? Inventory.main.container : null;
        var amount = Mathf.Max(1, _trackedAmount);

        if (tooltipMode == ModConfig.CraftingTooltipModeBasic)
        {
            var aggregate = new Dictionary<TechType, int>();
            for (var i = 0; i < ingredients.Count; i++)
            {
                var ingredient = ingredients[i];
                var required = Mathf.Max(0, ingredient.amount * amount);
                AggregateBasicRequirements(ingredient.techType, required, aggregate, new HashSet<TechType>());
            }

            foreach (var pair in aggregate)
            {
                if (pair.Value > 0)
                {
                    result.Add(new IngredientNode(pair.Key, pair.Value));
                }
            }

            MainNodesCache[cacheKey] = result;
            return result;
        }

        for (var i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            var required = Mathf.Max(0, ingredient.amount * amount);
            var node = new IngredientNode(ingredient.techType, required);

            var available = RecipeCraftabilityResolver.GetAvailableCount(ingredient.techType, playerContainer);
            var missing = Mathf.Max(0, required - available);
            if (missing > 0 && RecipeCraftabilityResolver.CanExpandSubrecipe(ingredient.techType))
            {
                RecipeCraftabilityResolver.ResolveNodeCraftability(node, missing, 1, MaxNestedDepth, playerContainer);
            }

            result.Add(node);
        }

        MainNodesCache[cacheKey] = result;

        return result;
    }

    private static void AggregateBasicRequirements(TechType techType, int amountNeeded, Dictionary<TechType, int> aggregate, HashSet<TechType> path)
    {
        if (amountNeeded <= 0)
        {
            return;
        }

        if (!path.Add(techType))
        {
            return;
        }

        if (RecipeCraftabilityResolver.CanExpandSubrecipe(techType))
        {
            var available = RecipeCraftabilityResolver.GetAvailableCount(techType, Inventory.main != null ? Inventory.main.container : null);
            var useExisting = Mathf.Min(available, amountNeeded);
            if (useExisting > 0)
            {
                AddAggregateAmount(aggregate, techType, useExisting);
            }

            var remaining = amountNeeded - useExisting;
            if (remaining > 0)
            {
                var recipe = TechData.GetIngredients(techType);
                if (recipe != null && recipe.Count > 0)
                {
                    var yield = TechData.GetCraftAmount(techType);
                    if (yield <= 0)
                    {
                        yield = 1;
                    }

                    var craftsNeeded = Mathf.CeilToInt(remaining / (float)yield);
                    for (var i = 0; i < recipe.Count; i++)
                    {
                        var ingredient = recipe[i];
                        AggregateBasicRequirements(ingredient.techType, ingredient.amount * craftsNeeded, aggregate, path);
                    }
                }
                else
                {
                    AddAggregateAmount(aggregate, techType, remaining);
                }
            }
        }
        else
        {
            AddAggregateAmount(aggregate, techType, amountNeeded);
        }

        path.Remove(techType);
    }

    private static void AddAggregateAmount(Dictionary<TechType, int> aggregate, TechType techType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!aggregate.ContainsKey(techType))
        {
            aggregate[techType] = 0;
        }

        aggregate[techType] += amount;
    }

    private static RectTransform BuildPanel(RectTransform parent, IList<IngredientNode> nodes, bool horizontal, int depth, bool basicMode)
    {
        if (parent == null || nodes == null || nodes.Count == 0)
        {
            return null;
        }

        var panelObj = new GameObject($"Panel_{depth}", typeof(RectTransform), typeof(Image));
        var panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.localScale = Vector3.one;

        var panelImage = panelObj.GetComponent<Image>();
        if (depth == 0)
        {
            panelImage.enabled = false;
            panelImage.raycastTarget = false;
        }
        else
        {
            ApplyBackgroundVisualStyle(GetTooltipInstance(), panelImage);
        }

        var contentObj = new GameObject($"PanelContent_{depth}", typeof(RectTransform));
        var contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.SetParent(panelRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        var panelPaddingX = depth == 0 ? 0f : NestedPaddingX;
        var panelPaddingY = depth == 0 ? 0f : NestedPaddingY;
        contentRect.anchoredPosition = new Vector2(panelPaddingX, -panelPaddingY);
        contentRect.localScale = Vector3.one;

        if (horizontal)
        {
            var layout = contentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = depth <= 0 ? RootLayoutSpacing : NestedLayoutSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = depth <= 0 ? RootLayoutSpacing : NestedLayoutSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var createdIcons = new List<uGUI_TooltipIcon>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var icon = CreateIcon(contentRect, node, depth);
            createdIcons.Add(icon);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        for (var i = 0; i < nodes.Count; i++)
        {
            var icon = createdIcons[i];
            var node = nodes[i];
            if (icon == null || node.Children == null || node.Children.Count == 0)
            {
                continue;
            }

            var childPanel = BuildPanel(panelRect, node.Children, true, depth + 1, basicMode);
            if (childPanel != null)
            {
                PositionChildPanelLeftOfIcon(panelRect, icon.rectTransform, childPanel, depth);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        var width = Mathf.Max(0f, LayoutUtility.GetPreferredWidth(contentRect)) + (panelPaddingX * 2f);
        var height = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(contentRect)) + (panelPaddingY * 2f);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (basicMode && depth == 0)
        {
            var tooltip = GetTooltipInstance();
            var vanillaHeight = tooltip != null && tooltip.rectTransform != null ? tooltip.rectTransform.rect.height : 0f;
            if (vanillaHeight > 1f && panelRect.rect.height > vanillaHeight)
            {
                ConvertMainPanelToColumns(contentRect, panelRect, vanillaHeight);
            }
        }

        return panelRect;
    }

    private static void ConvertMainPanelToColumns(RectTransform contentRect, RectTransform panelRect, float maxHeight)
    {
        if (contentRect == null || panelRect == null || maxHeight <= 1f)
        {
            return;
        }

        var layout = contentRect.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            UnityEngine.Object.Destroy(layout);
        }

        var fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            UnityEngine.Object.Destroy(fitter);
        }

        var icons = new List<RectTransform>();
        for (var i = 0; i < contentRect.childCount; i++)
        {
            var child = contentRect.GetChild(i) as RectTransform;
            if (child != null)
            {
                icons.Add(child);
            }
        }

        if (icons.Count == 0)
        {
            return;
        }

        var cellWidth = 0f;
        var cellHeight = 0f;
        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            cellWidth = Mathf.Max(cellWidth, icon.rect.width);
            cellHeight = Mathf.Max(cellHeight, icon.rect.height);
        }

        if (cellWidth <= 0.1f)
        {
            cellWidth = IconSize;
        }

        if (cellHeight <= 0.1f)
        {
            cellHeight = IconSize;
        }

        var innerMaxHeight = Mathf.Max(cellHeight, maxHeight - (RootPaddingY * 2f));
        var maxRows = Mathf.Max(1, Mathf.FloorToInt((innerMaxHeight + BasicRowSpacing) / (cellHeight + BasicRowSpacing)));
        var usedCols = Mathf.CeilToInt(icons.Count / (float)maxRows);
        if (usedCols <= 1)
        {
            return;
        }

        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = Vector2.zero;

        contentRect.anchorMin = new Vector2(1f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(1f, 1f);
        contentRect.anchoredPosition = new Vector2(-RootPaddingX, -RootPaddingY);

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            icon.SetParent(contentRect, false);
            icon.anchorMin = new Vector2(1f, 1f);
            icon.anchorMax = new Vector2(1f, 1f);
            icon.pivot = new Vector2(1f, 1f);

            var col = i / maxRows;
            var row = i % maxRows;
            var x = -(col * (cellWidth + BasicColumnSpacing));
            var y = -(row * (cellHeight + BasicRowSpacing));
            icon.anchoredPosition = new Vector2(x, y);
        }

        var usedRows = Mathf.Min(maxRows, icons.Count);
        var width = (usedCols * cellWidth) + ((usedCols - 1) * BasicColumnSpacing);
        var height = (usedRows * cellHeight) + ((usedRows - 1) * BasicRowSpacing);

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + (RootPaddingX * 2f));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + (RootPaddingY * 2f));
    }

    private static void PositionChildPanelLeftOfIcon(RectTransform panelRect, RectTransform iconRect, RectTransform childPanel, int parentDepth)
    {
        if (panelRect == null || iconRect == null || childPanel == null)
        {
            return;
        }

        var leftCenterY = iconRect.anchoredPosition.y - (iconRect.rect.height * 0.5f);

        childPanel.anchorMin = new Vector2(0f, 1f);
        childPanel.anchorMax = new Vector2(0f, 1f);
        childPanel.pivot = new Vector2(1f, 0.5f);

        var xOffset = -PanelGap;
        if (parentDepth == 0)
        {
            xOffset -= RootPaddingX;
        }

        childPanel.anchoredPosition = new Vector2(xOffset, leftCenterY);
        childPanel.localScale = Vector3.one;
    }

    private static uGUI_TooltipIcon CreateIcon(RectTransform parent, IngredientNode node, int depth)
    {
        var tooltip = GetTooltipInstance();
        var prefab = GetPrefabIconEntry(tooltip);
        if (prefab == null)
        {
            return null;
        }

        uGUI_TooltipIcon icon = null;
        while (IconPool.Count > 0 && icon == null)
        {
            icon = IconPool.Pop();
        }

        if (icon == null)
        {
            icon = UnityEngine.Object.Instantiate(prefab.gameObject).GetComponent<uGUI_TooltipIcon>();
        }

        icon.rectTransform.SetParent(parent, false);
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.localScale = Vector3.one;

        if (icon.layoutGroup != null)
        {
            icon.layoutGroup.childControlWidth = true;
            icon.layoutGroup.childControlHeight = true;
            icon.layoutGroup.childForceExpandWidth = false;
            icon.layoutGroup.childForceExpandHeight = false;
        }

        var iconSize = depth <= 0 ? IconSize : NestedIconSize;
        var textSize = depth <= 0 ? RootTextSize : NestedTextSize;
        icon.SetSize(iconSize, iconSize);

        var playerContainer = Inventory.main != null ? Inventory.main.container : null;
        var playerCount = RecipeCraftabilityResolver.GetPlayerCount(node.TechType, playerContainer);
        var storageCount = RecipeCraftabilityResolver.GetStorageCount(node.TechType, playerContainer);
        var total = playerCount + storageCount;
        var color = ResolveOwnedColorHex(node.Required, playerCount, storageCount, node.CraftableBySubingredients, node.CraftFromStorage);

        var text = $"<size={textSize}><color=#{color}>{total}</color><color=#FFFFFFFF> / {node.Required}</color></size>";

        icon.SetIcon(SpriteManager.Get(node.TechType));
        icon.SetText(text);
        ApplyInventoryOwnedBadge(icon, playerCount, depth);
        icon.gameObject.SetActive(true);
        return icon;
    }

    private static void ApplyInventoryOwnedBadge(uGUI_TooltipIcon icon, int playerCount, int depth)
    {
        if (icon?.icon == null || icon.icon.transform == null)
        {
            return;
        }

        var badgeSprite = GetInventoryBadgeSprite();
        var iconTransform = icon.icon.transform as RectTransform;
        if (iconTransform == null)
        {
            return;
        }

        var existingRoot = iconTransform.Find(InventoryBadgeRootName) as RectTransform;
        if (playerCount <= 0 || badgeSprite == null)
        {
            if (existingRoot != null)
            {
                existingRoot.gameObject.SetActive(false);
            }

            return;
        }

        if (existingRoot == null)
        {
            existingRoot = CreateInventoryOwnedBadge(iconTransform);
        }

        var textTransform = existingRoot.Find(InventoryBadgeTextName);
        var iconImageTransform = existingRoot.Find(InventoryBadgeIconName);
        var text = textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;
        var iconImage = iconImageTransform != null ? iconImageTransform.GetComponent<Image>() : null;
        if (text == null || iconImage == null)
        {
            return;
        }

        ConfigureInventoryOwnedBadge(existingRoot, text.rectTransform, iconImage.rectTransform, text, iconImage, icon, playerCount, depth);
        existingRoot.gameObject.SetActive(true);
    }

    private static RectTransform CreateInventoryOwnedBadge(RectTransform iconTransform)
    {
        var root = new GameObject(InventoryBadgeRootName, typeof(RectTransform));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(iconTransform, false);
        rootRect.localScale = Vector3.one;

        var textObject = new GameObject(InventoryBadgeTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rootRect, false);
        textRect.localScale = Vector3.one;

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        var iconObject = new GameObject(InventoryBadgeIconName, typeof(RectTransform), typeof(Image));
        var badgeIconRect = iconObject.GetComponent<RectTransform>();
        badgeIconRect.SetParent(rootRect, false);
        badgeIconRect.localScale = Vector3.one;

        var image = iconObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        return rootRect;
    }

    private static void ConfigureInventoryOwnedBadge(RectTransform rootRect, RectTransform textRect, RectTransform badgeIconRect, TextMeshProUGUI text, Image iconImage, uGUI_TooltipIcon icon, int playerCount, int depth)
    {
        var scale = depth > 0 ? NestedIconSize / IconSize : 1f;
        var rootWidth = InventoryBadgeRootWidth * scale;
        var rootHeight = InventoryBadgeRootHeight * scale;
        var iconSize = InventoryBadgeIconSize * scale;
        var fontSize = InventoryBadgeFontSize * scale;
        var centerX = -(rootWidth * 0.5f);

        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(InventoryBadgeOffsetX, InventoryBadgeOffsetY);
        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rootWidth);
        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rootHeight);

        badgeIconRect.anchorMin = new Vector2(1f, 1f);
        badgeIconRect.anchorMax = new Vector2(1f, 1f);
        badgeIconRect.pivot = new Vector2(0.5f, 1f);
        badgeIconRect.anchoredPosition = new Vector2(centerX, -InventoryBadgeIconYOffset * scale);
        badgeIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, iconSize);
        badgeIconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, iconSize);

        textRect.anchorMin = new Vector2(1f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(centerX, -((InventoryBadgeIconYOffset + InventoryBadgeIconSize + InventoryBadgeNumberGap) * scale));
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rootWidth + (4f * scale));
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(10f * scale, fontSize + (2f * scale)));

        text.text = playerCount.ToString();
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        if (icon.title != null)
        {
            text.font = icon.title.font;
            text.fontSharedMaterial = icon.title.fontSharedMaterial;
        }

        iconImage.sprite = GetInventoryBadgeSprite();
    }

    private static Sprite GetInventoryBadgeSprite()
    {
        var mapping = PdaLogMappingField?.GetValue(null) as System.Collections.IDictionary;
        if (mapping != null && PdaLogEntryDataIconField != null)
        {
            foreach (System.Collections.DictionaryEntry pair in mapping)
            {
                var entryData = pair.Value;
                var sprite = PdaLogEntryDataIconField.GetValue(entryData) as Sprite;
                if (sprite != null && string.Equals(sprite.name, InventoryBadgeSpriteName, StringComparison.Ordinal))
                {
                    return sprite;
                }
            }
        }

        return PDALog.GetIcon(null) ?? SpriteManager.defaultSprite;
    }

    private static void RefreshPanelSize(RectTransform mainPanel)
    {
        if (_rootRect == null || _contentRect == null || mainPanel == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(mainPanel);

        _contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, mainPanel.rect.width);
        _contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, mainPanel.rect.height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rootRect);

        var width = Mathf.Max(0f, mainPanel.rect.width) + (RootPaddingX * 2f);
        var height = Mathf.Max(0f, mainPanel.rect.height) + (RootPaddingY * 2f);
        _rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private static void PositionRootLeftOfVanilla(uGUI_Tooltip tooltip)
    {
        if (_rootRect == null || tooltip == null || tooltip.rectTransform == null)
        {
            return;
        }

        _rootRect.SetParent(tooltip.rectTransform, false);
        var mirroredAboveCursor = IsVanillaMirroredAboveCursor(tooltip);

        if (mirroredAboveCursor)
        {
            _rootRect.anchorMin = new Vector2(0f, 0f);
            _rootRect.anchorMax = new Vector2(0f, 0f);
            _rootRect.pivot = new Vector2(1f, 0f);
            _rootRect.anchoredPosition = new Vector2(-VanillaMarginX, 0f);
        }
        else
        {
            _rootRect.anchorMin = new Vector2(0f, 1f);
            _rootRect.anchorMax = new Vector2(0f, 1f);
            _rootRect.pivot = new Vector2(1f, 1f);
            _rootRect.anchoredPosition = new Vector2(-VanillaMarginX, 0f);
        }

        _rootRect.localScale = Vector3.one;
    }

    private static bool IsVanillaMirroredAboveCursor(uGUI_Tooltip tooltip)
    {
        if (tooltip == null || tooltip.rectTransform == null)
        {
            return false;
        }

        if (TooltipPositionField == null)
        {
            return false;
        }

        var value = TooltipPositionField.GetValue(tooltip);
        if (!(value is Vector3 pointerPosition))
        {
            return false;
        }

        var corners = new Vector3[4];
        tooltip.rectTransform.GetWorldCorners(corners);
        var tooltipBottomY = corners[0].y;
        return tooltipBottomY > pointerPosition.y;
    }

    internal static bool TryGetCombinedSizeForVanillaBounds(uGUI_Tooltip tooltip, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        return false;
    }

    private static void ApplyBackgroundVisualStyle(uGUI_Tooltip tooltip, Image target)
    {
        if (target == null)
        {
            return;
        }

        var source = FindVanillaBackgroundImage(tooltip);
        if (source == null)
        {
            target.type = Image.Type.Sliced;
            target.color = new Color(0.03f, 0.09f, 0.14f, 0.95f);
            target.raycastTarget = false;
            return;
        }

        target.sprite = source.sprite;
        target.overrideSprite = source.overrideSprite;
        target.material = source.material;
        target.type = source.type;
        target.fillCenter = source.fillCenter;
        target.preserveAspect = source.preserveAspect;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.color = source.color;
        target.raycastTarget = false;

        if (target.type == Image.Type.Simple && target.sprite != null)
        {
            target.type = Image.Type.Sliced;
        }

        if (target.type == Image.Type.Sliced)
        {
            target.pixelsPerUnitMultiplier = BackgroundCornerScale;
        }
    }

    private static Image FindVanillaBackgroundImage(uGUI_Tooltip tooltip)
    {
        if (tooltip == null)
        {
            return null;
        }

        var images = tooltip.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            if (_rootRect != null && image.transform == _rootRect)
            {
                continue;
            }

            var name = image.gameObject.name;
            if (!string.IsNullOrEmpty(name) && name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image != null && image.sprite != null)
            {
                return image;
            }
        }

        return null;
    }

    private static uGUI_Tooltip GetTooltipInstance()
    {
        if (uGUI.main == null)
        {
            return GetTooltipFromScene();
        }

        var tooltips = uGUI.main.GetComponentsInChildren<uGUI_Tooltip>(true);
        if (tooltips == null || tooltips.Length == 0)
        {
            return GetTooltipFromScene();
        }

        for (var i = 0; i < tooltips.Length; i++)
        {
            var tooltip = tooltips[i];
            if (tooltip != null && tooltip.gameObject != null && tooltip.gameObject.activeInHierarchy)
            {
                return tooltip;
            }
        }

        var fallback = tooltips[0];
        if (fallback != null)
        {
            return fallback;
        }

        return GetTooltipFromScene();
    }

    private static uGUI_Tooltip GetTooltipFromScene()
    {
        var all = UnityEngine.Resources.FindObjectsOfTypeAll<uGUI_Tooltip>();
        if (all == null || all.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < all.Length; i++)
        {
            var tooltip = all[i];
            if (tooltip != null && tooltip.gameObject != null && tooltip.gameObject.activeInHierarchy)
            {
                return tooltip;
            }
        }

        return all[0];
    }

    private static uGUI_TooltipIcon GetPrefabIconEntry(uGUI_Tooltip tooltip)
    {
        if (tooltip == null)
        {
            return null;
        }

        if (tooltip.prefabIconEntry != null)
        {
            return tooltip.prefabIconEntry;
        }

        if (TooltipPrefabIconEntryField == null)
        {
            var iconCanvas = tooltip.iconCanvas;
            if (iconCanvas != null)
            {
                return iconCanvas.GetComponentInChildren<uGUI_TooltipIcon>(true);
            }

            return null;
        }

        var prefab = TooltipPrefabIconEntryField.GetValue(tooltip) as uGUI_TooltipIcon;
        if (prefab != null)
        {
            return prefab;
        }

        var fallbackCanvas = tooltip.iconCanvas;
        return fallbackCanvas != null ? fallbackCanvas.GetComponentInChildren<uGUI_TooltipIcon>(true) : null;
    }

    private static bool CanExpandSubrecipe(TechType techType)
    {
        var services = Plugin.Services;
        if (services?.Config == null)
        {
            return false;
        }

        if (!services.Config.EnableAutoSubcraft.Value)
        {
            return false;
        }

        return CraftTree.IsCraftable(techType);
    }

    private static int GetAvailableCount(TechType techType, ItemsContainer playerContainer)
    {
        return GetPlayerCount(techType, playerContainer) + GetStorageCount(techType, playerContainer);
    }

    private static int GetPlayerCount(TechType techType, ItemsContainer playerContainer)
    {
        if (playerContainer != null && Plugin.Services?.StackingCount != null)
        {
            return Plugin.Services.StackingCount.GetContainerCount(playerContainer, techType);
        }

        if (playerContainer != null)
        {
            return playerContainer.GetCount(techType);
        }

        return 0;
    }

    private static int GetStorageCount(TechType techType, ItemsContainer playerContainer)
    {
        if (Plugin.Services?.NearbyStorage == null)
        {
            return 0;
        }

        return Plugin.Services.NearbyStorage.GetNearbyCount(techType, playerContainer);
    }

    private static string GetOwnedAmountColorHex(int required, int playerCount, int storageCount)
    {
        if (Plugin.Services?.Config == null)
        {
            return required <= playerCount + storageCount ? "94DE00FF" : "DF4026FF";
        }

        if (required <= playerCount || !GameModeUtils.RequiresIngredients())
        {
            return GetPresetColorHex(StorageIngredientColorPresets.Colors, Plugin.Services.Config.InventoryIngredientColorPreset.Value);
        }

        if (required <= playerCount + storageCount)
        {
            return GetPresetColorHex(StorageIngredientColorPresets.Colors, Plugin.Services.Config.StorageOnlyIngredientColorPreset.Value);
        }

        return GetPresetColorHex(MissingIngredientColorPresets.Colors, Plugin.Services.Config.MissingIngredientColorPreset.Value);
    }

    private static string ResolveOwnedColorHex(int required, int playerCount, int storageCount, bool craftableBySubingredients, bool craftFromStorage)
    {
        if (required > playerCount + storageCount)
        {
            if (craftableBySubingredients)
            {
                return craftFromStorage
                    ? GetPresetColorHex(StorageIngredientColorPresets.Colors, Plugin.Services.Config.StorageOnlyIngredientColorPreset.Value)
                    : GetPresetColorHex(StorageIngredientColorPresets.Colors, Plugin.Services.Config.InventoryIngredientColorPreset.Value);
            }
        }

        return GetOwnedAmountColorHex(required, playerCount, storageCount);
    }

    private static string GetPresetColorHex(Color[] presets, int index)
    {
        if (presets == null || presets.Length == 0)
        {
            return "FFFFFFFF";
        }

        if (index < 0 || index >= presets.Length)
        {
            index = 0;
        }

        return ColorHexUtility.ToHex(presets[index]);
    }
}
