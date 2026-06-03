using System.Collections.Generic;
using SubCraftica.Services.Configuration;
using UnityEngine;

namespace SubCraftica.Services.Crafting;

internal sealed class QuantitySelectionService
{
    private const float ControllerInitialRepeatDelay = 0.28f;
    private const float ControllerRepeatInterval = 0.12f;
    private const float SessionEndCleanupDelaySeconds = 1f;

    private readonly ModConfig config;
    private readonly RecipePlannerService planner;
    private readonly CraftSynchronizationService synchronization;
    private readonly CraftingQueueService queue;
    private readonly Dictionary<TechType, int> amountsByTechType = new Dictionary<TechType, int>();

    private TechType currentTechType = TechType.None;
    private int currentAmount = 1;
    private float nextControllerAdjustAt;
    private bool wasCraftSessionActive;
    private float sessionInactiveSince = -1f;

    public QuantitySelectionService(
        ModConfig config,
        RecipePlannerService planner,
        CraftSynchronizationService synchronization,
        CraftingQueueService queue)
    {
        this.config = config;
        this.planner = planner;
        this.synchronization = synchronization;
        this.queue = queue;
    }

    public int GetCurrentAmount(TechType techType)
    {
        var sessionActive = IsCraftSessionActive();
        SyncSessionState(sessionActive);

        if (currentTechType != techType)
        {
            currentTechType = techType;
            currentAmount = sessionActive ? GetStoredAmount(techType) : 1;
            nextControllerAdjustAt = 0f;
        }

        var clamped = Mathf.Clamp(currentAmount, 1, Mathf.Max(1, config.MaxQueueSize.Value));
        currentAmount = clamped;

        if (sessionActive && techType != TechType.None)
        {
            amountsByTechType[techType] = clamped;
        }

        return clamped;
    }

    public int UpdateWithScroll(TechType techType)
    {
        var amount = GetCurrentAmount(techType);

        var scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta > 0.01f)
        {
            amount = TryIncrease(techType, amount);
        }
        else if (scrollDelta < -0.01f)
        {
            amount = Mathf.Max(1, amount - 1);
        }

        amount = HandleControllerAdjust(techType, amount);

        currentTechType = techType;
        currentAmount = amount;

        if (IsCraftSessionActive() && techType != TechType.None)
        {
            amountsByTechType[techType] = amount;
        }

        return amount;
    }

    private void SyncSessionState(bool sessionActive)
    {
        if (sessionActive)
        {
            wasCraftSessionActive = true;
            sessionInactiveSince = -1f;
            return;
        }

        if (!wasCraftSessionActive)
        {
            return;
        }

        if (sessionInactiveSince < 0f)
        {
            sessionInactiveSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - sessionInactiveSince < SessionEndCleanupDelaySeconds)
        {
            return;
        }

        amountsByTechType.Clear();
        currentTechType = TechType.None;
        currentAmount = 1;
        nextControllerAdjustAt = 0f;
        wasCraftSessionActive = false;
        sessionInactiveSince = -1f;
    }

    private bool IsCraftSessionActive()
    {
        return (synchronization != null && synchronization.IsCraftInProgress)
            || (queue != null && queue.Count > 0);
    }

    private int GetStoredAmount(TechType techType)
    {
        if (techType == TechType.None)
        {
            return 1;
        }

        return amountsByTechType.TryGetValue(techType, out var amount) ? amount : 1;
    }

    private int HandleControllerAdjust(TechType techType, int amount)
    {
        if (!GameInput.IsPrimaryDeviceGamepad())
        {
            nextControllerAdjustAt = 0f;
            return amount;
        }

        if (TryConsumeControllerAdjust(GameInput.Button.UINextTab))
        {
            return TryIncrease(techType, amount);
        }

        if (TryConsumeControllerAdjust(GameInput.Button.UIPrevTab))
        {
            return Mathf.Max(1, amount - 1);
        }

        return amount;
    }

    private bool TryConsumeControllerAdjust(GameInput.Button button)
    {
        if (GameInput.GetButtonDown(button))
        {
            nextControllerAdjustAt = Time.unscaledTime + ControllerInitialRepeatDelay;
            return true;
        }

        if (Time.unscaledTime < nextControllerAdjustAt)
        {
            return false;
        }

        if (GameInput.GetButtonHeld(button))
        {
            nextControllerAdjustAt = Time.unscaledTime + ControllerRepeatInterval;
            return true;
        }

        return false;
    }

    private int TryIncrease(TechType techType, int currentAmount)
    {
        var upperLimit = Mathf.Max(1, config.MaxQueueSize.Value);
        if (currentAmount >= upperLimit)
        {
            return currentAmount;
        }

        var candidate = currentAmount + 1;

        if (config.CreativeMode.Value)
        {
            return candidate;
        }

        var plan = planner.BuildRequestPlan(techType, candidate);
        return plan.Success ? candidate : currentAmount;
    }
}