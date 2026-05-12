using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a diagnosed fault from the Fault Diagnosis Agent.
/// This is the input to the Repair Planner Agent.
/// </summary>
public sealed class DiagnosedFault
{
    /// <summary>Gets or sets the unique identifier for this fault record.</summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the machine ID associated with this fault.</summary>
    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    /// <summary>Gets or sets the type of fault (e.g., "curing_temperature_excessive").</summary>
    [JsonPropertyName("faultType")]
    [JsonProperty("faultType")]
    public string FaultType { get; set; } = string.Empty;

    /// <summary>Gets or sets the severity level of the fault.</summary>
    [JsonPropertyName("severity")]
    [JsonProperty("severity")]
    public string Severity { get; set; } = string.Empty; // "critical", "high", "medium", "low"

    /// <summary>Gets or sets a detailed description of the fault.</summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp when the fault was diagnosed.</summary>
    [JsonPropertyName("diagnosedAt")]
    [JsonProperty("diagnosedAt")]
    public DateTime DiagnosedAt { get; set; }

    /// <summary>Gets or sets the confidence level of the diagnosis (0-100).</summary>
    [JsonPropertyName("confidenceScore")]
    [JsonProperty("confidenceScore")]
    public double ConfidenceScore { get; set; }

    /// <summary>Gets or sets any additional context or recommendations from the diagnosis.</summary>
    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;
}
