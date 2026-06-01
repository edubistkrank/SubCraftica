namespace SubCraftica.Services.Crafting;

/// <summary>
/// Centralizes transient coordination state shared across crafting patches,
/// replacing static cross-patch references with a single service owned by ModServices.
/// </summary>
internal sealed class CraftQueueCoordinatorService
{
    private bool _stopQueueContinuationRequested;
    private bool _shouldNotifyQueueCompleted;
    private bool _storageMoveNoticeShown;
    private int _pendingPickupOperations;

    public bool HasPendingPickupOperations => _pendingPickupOperations > 0;

    // --- Queue continuation stop ---

    public void RequestStopQueueContinuation(CraftingQueueService queue)
    {
        _stopQueueContinuationRequested = true;
        queue?.Clear();
    }

    public bool ConsumeStopQueueContinuationRequested()
    {
        if (!_stopQueueContinuationRequested)
        {
            return false;
        }

        _stopQueueContinuationRequested = false;
        return true;
    }

    public void ClearStopQueueContinuationRequested()
    {
        _stopQueueContinuationRequested = false;
    }

    // --- Queue completed notification ---

    public void SetShouldNotifyQueueCompleted()
    {
        _shouldNotifyQueueCompleted = true;
    }

    public bool ConsumeShouldNotifyQueueCompleted()
    {
        if (!_shouldNotifyQueueCompleted)
        {
            return false;
        }

        _shouldNotifyQueueCompleted = false;
        return true;
    }

    // --- Storage move notice ---

    public bool StorageMoveNoticeShown => _storageMoveNoticeShown;

    public void MarkStorageMoveNoticeShown()
    {
        _storageMoveNoticeShown = true;
    }

    public void ResetStorageMoveNotice()
    {
        _storageMoveNoticeShown = false;
    }

    // --- Pending pickup operations ---

    public void BeginPickupOperation()
    {
        _pendingPickupOperations++;
    }

    public void EndPickupOperation()
    {
        if (_pendingPickupOperations > 0)
        {
            _pendingPickupOperations--;
        }
    }

    // --- Full reset (end of queue cycle) ---

    public void ResetForQueueEnd()
    {
        _storageMoveNoticeShown = false;
    }
}
