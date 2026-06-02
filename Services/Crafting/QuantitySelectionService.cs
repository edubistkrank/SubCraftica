using SubCraftica.Services.Configuration;
using UnityEngine;

namespace SubCraftica.Services.Crafting;

internal sealed class QuantitySelectionService
{
    private const float ControllerInitialRepeatDelay = 0.28f;
    private const float ControllerRepeatInterval = 0.12f;

    private readonly ModConfig config;
    private readonly RecipePlannerService planner;

    private TechType currentTechType = TechType.None;
    private int currentAmount = 1;
    private float nextControllerAdjustAt;

    public QuantitySelectionService(ModConfig config, RecipePlannerService planner)
    {
        this.config = config;
        this.planner = planner;
    }

    public int GetCurrentAmount(TechType techType)
    {
        if (currentTechType != techType)
        {
            currentTechType = techType;
            currentAmount = 1;
            nextControllerAdjustAt = 0f;
        }

        return currentAmount;
    }

    public int UpdateWithScroll(TechType techType)
    {
        if (currentTechType != techType)
        {
            currentTechType = techType;
            currentAmount = 1;
            nextControllerAdjustAt = 0f;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta > 0.01f)
        {
            TryIncrease(techType);
        }
        else if (scrollDelta < -0.01f)
        {
            currentAmount = Mathf.Max(1, currentAmount - 1);
        }

        HandleControllerAdjust(techType);

        return currentAmount;
    }

    private void HandleControllerAdjust(TechType techType)
    {
        if (!GameInput.IsPrimaryDeviceGamepad())
        {
            nextControllerAdjustAt = 0f;
            return;
        }

        if (TryConsumeControllerAdjust(GameInput.Button.CycleNext, GameInput.Button.UIAdjustRight))
        {
            TryIncrease(techType);
            return;
        }

        if (TryConsumeControllerAdjust(GameInput.Button.CyclePrev, GameInput.Button.UIAdjustLeft))
        {
            currentAmount = Mathf.Max(1, currentAmount - 1);
        }
    }

    private bool TryConsumeControllerAdjust(GameInput.Button primaryButton, GameInput.Button secondaryButton)
    {
        if (GameInput.GetButtonDown(primaryButton) || GameInput.GetButtonDown(secondaryButton))
        {
            nextControllerAdjustAt = Time.unscaledTime + ControllerInitialRepeatDelay;
            return true;
        }

        if (Time.unscaledTime < nextControllerAdjustAt)
        {
            return false;
        }

        if (GameInput.GetButtonHeld(primaryButton) || GameInput.GetButtonHeld(secondaryButton))
        {
            nextControllerAdjustAt = Time.unscaledTime + ControllerRepeatInterval;
            return true;
        }

        return false;
    }

    private void TryIncrease(TechType techType)
    {
        var upperLimit = Mathf.Max(1, config.MaxQueueSize.Value);
        if (currentAmount >= upperLimit)
        {
            return;
        }

        var candidate = currentAmount + 1;

        if (config.CreativeMode.Value)
        {
            currentAmount = candidate;
            return;
        }

        var plan = planner.BuildRequestPlan(techType, candidate);
        if (plan.Success)
        {
            currentAmount = candidate;
        }
    }
}