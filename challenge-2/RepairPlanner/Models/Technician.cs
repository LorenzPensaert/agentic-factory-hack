using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a repair technician with skills and availability.
/// Data is stored in the Cosmos DB "Technicians" container.
/// </summary>
public sealed class Technician
{
    /// <summary>Gets or sets the unique identifier for this technician.</summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the technician's full name.</summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the department this technician belongs to (used as partition key).</summary>
    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of skills this technician is certified for.</summary>
    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public List<string> Skills { get; set; } = [];

    /// <summary>Gets or sets the technician's certification level (1-5, where 5 is expert).</summary>
    [JsonPropertyName("certificationLevel")]
    [JsonProperty("certificationLevel")]
    public int CertificationLevel { get; set; }

    /// <summary>Gets or sets the number of years of experience in tire manufacturing.</summary>
    [JsonPropertyName("yearsExperience")]
    [JsonProperty("yearsExperience")]
    public int YearsExperience { get; set; }

    /// <summary>Gets or sets a value indicating whether the technician is currently available.</summary>
    [JsonPropertyName("isAvailable")]
    [JsonProperty("isAvailable")]
    public bool IsAvailable { get; set; }

    /// <summary>Gets or sets the technician's contact phone number.</summary>
    [JsonPropertyName("phoneNumber")]
    [JsonProperty("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp of when this record was last updated.</summary>
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
