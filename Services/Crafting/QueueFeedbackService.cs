using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using SubCraftica.Services.UI;

namespace SubCraftica.Services.Crafting;

internal sealed class QueueFeedbackService
{
    private readonly QueueProgressMessageService progressMessages;
    private readonly ModConfig config;

    public QueueFeedbackService(QueueProgressMessageService progressMessages, ModConfig config)
    {
        this.progressMessages = progressMessages;
        this.config = config;
    }

    private bool IsPerItemMode => config.CraftingMode.Value == ModConfig.CraftingModePerItem;

    public void NotifyQueued(CraftingRequest request, bool craftInProgress)
    {
        if (request == null)
            return;

        var name = Language.main != null
            ? Language.main.Get(TechTypeExtensions.AsString(request.TechType, false))
            : TechTypeExtensions.AsString(request.TechType, false);
        var message = ModText.Format(ModText.QueueQueued, request.Amount, name);
        ErrorMessage.AddMessage(message);

        // Progress lines only make sense in per-item mode (batch/instant don't have intermediate steps).
        if (IsPerItemMode && craftInProgress)
            progressMessages.RegisterPending(request.TechType, request.TotalAmount);
    }

    public void NotifyCraftProgress(TechType techType, int current, int total)
    {
        if (!IsPerItemMode)
            return;

        if (techType == TechType.None || total <= 0 || current <= 0)
            return;

        progressMessages.SetProgress(techType, current, total);
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
        progressMessages.Clear();
        ErrorMessage.AddMessage(ModText.Get(ModText.QueueCompleted));
    }

    public void ClearProgress(TechType techType)
    {
        progressMessages.RemoveProgress(techType);
    }

    public void ClearAllProgress()
    {
        progressMessages.Clear();
    }
}
