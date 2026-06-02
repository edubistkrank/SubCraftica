using HarmonyLib;
using SubCraftica.Services.Configuration;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(CrafterLogic), nameof(CrafterLogic.IsCraftRecipeFulfilled))]
internal static class CrafterLogicIsCraftRecipeFulfilledPatch
{
    [HarmonyPostfix]
    private static void Postfix(TechType techType, ref bool __result)
    {
        if (Plugin.Services == null)
        {
            return;
        }

        if (CreativeModeHelper.IsCreativeBypassActive(techType))
        {
            __result = true;
            return;
        }

        if (__result)
        {
            return;
        }

        if (!Plugin.Services.PlannerValidation.CanPlan(techType))
        {
            return;
        }

        var amount = GetRequestedAmount(techType);
        var plan = Plugin.Services.RecipePlanner.BuildRequestPlan(techType, amount);
        __result = plan.Success;
    }

    private static int GetRequestedAmount(TechType techType)
    {
        if (CraftingMenuClientHelper.IsConstructorClientActive())
        {
            return 1;
        }

        if (Plugin.Services.RecipeOverride.IsOverridden(techType))
        {
            return 1;
        }

        return Plugin.Services.Quantity.GetCurrentAmount(techType);
    }
}
