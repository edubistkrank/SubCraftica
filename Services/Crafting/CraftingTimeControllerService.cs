using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Configuration;
using UnityEngine;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftingTimeControllerService
{
    private const float InstantDuration = 0.01f;

    private static readonly FieldInfo CrafterLogicField = AccessTools.Field(typeof(GhostCrafter), "crafterLogic");
    private static readonly FieldInfo TimeCraftingEndField = AccessTools.Field(typeof(CrafterLogic), "timeCraftingEnd");
    private static readonly FieldInfo TimeCraftingBeginField = AccessTools.Field(typeof(CrafterLogic), "timeCraftingBegin");
    private static readonly FieldInfo FabricateSoundField = AccessTools.Field(typeof(Fabricator), "fabricateSound");
    private static readonly FieldInfo AnimatorField = AccessTools.Field(typeof(Fabricator), "animator");
    private static readonly FieldInfo LeftBeamField = AccessTools.Field(typeof(Fabricator), "leftBeam");
    private static readonly FieldInfo RightBeamField = AccessTools.Field(typeof(Fabricator), "rightBeam");
    private static readonly FieldInfo FabLightField = AccessTools.Field(typeof(Fabricator), "fabLight");

    private readonly HashSet<GhostCrafter> activeCrafters = new HashSet<GhostCrafter>();

    private static float GameNow => DayNightCycle.main != null ? DayNightCycle.main.timePassedAsFloat : Time.realtimeSinceStartup;

    public void OnCrafterCraft(Crafter crafter, ref float duration)
    {
        if (!IsInstantMode())
        {
            return;
        }

        if (crafter is GhostCrafter ghostCrafter)
        {
            activeCrafters.Add(ghostCrafter);
        }

        duration = InstantDuration;
    }

    public void OnCraftingBegin(GhostCrafter crafter)
    {
        if (!IsInstantMode() || crafter == null)
        {
            return;
        }

        var logic = CrafterLogicField?.GetValue(crafter) as CrafterLogic;
        if (logic == null)
        {
            return;
        }

        var now = GameNow;
        TimeCraftingBeginField?.SetValue(logic, now);
        TimeCraftingEndField?.SetValue(logic, now + InstantDuration);

        SetFabricatorEffects(crafter as Fabricator, false);
    }

    public void Update()
    {
        if (!IsInstantMode() || activeCrafters.Count == 0)
        {
            return;
        }

        var now = GameNow;
        foreach (var crafter in new List<GhostCrafter>(activeCrafters))
        {
            if (crafter == null)
            {
                activeCrafters.Remove(crafter);
                continue;
            }

            var logic = CrafterLogicField?.GetValue(crafter) as CrafterLogic;
            if (logic == null || !logic.inProgress)
            {
                activeCrafters.Remove(crafter);
                continue;
            }

            var end = (float?)TimeCraftingEndField?.GetValue(logic) ?? now;
            if (end - now > InstantDuration)
            {
                TimeCraftingBeginField?.SetValue(logic, now);
                TimeCraftingEndField?.SetValue(logic, now + InstantDuration);
            }

            SetFabricatorEffects(crafter as Fabricator, false);
        }
    }

    private static void SetFabricatorEffects(Fabricator fabricator, bool enabled)
    {
        if (fabricator == null)
        {
            return;
        }

        var sound = FabricateSoundField?.GetValue(fabricator);
        if (sound != null)
        {
            var method = AccessTools.Method(sound.GetType(), enabled ? "Play" : "Stop");
            method?.Invoke(sound, null);
        }

        var animator = AnimatorField?.GetValue(fabricator);
        if (animator != null)
        {
            var setBoolMethod = AccessTools.Method(animator.GetType(), "SetBool", new[] { typeof(string), typeof(bool) });
            setBoolMethod?.Invoke(animator, new object[] { "fabricating", enabled });
            setBoolMethod?.Invoke(animator, new object[] { "fabricating_slow", false });
        }

        var leftBeam = LeftBeamField?.GetValue(fabricator) as GameObject;
        if (leftBeam != null)
        {
            leftBeam.SetActive(enabled);
        }

        var rightBeam = RightBeamField?.GetValue(fabricator) as GameObject;
        if (rightBeam != null)
        {
            rightBeam.SetActive(enabled);
        }

        var fabLight = FabLightField?.GetValue(fabricator) as Light;
        if (fabLight != null)
        {
            fabLight.enabled = enabled;
        }
    }

    private static bool IsInstantMode()
    {
        return Plugin.Services != null && Plugin.Services.Config.CraftingMode.Value == ModConfig.CraftingModeInstant;
    }
}
