namespace SubCraftica.Services.Crafting;

internal sealed class CraftingRequest
{
    public CraftingRequest(TechType techType, int amount)
        : this(techType, amount, amount)
    {
    }

    public CraftingRequest(TechType techType, int amount, int totalAmount)
    {
        TechType = techType;
        Amount = amount;
        TotalAmount = totalAmount < amount ? amount : totalAmount;
    }

    public TechType TechType { get; }
    public int Amount { get; }
    public int TotalAmount { get; }
}