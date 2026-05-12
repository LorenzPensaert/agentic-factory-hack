using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents an inventory part or component used in repairs.
/// Data is stored in the Cosmos DB "PartsInventory" container.
/// </summary>
public sealed class Part
{
    /// <summary>Gets or sets the unique identifier for this part.</summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the part number (manufacturer's part code).</summary>
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable name of the part.</summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the category of the part (used as partition key).</summary>
    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty; // e.g., "heating_elements", "seals", "bearings"

    /// <summary>Gets or sets a detailed description of the part's purpose.</summary>
    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the current quantity in stock.</summary>
    [JsonPropertyName("quantityInStock")]
    [JsonProperty("quantityInStock")]
    public int QuantityInStock { get; set; }

    /// <summary>Gets or sets the minimum quantity that should be maintained.</summary>
    [JsonPropertyName("reorderPoint")]
    [JsonProperty("reorderPoint")]
    public int ReorderPoint { get; set; }

    /// <summary>Gets or sets the unit cost of this part in USD.</summary>
    [JsonPropertyName("unitCost")]
    [JsonProperty("unitCost")]
    public decimal UnitCost { get; set; }

    /// <summary>Gets or sets the supplier's name or ID.</summary>
    [JsonPropertyName("supplierId")]
    [JsonProperty("supplierId")]
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Gets or sets the typical lead time for reordering (in days).</summary>
    [JsonPropertyName("leadTimeDays")]
    [JsonProperty("leadTimeDays")]
    public int LeadTimeDays { get; set; }

    /// <summary>Gets or sets the timestamp of when this record was last updated.</summary>
    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
