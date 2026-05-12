namespace RepairPlanner.Services;

/// <summary>
/// Configuration options for Cosmos DB connection and containers.
/// </summary>
public sealed class CosmosDbOptions
{
    /// <summary>Gets or sets the Cosmos DB endpoint URI.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the Cosmos DB account key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the database name.</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>Gets or sets the container name for technicians.</summary>
    public string TechniciansContainer { get; set; } = "Technicians";

    /// <summary>Gets or sets the container name for parts inventory.</summary>
    public string PartsContainer { get; set; } = "PartsInventory";

    /// <summary>Gets or sets the container name for work orders.</summary>
    public string WorkOrdersContainer { get; set; } = "WorkOrders";
}
