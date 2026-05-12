using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

// ============================================================================
// Repair Planner Agent - Main Entry Point
// ============================================================================
// This demonstrates the Repair Planner Agent workflow using the Foundry 
// Agents SDK to generate work orders from diagnosed faults.
// ============================================================================

var logger = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
}).CreateLogger(nameof(Program));

try
{
    logger.LogInformation("========== Repair Planner Agent Starting ==========");

    // Step 1: Load configuration from environment variables
    logger.LogInformation("Loading configuration from environment variables");

    var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
        ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT environment variable is required");

    var modelDeploymentName = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
        ?? throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable is required");

    var cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
        ?? throw new InvalidOperationException("COSMOS_ENDPOINT environment variable is required");

    var cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY")
        ?? throw new InvalidOperationException("COSMOS_KEY environment variable is required");

    var cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME")
        ?? throw new InvalidOperationException("COSMOS_DATABASE_NAME environment variable is required");

    logger.LogInformation("Configuration loaded successfully");
    logger.LogDebug("Azure Endpoint: {Endpoint}", azureEndpoint);
    logger.LogDebug("Model Deployment: {Model}", modelDeploymentName);
    logger.LogDebug("Cosmos Database: {Database}", cosmosDatabaseName);

    // Step 2: Set up dependency injection container
    logger.LogInformation("Setting up dependency injection container");

    var services = new ServiceCollection();

    // Add logging
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    });

    // Add Cosmos DB service
    services.AddSingleton(new CosmosDbOptions
    {
        Endpoint = cosmosEndpoint,
        Key = cosmosKey,
        DatabaseName = cosmosDatabaseName,
        TechniciansContainer = "Technicians",
        PartsContainer = "PartsInventory",
        WorkOrdersContainer = "WorkOrders"
    });

    services.AddSingleton<CosmosDbService>();

    // Add fault mapping service
    services.AddSingleton<IFaultMappingService, FaultMappingService>();

    // Add Azure AI Foundry client (uses DefaultAzureCredential)
    services.AddSingleton(sp =>
    {
        var credential = new DefaultAzureCredential();
        return new AIProjectClient(new Uri(azureEndpoint), credential);
    });

    // Add Repair Planner Agent
    services.AddSingleton(sp =>
    {
        var projectClient = sp.GetRequiredService<AIProjectClient>();
        var cosmosDb = sp.GetRequiredService<CosmosDbService>();
        var faultMapping = sp.GetRequiredService<IFaultMappingService>();
        var agentLogger = sp.GetRequiredService<ILogger<RepairPlannerAgent>>();

        return new RepairPlannerAgent(
            projectClient,
            cosmosDb,
            faultMapping,
            modelDeploymentName,
            agentLogger);
    });

    var serviceProvider = services.BuildServiceProvider();

    // Step 3: Get services from DI container
    logger.LogInformation("Resolving services from dependency injection container");

    var agent = serviceProvider.GetRequiredService<RepairPlannerAgent>();
    var cosmosDbService = serviceProvider.GetRequiredService<CosmosDbService>();

    // Step 4: Ensure agent is registered with Azure AI Foundry
    logger.LogInformation("Registering agent with Azure AI Foundry");
    await agent.EnsureAgentVersionAsync();
    logger.LogInformation("Agent registration complete");

    // Step 5: Create a sample diagnosed fault
    logger.LogInformation("Creating sample diagnosed fault for demonstration");

    var sampleFault = new DiagnosedFault
    {
        Id = Guid.NewGuid().ToString("N"),
        MachineId = "CURING-PRESS-001",
        FaultType = "curing_temperature_excessive",
        Severity = "high",
        Description = "Curing press temperature exceeded safe threshold of 180°C. Currently reading 195°C. " +
                     "Both heating elements responding to control signals but temperature sensor may be drift. " +
                     "Potential causes: faulty temperature sensor, heating element degradation, or thermostat malfunction.",
        DiagnosedAt = DateTime.UtcNow,
        ConfidenceScore = 87.5,
        Notes = "Issue detected during routine maintenance cycle. Recommend immediate inspection of heating elements."
    };

    logger.LogInformation(
        "Sample fault created: {FaultType} on {MachineId} (Severity: {Severity})",
        sampleFault.FaultType,
        sampleFault.MachineId,
        sampleFault.Severity);

    // Step 6: Execute repair planning workflow
    logger.LogInformation("========== Executing Repair Planning Workflow ==========");
    logger.LogInformation("Invoking RepairPlannerAgent with diagnosed fault");

    var workOrder = await agent.PlanAndCreateWorkOrderAsync(sampleFault);

    // Step 7: Display results
    logger.LogInformation("========== Work Order Generated Successfully ==========");
    logger.LogInformation("Work Order Number: {WorkOrderNumber}", workOrder.WorkOrderNumber);
    logger.LogInformation("Machine ID: {MachineId}", workOrder.MachineId);
    logger.LogInformation("Title: {Title}", workOrder.Title);
    logger.LogInformation("Type: {Type}", workOrder.Type);
    logger.LogInformation("Priority: {Priority}", workOrder.Priority);
    logger.LogInformation("Status: {Status}", workOrder.Status);
    logger.LogInformation("Assigned To: {AssignedTo}", workOrder.AssignedTo ?? "(Unassigned)");
    logger.LogInformation("Estimated Duration: {Duration} minutes", workOrder.EstimatedDuration);
    logger.LogInformation("Estimated Cost: ${Cost}", workOrder.EstimatedCost);

    logger.LogInformation("");
    logger.LogInformation("Description: {Description}", workOrder.Description);
    logger.LogInformation("");

    if (workOrder.Tasks.Count > 0)
    {
        logger.LogInformation("--- Repair Tasks ({Count}) ---", workOrder.Tasks.Count);
        foreach (var task in workOrder.Tasks)
        {
            logger.LogInformation(
                "  [{Seq}] {Title} ({Duration}min) - Skills: {Skills}",
                task.Sequence,
                task.Title,
                task.EstimatedDurationMinutes,
                string.Join(", ", task.RequiredSkills));
            logger.LogInformation("      {Description}", task.Description);
            if (!string.IsNullOrWhiteSpace(task.SafetyNotes))
            {
                logger.LogInformation("      ⚠️  Safety: {SafetyNotes}", task.SafetyNotes);
            }
        }
        logger.LogInformation("");
    }

    if (workOrder.PartsUsed.Count > 0)
    {
        logger.LogInformation("--- Parts Required ({Count}) ---", workOrder.PartsUsed.Count);
        foreach (var part in workOrder.PartsUsed)
        {
            logger.LogInformation(
                "  {PartNumber}: {PartName} (Qty: {Qty}, Cost: ${Cost})",
                part.PartNumber,
                part.PartName,
                part.Quantity,
                part.TotalCost);
        }
        logger.LogInformation("");
    }

    logger.LogInformation("Work order saved to Cosmos DB with ID: {Id}", workOrder.Id);
    logger.LogInformation("========== Workflow Complete ==========");

    // Clean up
    await cosmosDbService.DisposeAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred during execution");
    Environment.Exit(1);
}
