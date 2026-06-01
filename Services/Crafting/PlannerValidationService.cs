namespace SubCraftica.Services.Crafting;

internal sealed class PlannerValidationService
{
    public bool CanPlan(TechType techType)
    {
        if (techType == TechType.None)
        {
            return false;
        }

        var ingredients = TechData.GetIngredients(techType);
        return ingredients != null;
    }
}