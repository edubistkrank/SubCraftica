using HarmonyLib;
using SubCraftica.Services.Localization;
using System.Reflection;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch]
internal static class uGUIInventoryStorageSurplusTogglePatch
{
    private const string ToggleObjectName = "SubCraftica_SurplusStorageToggle";
    private const string LabelObjectName = "SubCraftica_SurplusLabel";

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

        if (Plugin.Services.Config != null && !Plugin.Services.Config.EnableSurplusToggle.Value)
        {
            uGUIInventoryStorageToggleShared.SetVisibility(__instance, ToggleObjectName, LabelObjectName, false);
            return;
        }

        uGUIInventoryStorageToggleShared.EnsureToggle(
            __instance,
            ToggleObjectName,
            LabelObjectName,
            new Vector2(50f, -26f),
            ModText.Get(ModText.ToggleLabelPrefer),
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

        var preferred = Plugin.Services.StoragePreferredSurplus != null && Plugin.Services.StoragePreferredSurplus.IsPreferred(id);
        toggle.SetIsOnWithoutNotify(preferred);
    }

    private static void OnToggleChanged(bool isOn)
    {
        if (!uGUIInventoryStorageToggleShared.TryResolveActiveStorageId(out var id))
        {
            return;
        }

        Plugin.Services.StoragePreferredSurplus?.SetPreferred(id, isOn);
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

            tooltip.prefix.Append(ModText.Get(ModText.TogglePreferredForSurplus));
        }
    }
}