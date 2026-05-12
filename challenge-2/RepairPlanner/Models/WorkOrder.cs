using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a work order for a repair operation.
/// This is the output of the Repair Planner Agent.
/// Data is stored in the Cosmos DB "WorkOrders" container.
/// </summary>
public sealed class WorkOrder
{
    /// <summary>Gets or sets the unique identifier for this work order (Cosmos DB id).</summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the work order number (human-readable identifier).</summary>
    [JsonPropertyName("workOrderNumber")]
    [JsonProperty("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the machine ID associated with this repair.</summary>
    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    /// <summary>Gets or sets the title of the work order.</summary>
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets a detailed description of the work to be performed.</summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the type of repair (corrective, preventive, or emergency).</summary>
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string Type { get; set; } = "corrective"; // "corrective", "preventive", "emergency"

    /// <summary>Gets or sets the priority level of this work order.</summary>
    [JsonPropertyName("priority")]
    [JsonProperty("priority")]
    public string Priority { get; set; } = "medium"; // "critical", "high", "medium", "low"

    /// <summary>Gets or sets the current status of the work order (used as partition key).</summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "open"; // "open", "in_progress", "completed", "cancelled"

    /// <summary>Gets or sets the ID of the technician assigned to this work order (or null if unassigned).</summary>
    [JsonPropertyName("assignedTo")]
    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }

    /// <summary>Gets or sets additional notes or instructions for the technician.</summary>
    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the estimated duration for this repair in minutes (as an integer).</summary>
    [JsonPropertyName("estimatedDuration")]
    [JsonProperty("estimatedDuration")]
    public int EstimatedDuration { get; set; }

    /// <summary>Gets or sets the list of repair tasks to be performed.</summary>
    [JsonPropertyName("tasks")]
    [JsonProperty("tasks")]
    public List<RepairTask> Tasks { get; set; } = [];

    /// <summary>Gets or sets the list of parts needed for this repair.</summary>
    [JsonPropertyName("partsUsed")]
    [JsonProperty("partsUsed")]
    public List<WorkOrderPartUsage> PartsUsed { get; set; } = [];

    /// <summary>Gets or sets the estimated total cost (labor + parts).</summary>
    [JsonPropertyName("estimatedCost")]
    [JsonProperty("estimatedCost")]
    public decimal EstimatedCost { get; set; }

    /// <summary>Gets or sets the timestamp when this work order was created.</summary>
    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the timestamp of the last update to this work order.</summary>
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the timestamp when the work was planned to start.</summary>
    [JsonPropertyName("plannedStartDate")]
    [JsonProperty("plannedStartDate")]
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>Gets or sets the timestamp when the work was actually completed (or null if not yet completed).</summary>
    [JsonPropertyName("completedAt")]
    [JsonProperty("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
