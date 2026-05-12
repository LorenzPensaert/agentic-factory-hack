namespace RepairPlanner.Services;

/// <summary>
/// Provides mappings between fault types and the skills/parts required to repair them.
/// </summary>
public interface IFaultMappingService
{
    /// <summary>
    /// Gets the list of skills required to repair a specific fault type.
    /// </summary>
    /// <param name="faultType">The fault type (e.g., "curing_temperature_excessive").</param>
    /// <returns>A read-only list of required skills. Returns ["general_maintenance"] if fault type is unknown.</returns>
    IReadOnlyList<string> GetRequiredSkills(string faultType);

    /// <summary>
    /// Gets the list of part IDs required to repair a specific fault type.
    /// </summary>
    /// <param name="faultType">The fault type (e.g., "curing_temperature_excessive").</param>
    /// <returns>A read-only list of required part IDs. Returns an empty list if no parts are needed or fault type is unknown.</returns>
    IReadOnlyList<string> GetRequiredParts(string faultType);
}
