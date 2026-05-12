using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

/// <summary>
/// Service for interacting with Cosmos DB containers (Technicians, PartsInventory, WorkOrders).
/// Provides methods to query technicians, fetch parts, and create work orders.
/// </summary>
public sealed class CosmosDbService : IAsyncDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _techniciansContainer;
    private readonly Container _partsContainer;
    private readonly Container _workOrdersContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(options?.Endpoint))
            throw new ArgumentException("Cosmos DB endpoint is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Key))
            throw new ArgumentException("Cosmos DB key is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.DatabaseName))
            throw new ArgumentException("Database name is required.", nameof(options));

        _logger.LogInformation("Initializing Cosmos DB client for database: {DatabaseName}", options.DatabaseName);

        try
        {
            _cosmosClient = new CosmosClient(options.Endpoint, options.Key);
            _database = _cosmosClient.GetDatabase(options.DatabaseName);
            _techniciansContainer = _database.GetContainer(options.TechniciansContainer);
            _partsContainer = _database.GetContainer(options.PartsContainer);
            _workOrdersContainer = _database.GetContainer(options.WorkOrdersContainer);

            _logger.LogInformation(
                "Cosmos DB service initialized. Containers: {Technicians}, {Parts}, {WorkOrders}",
                options.TechniciansContainer,
                options.PartsContainer,
                options.WorkOrdersContainer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB service");
            throw;
        }
    }

    /// <summary>
    /// Retrieves available technicians who have all of the specified required skills.
    /// </summary>
    /// <param name="requiredSkills">List of skills required for the repair.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available technicians matching the skills.</returns>
    public async Task<List<Technician>> GetAvailableTechniciansBySkillsAsync(
        IReadOnlyList<string> requiredSkills,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying available technicians with skills: {Skills}", string.Join(", ", requiredSkills));

            // Note: Cosmos DB LINQ doesn't support .All() on arrays, so we fetch available technicians
            // and filter for skill matches in-memory. This is acceptable for typical technician roster sizes.
            var query = _techniciansContainer.GetItemLinqQueryable<Technician>()
                .Where(t => t.IsAvailable)
                .OrderByDescending(t => t.CertificationLevel)
                .ThenByDescending(t => t.YearsExperience);

            using var iterator = query.ToFeedIterator();
            var allAvailableTechnicians = new List<Technician>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                allAvailableTechnicians.AddRange(response);
            }

            // Filter for technicians who have ALL required skills
            var matchingTechnicians = allAvailableTechnicians
                .Where(t => requiredSkills.All(skill => t.Skills.Contains(skill)))
                .ToList();

            _logger.LogInformation("Found {Count} available technicians with required skills", matchingTechnicians.Count);
            return matchingTechnicians;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Technicians container not found. Returning empty list.");
            return new List<Technician>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying technicians by skills");
            throw;
        }
    }

    /// <summary>
    /// Retrieves parts by their part numbers from inventory.
    /// </summary>
    /// <param name="partNumbers">List of part numbers to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of parts found in inventory.</returns>
    public async Task<List<Part>> GetPartsByNumbersAsync(
        IReadOnlyList<string> partNumbers,
        CancellationToken cancellationToken = default)
    {
        if (partNumbers == null || partNumbers.Count == 0)
        {
            _logger.LogDebug("No part numbers provided");
            return new List<Part>();
        }

        try
        {
            _logger.LogDebug("Querying parts inventory for {Count} part numbers", partNumbers.Count);

            var query = _partsContainer.GetItemLinqQueryable<Part>()
                .Where(p => partNumbers.Contains(p.PartNumber))
                .OrderBy(p => p.Name);

            using var iterator = query.ToFeedIterator();
            var parts = new List<Part>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                parts.AddRange(response);
            }

            _logger.LogInformation("Found {Found} of {Requested} requested parts in inventory", parts.Count, partNumbers.Count);

            if (parts.Count < partNumbers.Count)
            {
                var foundNumbers = new HashSet<string>(parts.Select(p => p.PartNumber));
                var missing = partNumbers.Where(pn => !foundNumbers.Contains(pn));
                _logger.LogWarning("Missing parts: {MissingParts}", string.Join(", ", missing));
            }

            return parts;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Parts inventory container not found. Returning empty list.");
            return new List<Part>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying parts inventory");
            throw;
        }
    }

    /// <summary>
    /// Creates a new work order in Cosmos DB.
    /// </summary>
    /// <param name="workOrder">The work order to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created work order with Cosmos DB metadata.</returns>
    public async Task<WorkOrder> CreateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workOrder?.Id))
            {
                workOrder!.Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(workOrder.WorkOrderNumber))
            {
                workOrder.WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
            }

            workOrder.CreatedAt = DateTime.UtcNow;
            workOrder.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Creating work order {WorkOrderNumber} for machine {MachineId}",
                workOrder.WorkOrderNumber,
                workOrder.MachineId);

            var response = await _workOrdersContainer.CreateItemAsync(
                workOrder,
                new PartitionKey(workOrder.Status),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Work order {WorkOrderNumber} created successfully. RU consumed: {RequestCharge}",
                workOrder.WorkOrderNumber,
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogError(ex, "Work order {WorkOrderId} already exists", workOrder?.Id);
            throw;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Work orders container not found");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work order {WorkOrderNumber}", workOrder?.WorkOrderNumber);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a work order by ID and status (partition key).
    /// </summary>
    /// <param name="workOrderId">The work order ID.</param>
    /// <param name="status">The work order status (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work order if found; otherwise null.</returns>
    public async Task<WorkOrder?> GetWorkOrderAsync(
        string workOrderId,
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving work order {WorkOrderId} with status {Status}", workOrderId, status);

            var response = await _workOrdersContainer.ReadItemAsync<WorkOrder>(
                workOrderId,
                new PartitionKey(status),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Work order {WorkOrderId} retrieved successfully", workOrderId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Work order {WorkOrderId} not found", workOrderId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing work order.
    /// </summary>
    /// <param name="workOrder">The updated work order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated work order.</returns>
    public async Task<WorkOrder> UpdateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            workOrder.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Updating work order {WorkOrderNumber}", workOrder.WorkOrderNumber);

            var response = await _workOrdersContainer.ReplaceItemAsync(
                workOrder,
                workOrder.Id,
                new PartitionKey(workOrder.Status),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Work order {WorkOrderNumber} updated successfully. RU consumed: {RequestCharge}",
                workOrder.WorkOrderNumber,
                response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Work order {WorkOrderId} not found for update", workOrder.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order {WorkOrderNumber}", workOrder.WorkOrderNumber);
            throw;
        }
    }

    /// <summary>
    /// Disposes the Cosmos DB client connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_cosmosClient != null)
        {
            _logger.LogInformation("Disposing Cosmos DB client");
            _cosmosClient.Dispose();
        }

        await ValueTask.CompletedTask;
    }
}
