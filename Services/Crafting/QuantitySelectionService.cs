using SubCraftica.Services.Compat;
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
    private readonly DefabricatorCompatService defabricatorCompat;

    // Only set by UpdateWithScroll — the item the player is actively adjusting.
    private TechType focusedTechType = TechType.None;
    private int focusedAmount = 1;
    private float nextControllerAdjustAt;
    private bool wasCraftSessionActive;
    private float sessionInactiveSince = -1f;

    public QuantitySelectionService(
        ModConfig config,
        RecipePlannerService planner,
        CraftSynchronizationService synchronization,
        CraftingQueueService queue,
        DefabricatorCompatService defabricatorCompat)
    {
        this.config = config;
        this.planner = planner;
        this.synchronization = synchronization;
        this.queue = queue;
        this.defabricatorCompat = defabricatorCompat;
    }

    /// <summary>
    /// Pure read — returns the focused amount if this is the focused item, otherwise 1.
    /// Never changes internal focus state.
    /// </summary>
    public int GetCurrentAmount(TechType techType)
    {
        SyncSessionState(IsCraftSessionActive());
        return techType != TechType.None && techType == focusedTechType ? focusedAmount : 1;
    }

    /// <summary>
    /// Called each frame for the item under the cursor. Updates focus and scroll/controller input.
    /// </summary>
    public int UpdateWithScroll(TechType techType)
    {
        SyncSessionState(IsCraftSessionActive());

        if (techType == TechType.None)
            return 1;

        // Switching to a different item — reset amount to 1
        if (techType != focusedTechType)
        {
            focusedTechType = techType;
            focusedAmount = 1;
            nextControllerAdjustAt = 0f;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta > 0.01f)
            focusedAmount = TryIncrease(techType, focusedAmount);
        else if (scrollDelta < -0.01f)
            focusedAmount = Mathf.Max(1, focusedAmount - 1);

        if (TryConsumeKeyboardIncrease())
            focusedAmount = TryIncrease(techType, focusedAmount);
        else if (TryConsumeKeyboardDecrease())
            focusedAmount = Mathf.Max(1, focusedAmount - 1);

        focusedAmount = HandleControllerAdjust(techType, focusedAmount);
        focusedAmount = Mathf.Clamp(focusedAmount, 1, Mathf.Max(1, config.MaxQueueSize.Value));

        return focusedAmount;
    }

    /// <summary>
    /// Called after the item is sent to queue — clears focus so next hover starts at x1.
    /// </summary>
    public void ResetFocus(TechType techType)
    {
        if (focusedTechType == techType)
        {
            focusedTechType = TechType.None;
            focusedAmount = 1;
        }
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
            return;

        if (sessionInactiveSince < 0f)
        {
            sessionInactiveSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - sessionInactiveSince < SessionEndCleanupDelaySeconds)
            return;

        focusedTechType = TechType.None;
        focusedAmount = 1;
        nextControllerAdjustAt = 0f;
        wasCraftSessionActive = false;
        sessionInactiveSince = -1f;
    }

    private bool IsCraftSessionActive()
    {
        return (synchronization != null && synchronization.IsCraftInProgress)
            || (queue != null && queue.Count > 0);
    }

    private int HandleControllerAdjust(TechType techType, int amount)
    {
        if (!GameInput.IsPrimaryDeviceGamepad())
        {
            nextControllerAdjustAt = 0f;
            return amount;
        }

        if (TryConsumeControllerAdjust(GameInput.Button.UINextTab))
            return TryIncrease(techType, amount);

        if (TryConsumeControllerAdjust(GameInput.Button.UIPrevTab))
            return Mathf.Max(1, amount - 1);

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
            return false;

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
            return currentAmount;

        var candidate = currentAmount + 1;

        if (defabricatorCompat != null && defabricatorCompat.IsDefabricationActiveFor(techType))
        {
            return defabricatorCompat.CanRecycleAmount(techType, candidate)
                ? candidate
                : currentAmount;
        }

        if (config.CreativeMode.Value)
            return candidate;

        var plan = planner.BuildRequestPlan(techType, candidate);
        return plan.Success ? candidate : currentAmount;
    }

    private static bool TryConsumeKeyboardIncrease()
    {
        return Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus);
    }

    private static bool TryConsumeKeyboardDecrease()
    {
        return Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
    }
}