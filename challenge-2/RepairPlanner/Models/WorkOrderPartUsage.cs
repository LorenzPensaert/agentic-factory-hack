using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents the usage of a part in a work order.
/// </summary>
public sealed class WorkOrderPartUsage
{
    /// <summary>Gets or sets the unique identifier of the part.</summary>
    [JsonPropertyName("partId")]
    [JsonProperty("partId")]
    public string PartId { get; set; } = string.Empty;

    /// <summary>Gets or sets the manufacturer's part number.</summary>
    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable name of the part.</summary>
    [JsonPropertyName("partName")]
    [JsonProperty("partName")]
    public string PartName { get; set; } = string.Empty;

    /// <summary>Gets or sets the quantity of this part needed for the repair.</summary>
    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    /// <summary>Gets or sets the unit cost of this part at the time the work order was created.</summary>
    [JsonPropertyName("unitCost")]
    [JsonProperty("unitCost")]
    public decimal UnitCost { get; set; }

    /// <summary>Gets or sets the total cost for this part (quantity * unitCost).</summary>
    [JsonPropertyName("totalCost")]
    [JsonProperty("totalCost")]
    public decimal TotalCost { get; set; }
}
