using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Resources;
using SubCraftica.Services.UI;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(TooltipFactory), nameof(TooltipFactory.CraftRecipe))]
internal static class TooltipFactoryCraftRecipePatch
{
    private const string InputIconResolveWarningKey = "TooltipFactoryCraftRecipePatch.ResolveInputIcon";

    private static readonly MethodInfo AppendDisplayTextMethod =
        typeof(GameInput).GetMethod("AppendDisplayText",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(StringBuilder), typeof(string) }, null);

    private static string _middleMouseIconCache = null;
    private static string _scrollUpIconCache = null;
    private static string _scrollDownIconCache = null;

    private static string GetMiddleMouseIcon()
    {
        if (_middleMouseIconCache != null)
            return _middleMouseIconCache;

        _middleMouseIconCache = ResolveInputIcon(new[] { "<Mouse>/middleButton" });
        return _middleMouseIconCache;
    }

    private static string GetScrollUpIcon()
    {
        if (_scrollUpIconCache != null)
            return _scrollUpIconCache;

        _scrollUpIconCache = ResolveInputIcon(new[]
        {
            "<Mouse>/scroll/up",
            "<Mouse>/scrollUp"
        });

        return _scrollUpIconCache;
    }

    private static string GetScrollDownIcon()
    {
        if (_scrollDownIconCache != null)
            return _scrollDownIconCache;

        _scrollDownIconCache = ResolveInputIcon(new[]
        {
            "<Mouse>/scroll/down",
            "<Mouse>/scrollDown"
        });

        return _scrollDownIconCache;
    }

    private static string ResolveInputIcon(string[] inputPaths)
    {
        if (inputPaths == null || inputPaths.Length == 0)
        {
            return string.Empty;
        }

        for (var i = 0; i < inputPaths.Length; i++)
        {
            try
            {
                var sb = new StringBuilder();
                AppendDisplayTextMethod?.Invoke(null, new object[] { inputPaths[i], sb, "#ADF8FFFF" });
                var result = sb.ToString();
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                StorageCompatLogger.LogCompatibilityWarningOnce(InputIconResolveWarningKey, $"Could not resolve input icon for '{inputPaths[i]}': {ex.Message}");
            }
        }

        return string.Empty;
    }

    [HarmonyPostfix]
    private static void Postfix(TechType techType, bool locked, TooltipData data)
    {
        if (Plugin.Services?.Quantity == null || data == null)
            return;

        var trackedAmount = Plugin.Services.Quantity.GetCurrentAmount(techType);
        RecipeOwnedIngredientsTooltipService.Track(techType, locked, trackedAmount);

        if (locked)
            return;

        if (!CrafterLogic.IsCraftRecipeFulfilled(techType))
            return;

        var unlockState = KnownTech.GetTechUnlockState(techType);
        if (unlockState.ToString() != "Available")
            return;

        var amount = Plugin.Services.Quantity.UpdateWithScroll(techType);
        RecipeOwnedIngredientsTooltipService.Track(techType, locked, amount);

        var icon = GetMiddleMouseIcon();
        var scrollUpIcon = GetScrollUpIcon();
        var scrollDownIcon = GetScrollDownIcon();
        var adjustText = ModText.Get(ModText.Tooltip_AdjustAmount);

        var isGamepad = GameInput.IsPrimaryDeviceGamepad();
        var adjustLine = BuildAdjustLine(adjustText, isGamepad, icon, scrollUpIcon, scrollDownIcon);

        var label = ModText.Get(ModText.Tooltip_Amount);
        var craftAmount = TechData.GetCraftAmount(techType);
        if (craftAmount <= 0)
        {
            craftAmount = 1;
        }

        var amountLine = $"<align=center><size=20><color=#94DE00FF>{label} <size=20>x</size></color></size><size=28><color=#94DE00FF>{amount}</color></size>";
        if (craftAmount > 1)
        {
            var totalLabel = ModText.Get(ModText.Tooltip_Total);
            amountLine += $"<size=20><color=#94DE00FF> ({totalLabel} x{amount * craftAmount})</color></size>";
        }

        amountLine += "</align>";

        data.postfix.AppendLine($"\n{adjustLine}");
        data.postfix.AppendLine($"\n{amountLine}");
    }

    private static string BuildAdjustLine(string adjustText, bool isGamepad, string mouseMiddleIcon, string scrollUpIcon, string scrollDownIcon)
    {
        if (isGamepad)
        {
            var left = GameInput.FormatButton(GameInput.Button.UIAdjustLeft, false);
            var right = GameInput.FormatButton(GameInput.Button.UIAdjustRight, false);
            if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            {
                left = GameInput.FormatButton(GameInput.Button.UILeft, false);
                right = GameInput.FormatButton(GameInput.Button.UIRight, false);
            }

            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                return $"<size=20>{left} / {right} - <color=#00ffffff>{adjustText}</color></size>";
            }

            if (!string.IsNullOrWhiteSpace(left))
            {
                return $"<size=20>{left} - <color=#00ffffff>{adjustText}</color></size>";
            }

            if (!string.IsNullOrWhiteSpace(right))
            {
                return $"<size=20>{right} - <color=#00ffffff>{adjustText}</color></size>";
            }

            return $"<size=20><color=#00ffffff>{adjustText}</color></size>";
        }

        if (!string.IsNullOrEmpty(scrollUpIcon) && !string.IsNullOrEmpty(scrollDownIcon))
        {
            return string.IsNullOrEmpty(mouseMiddleIcon)
                ? $"<size=20><color=#00ffffff>{adjustText} ({scrollUpIcon} / {scrollDownIcon})</color></size>"
                : $"<size=20>{mouseMiddleIcon} - <color=#00ffffff>{adjustText} ({scrollUpIcon} / {scrollDownIcon})</color></size>";
        }

        return string.IsNullOrEmpty(mouseMiddleIcon)
            ? $"<size=20><color=#00ffffff>{adjustText}</color></size>"
            : $"<size=20>{mouseMiddleIcon} - <color=#00ffffff>{adjustText}</color></size>";
    }
}

[HarmonyPatch(typeof(TooltipFactory), nameof(TooltipFactory.BuilderItem))]
internal static class TooltipFactoryBuilderItemPatch
{
    [HarmonyPostfix]
    private static void Postfix(TechType techType, bool locked, TooltipData data)
    {
        if (data == null)
        {
            return;
        }

        RecipeOwnedIngredientsTooltipService.Track(techType, locked, 1);
    }
}

[HarmonyPatch(typeof(uGUI_BuilderMenu), nameof(uGUI_BuilderMenu.GetToolbarTooltip))]
internal static class uGUIBuilderMenuGetToolbarTooltipPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        RecipeOwnedIngredientsTooltipService.ResetTrack();
    }
}

[HarmonyPatch(typeof(uGUI_BuilderMenu), nameof(uGUI_BuilderMenu.OnPointerExit))]
internal static class uGUIBuilderMenuOnPointerExitPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RecipeOwnedIngredientsTooltipService.ResetTrack();
    }
}

[HarmonyPatch(typeof(TooltipFactory), nameof(TooltipFactory.CraftNode))]
internal static class TooltipFactoryCraftNodePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RecipeOwnedIngredientsTooltipService.ResetTrack();
    }
}

[HarmonyPatch(typeof(uGUI_Tooltip), nameof(uGUI_Tooltip.Clear))]
internal static class uGUITooltipClearPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RecipeOwnedIngredientsTooltipService.ResetTrack();
    }
}

[HarmonyPatch(typeof(uGUI_Tooltip), "UpdatePosition")]
internal static class uGUITooltipUpdatePositionPatch
{
    private static readonly Vector2 VanillaMargin = new Vector2(10f, 20f);

    [HarmonyPrefix]
    private static bool Prefix(uGUI_Tooltip __instance)
    {
        if (__instance == null || __instance.rectTransform == null)
        {
            return true;
        }

        if (!IsCraftingTooltipContext())
        {
            return true;
        }

        if (!RecipeOwnedIngredientsTooltipService.TryGetCombinedSizeForVanillaBounds(__instance, out var combinedWidth, out var combinedHeight))
        {
            return true;
        }

        if (!TryReadTransformData(__instance,
                out var worldToLocalMatrix,
                out var localToWorldMatrix,
                out var rotation,
                out var scale,
                out var rect,
                out var position,
                out var aimingPosition,
                out var aimingForward,
                out var scaleFactor))
        {
            return true;
        }

        var scaleMultiplier = Vector3.Dot(aimingForward, position - aimingPosition) * scaleFactor * uGUI_CanvasScaler.uiScale;
        __instance.rectTransform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
        __instance.rectTransform.rotation = rotation;

        var textPrefix = __instance.textPrefix;
        if (textPrefix != null)
        {
            textPrefix.SetScaleDirty();
        }

        var textPostfix = __instance.textPostfix;
        if (textPostfix != null)
        {
            textPostfix.SetScaleDirty();
        }

        var icons = GetPrivateField<System.Collections.Generic.List<uGUI_TooltipIcon>>(__instance, "icons");
        if (icons != null)
        {
            for (var i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                if (icon != null && icon.gameObject.activeSelf && icon.title != null)
                {
                    icon.title.SetScaleDirty();
                }
            }
        }

        var scaled = new Vector2(scaleMultiplier / scale.x, scaleMultiplier / scale.y);
        var combinedSize = Vector2.Scale(new Vector2(combinedWidth, combinedHeight), scaled);
        var vanillaSize = Vector2.Scale(new Vector2(__instance.rectTransform.rect.width, __instance.rectTransform.rect.height), scaled);
        var marginScaled = Vector2.Scale(VanillaMargin, scaled);

        var posLocal = worldToLocalMatrix.MultiplyPoint3x4(position);
        var anchored = new Vector3(0f, 0f, posLocal.z);

        var min = rect.position;
        var max = rect.position + rect.size;

        var overflowRight = Mathf.Max(0f, posLocal.x + marginScaled.x + combinedSize.x - max.x);
        var overflowLeft = -Mathf.Min(0f, posLocal.x - marginScaled.x - combinedSize.x - min.x);
        var overflowTop = Mathf.Max(0f, posLocal.y + marginScaled.y + combinedSize.y - max.y);
        var overflowBottom = -Mathf.Min(0f, posLocal.y - marginScaled.y - combinedSize.y - min.y);

        if (overflowRight > 0f)
        {
            anchored.x = overflowLeft > 0f
                ? (overflowRight > overflowLeft ? min.x : max.x - combinedSize.x)
                : posLocal.x - marginScaled.x - combinedSize.x;
        }
        else
        {
            anchored.x = posLocal.x + marginScaled.x;
        }

        if (overflowBottom > 0f)
        {
            if (overflowTop > 0f)
            {
                var vanillaMirroredY = posLocal.y + marginScaled.y + vanillaSize.y;
                var vanillaFitsAbove = vanillaMirroredY <= max.y;

                anchored.y = vanillaFitsAbove
                    ? vanillaMirroredY
                    : (overflowBottom > overflowTop ? max.y : min.y + combinedSize.y);
            }
            else
            {
                anchored.y = posLocal.y + marginScaled.y + vanillaSize.y;
            }
        }
        else
        {
            anchored.y = posLocal.y - marginScaled.y;
        }

        __instance.rectTransform.position = localToWorldMatrix.MultiplyPoint3x4(anchored);
        return false;
    }

    private static bool IsCraftingTooltipContext()
    {
        var menu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        if (menu == null)
        {
            return false;
        }

        return menu.gameObject != null && menu.gameObject.activeInHierarchy;
    }

    private static bool TryReadTransformData(
        uGUI_Tooltip tooltip,
        out Matrix4x4 worldToLocalMatrix,
        out Matrix4x4 localToWorldMatrix,
        out Quaternion rotation,
        out Vector3 scale,
        out Rect rect,
        out Vector3 position,
        out Vector3 aimingPosition,
        out Vector3 aimingForward,
        out float scaleFactor)
    {
        worldToLocalMatrix = default;
        localToWorldMatrix = default;
        rotation = default;
        scale = default;
        rect = default;
        position = default;
        aimingPosition = default;
        aimingForward = default;
        scaleFactor = tooltip.scaleFactor;

        position = GetPrivateField<Vector3>(tooltip, "position");
        aimingPosition = GetPrivateField<Vector3>(tooltip, "aimingPosition");
        aimingForward = GetPrivateField<Vector3>(tooltip, "aimingForward");
        worldToLocalMatrix = GetPrivateField<Matrix4x4>(tooltip, "worldToLocalMatrix");
        localToWorldMatrix = GetPrivateField<Matrix4x4>(tooltip, "localToWorldMatrix");
        rotation = GetPrivateField<Quaternion>(tooltip, "rotation");
        scale = GetPrivateField<Vector3>(tooltip, "scale");
        rect = GetPrivateField<Rect>(tooltip, "rect");

        if (scale.x == 0f)
        {
            scale.x = 1f;
        }

        if (scale.y == 0f)
        {
            scale.y = 1f;
        }

        return true;
    }

    private static T GetPrivateField<T>(uGUI_Tooltip tooltip, string name)
    {
        if (tooltip == null || string.IsNullOrEmpty(name))
        {
            return default;
        }

        var field = AccessTools.Field(typeof(uGUI_Tooltip), name);
        if (field == null)
        {
            return default;
        }

        var value = field.GetValue(tooltip);
        if (value is T typed)
        {
            return typed;
        }

        return default;
    }
}
