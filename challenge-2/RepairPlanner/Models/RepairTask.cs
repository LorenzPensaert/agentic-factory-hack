using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a single repair task within a work order.
/// </summary>
public sealed class RepairTask
{
    /// <summary>Gets or sets the sequence number of this task (1-based).</summary>
    [JsonPropertyName("sequence")]
    [JsonProperty("sequence")]
    public int Sequence { get; set; }

    /// <summary>Gets or sets the title of this repair task.</summary>
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets a detailed description of what needs to be done.</summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the estimated duration for this task in minutes (as an integer).</summary>
    [JsonPropertyName("estimatedDurationMinutes")]
    [JsonProperty("estimatedDurationMinutes")]
    public int EstimatedDurationMinutes { get; set; }

    /// <summary>Gets or sets the list of required skills for this task.</summary>
    [JsonPropertyName("requiredSkills")]
    [JsonProperty("requiredSkills")]
    public List<string> RequiredSkills { get; set; } = [];

    /// <summary>Gets or sets safety notes and precautions for this task.</summary>
    [JsonPropertyName("safetyNotes")]
    [JsonProperty("safetyNotes")]
    public string SafetyNotes { get; set; } = string.Empty;

    /// <summary>Gets or sets the current status of this task.</summary>
    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "pending"; // "pending", "in_progress", "completed", "blocked"
}
