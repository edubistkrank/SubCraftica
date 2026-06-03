using System.Collections.Generic;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftingQueueService
{
    private readonly LinkedList<CraftingRequest> queue = new LinkedList<CraftingRequest>();

    public int Count => queue.Count;

    public bool TryEnqueue(CraftingRequest request, int maxQueueSize)
    {
        if (!CanEnqueue(request, maxQueueSize))
        {
            return false;
        }

        queue.AddLast(request);
        return true;
    }

    public bool TryEnqueueFront(CraftingRequest request, int maxQueueSize)
    {
        if (!CanEnqueue(request, maxQueueSize))
        {
            return false;
        }

        queue.AddFirst(request);
        return true;
    }

    public bool TryPeek(out CraftingRequest request)
    {
        var node = queue.First;
        if (node == null)
        {
            request = null;
            return false;
        }

        request = node.Value;
        return true;
    }

    public bool TryDequeue(out CraftingRequest request)
    {
        var node = queue.First;
        if (node == null)
        {
            request = null;
            return false;
        }

        request = node.Value;
        queue.RemoveFirst();
        return true;
    }

    public bool TryDequeueForTechType(TechType techType, out CraftingRequest request)
    {
        var node = queue.First;
        if (node == null)
        {
            request = null;
            return false;
        }

        if (node.Value.TechType != techType)
        {
            request = null;
            return false;
        }

        request = node.Value;
        queue.RemoveFirst();
        return true;
    }

    public void Clear() => queue.Clear();

    private bool CanEnqueue(CraftingRequest request, int maxQueueSize)
    {
        if (request == null || request.Amount <= 0)
        {
            return false;
        }

        if (queue.Count >= maxQueueSize)
        {
            return false;
        }

        return true;
    }
}