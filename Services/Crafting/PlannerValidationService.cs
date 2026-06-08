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
        if (ingredients != null)
        {
            return true;
        }

        var linkedItems = TechData.GetLinkedItems(techType);
        return linkedItems != null && linkedItems.Count > 0;
    }
}