using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SubCraftica.Patches;

/// <summary>
/// Patches uGUI_RecipeEntry.UpdateIngredients so that the pinned-recipe tracker
/// on the right side of the screen counts ingredients from inventory + nearby
/// storage (respecting the player's configured storage mode), instead of only
/// from the player's direct inventory container.
/// </summary>
[HarmonyPatch(typeof(uGUI_RecipeEntry), nameof(uGUI_RecipeEntry.UpdateIngredients))]
internal static class uGUIPinnedRecipesUpdateIngredientsPatch
{
    private static readonly MethodInfo ContainerGetCount =
        AccessTools.Method(typeof(ItemsContainer), nameof(ItemsContainer.GetCount));

    private static readonly MethodInfo ModGetCount =
        AccessTools.Method(typeof(uGUIPinnedRecipesUpdateIngredientsPatch), nameof(GetTotalCount));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // Replace every call to ItemsContainer.GetCount(TechType) with our
            // helper that adds nearby-storage and stacking-mod counts on top.
            if (instruction.opcode == OpCodes.Callvirt
                && instruction.operand is MethodInfo mi
                && mi == ContainerGetCount)
            {
                // Stack at this point: ..., ItemsContainer, TechType
                // Our helper has the same signature: (ItemsContainer, TechType) -> int
                yield return new CodeInstruction(OpCodes.Call, ModGetCount);
                continue;
            }

            yield return instruction;
        }
    }

    /// <summary>
    /// Returns the total count of <paramref name="techType"/> visible from the
    /// player's inventory plus all eligible nearby/base/pod storage containers,
    /// using the same logic as the rest of SubCraftica's resource counting.
    /// </summary>
    private static int GetTotalCount(ItemsContainer container, TechType techType)
    {
        if (Plugin.Services == null)
        {
            return container.GetCount(techType);
        }

        var inventoryCount = Plugin.Services.StackingCount.GetContainerCount(container, techType);
        var nearbyCount = Plugin.Services.NearbyStorage.GetNearbyCount(techType, container);
        return inventoryCount + nearbyCount;
    }
}
