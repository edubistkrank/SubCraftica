using SubCraftica.Services.Localization;

namespace SubCraftica.Services.Crafting;

internal sealed class QueueFeedbackService
{
    public void NotifyQueued(CraftingRequest request)
    {
        if (request == null)
            return;

        var name = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(request.TechType, false))
            : TechTypeExtensions.AsString(request.TechType, false);
        var message = ModText.Format(ModText.QueueQueued, request.Amount, name);
        ErrorMessage.AddMessage(message);
    }

    public void NotifyCraftProgress(TechType techType, int current, int total)
    {
        if (techType == TechType.None || total <= 1 || current <= 0)
            return;

        var name = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(techType, false))
            : TechTypeExtensions.AsString(techType, false);
        ErrorMessage.AddMessage(ModText.Format(ModText.QueueProgress, name, current, total));
    }

    public void NotifyQueueFull(int maxQueueSize)
    {
        ErrorMessage.AddWarning(ModText.Format(ModText.QueueFull, maxQueueSize));
    }

    public void NotifyQueueMismatch(TechType queued, TechType requested)
    {
        var queuedName = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(queued, false))
            : TechTypeExtensions.AsString(queued, false);
        var requestedName = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(requested, false))
            : TechTypeExtensions.AsString(requested, false);

        ErrorMessage.AddWarning(ModText.Format(ModText.QueueMismatch, queuedName, requestedName));
    }

    public void NotifyQueueCompleted()
    {
        ErrorMessage.AddMessage(ModText.Get(ModText.QueueCompleted));
    }
}
