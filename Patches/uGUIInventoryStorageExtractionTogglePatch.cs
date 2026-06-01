using HarmonyLib;
using SubCraftica.Services.Localization;
using System.Reflection;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch]
internal static class uGUIInventoryStorageExtractionTogglePatch
{
    private const string ToggleObjectName = "SubCraftica_ExcludeStorageToggle";
    private const string LabelObjectName = "SubCraftica_ExcludeLabel";

    private static MethodBase TargetMethod()
    {
        return uGUIInventoryStorageToggleShared.TargetMethod();
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return;
        }

        if (Plugin.Services.Config != null && !Plugin.Services.Config.EnableBlacklistToggle.Value)
        {
            uGUIInventoryStorageToggleShared.SetVisibility(__instance, ToggleObjectName, LabelObjectName, false);
            return;
        }

        uGUIInventoryStorageToggleShared.EnsureToggle(
            __instance,
            ToggleObjectName,
            LabelObjectName,
            new Vector2(50f, 0f),
            ModText.Get(ModText.ToggleLabelExclude),
            OnToggleChanged,
            typeof(TooltipTrigger));

        RefreshToggleState(__instance);
    }

    private static void RefreshToggleState(object tab)
    {
        if (!uGUIInventoryStorageToggleShared.TryPrepareToggleState(tab, ToggleObjectName, LabelObjectName, out var toggle, out var id))
        {
            return;
        }

        toggle.SetIsOnWithoutNotify(Plugin.Services.StorageExtractionExclusions.IsExcluded(id));
    }

    private static void OnToggleChanged(bool isOn)
    {
        if (!uGUIInventoryStorageToggleShared.TryResolveActiveStorageId(out var id))
        {
            return;
        }

        Plugin.Services.StorageExtractionExclusions.SetExcluded(id, isOn);
    }

    private sealed class TooltipTrigger : MonoBehaviour, ITooltip
    {
        public bool showTooltipOnDrag => false;

        public void GetTooltip(TooltipData tooltip)
        {
            if (tooltip == null)
            {
                return;
            }

            tooltip.prefix.Append(ModText.Get(ModText.ToggleExcludeFromExtraction));
        }
    }
}
