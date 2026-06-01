using System.Collections.Generic;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftingQueueService
{
    private readonly Queue<CraftingRequest> queue = new Queue<CraftingRequest>();

    public int Count => queue.Count;

    public bool TryEnqueue(CraftingRequest request, int maxQueueSize)
    {
        if (request == null || request.Amount <= 0)
        {
            return false;
        }

        if (queue.Count >= maxQueueSize)
        {
            return false;
        }

        if (queue.Count > 0)
        {
            var head = queue.Peek();
            if (head.TechType != request.TechType)
            {
                return false;
            }
        }

        queue.Enqueue(request);
        return true;
    }

    public bool TryPeek(out CraftingRequest request)
    {
        if (queue.Count == 0)
        {
            request = null;
            return false;
        }

        request = queue.Peek();
        return true;
    }

    public bool TryDequeue(out CraftingRequest request)
    {
        if (queue.Count == 0)
        {
            request = null;
            return false;
        }

        request = queue.Dequeue();
        return true;
    }

    public bool TryDequeueForTechType(TechType techType, out CraftingRequest request)
    {
        if (queue.Count == 0)
        {
            request = null;
            return false;
        }

        var next = queue.Peek();
        if (next.TechType != techType)
        {
            request = null;
            return false;
        }

        request = queue.Dequeue();
        return true;
    }

    public void Clear() => queue.Clear();
}