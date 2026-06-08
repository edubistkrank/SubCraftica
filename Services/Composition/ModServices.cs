using SubCraftica.Services.Compat;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Resources;
using SubCraftica.Services.Stacking;
using SubCraftica.Services.UI;

namespace SubCraftica.Services.Composition;

internal sealed class ModServices
{
    public ModServices(ModConfig config)
    {
        Config = config;
        StackingDetection = new StackingDetectionService();
        Queue = new CraftingQueueService();
        QueueProgressMessage = new QueueProgressMessageService();
        QueueFeedback = new QueueFeedbackService(QueueProgressMessage, config);
        Synchronization = new CraftSynchronizationService();
        Math = new CraftingMathService(config);
        DefabricatorCompat = new DefabricatorCompatService();
        PrototypeSubCompat = new PrototypeSubCompatService();
        PowerSaverCompat = new PowerSaverCompatService();
        InferiusQoLCompat = new InferiusQoLCompatService();
        StackingCount = new StackingCountService(StackingDetection);
        NearbyStorage = new NearbyStorageService(config, StackingCount);
        StorageExtractionExclusions = new StorageExtractionExclusionService(config);
        StoragePreferredSurplus = new StoragePreferredSurplusService(config);
        PlannerValidation = new PlannerValidationService();
        RecipePlanner = new RecipePlannerService(config, StackingCount, NearbyStorage);
        RecipeOverride = new RecipeDataOverrideService();
        CraftRuntimeState = new CraftRuntimeState();
        Runtime = new CraftRuntimeTrackerService();
        Energy = new CraftEnergyService(config, Math, PowerSaverCompat);
        Quantity = new QuantitySelectionService(config, RecipePlanner, Synchronization, Queue, DefabricatorCompat);
        TimeController = new CraftingTimeControllerService();
        QueueCoordinator = new CraftQueueCoordinatorService();
    }

    public ModConfig Config { get; }
    public StackingDetectionService StackingDetection { get; }
    public CraftingQueueService Queue { get; }
    public QueueProgressMessageService QueueProgressMessage { get; }
    public QueueFeedbackService QueueFeedback { get; }
    public CraftSynchronizationService Synchronization { get; }
    public CraftingMathService Math { get; }
    public DefabricatorCompatService DefabricatorCompat { get; }
    public PrototypeSubCompatService PrototypeSubCompat { get; }
    public PowerSaverCompatService PowerSaverCompat { get; }
    public InferiusQoLCompatService InferiusQoLCompat { get; }
    public StackingCountService StackingCount { get; }
    public NearbyStorageService NearbyStorage { get; }
    public StorageExtractionExclusionService StorageExtractionExclusions { get; }
    public StoragePreferredSurplusService StoragePreferredSurplus { get; }
    public PlannerValidationService PlannerValidation { get; }
    public RecipePlannerService RecipePlanner { get; }
    public RecipeDataOverrideService RecipeOverride { get; }
    public CraftRuntimeState CraftRuntimeState { get; }
    public CraftRuntimeTrackerService Runtime { get; }
    public CraftEnergyService Energy { get; }
    public QuantitySelectionService Quantity { get; }
    public CraftingTimeControllerService TimeController { get; }
    public CraftQueueCoordinatorService QueueCoordinator { get; }
}