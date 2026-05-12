using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

/// <summary>
/// Main orchestrator for the Repair Planner Agent.
/// Coordinates fault diagnosis, technician/parts lookup, and work order generation using the Foundry Agents SDK.
/// </summary>
public sealed class RepairPlannerAgent
{
    private const string AgentName = "RepairPlannerAgent";
    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Your role is to generate a comprehensive repair plan when given a diagnosed fault.
        
        You must:
        1. Analyze the fault type, severity, and description
        2. Determine the sequence of repair tasks needed
        3. Identify required skills and safety considerations
        4. Generate specific, actionable tasks with clear instructions
        5. Return a structured JSON work order
        
        Output MUST be VALID JSON matching this schema:
        {
          "workOrderNumber": "WO-20260511-abcd1234",
          "machineId": "MACHINE-001",
          "title": "Brief title",
          "description": "Detailed description of the repair",
          "type": "corrective|preventive|emergency",
          "priority": "critical|high|medium|low",
          "status": "open",
          "assignedTo": null,
          "notes": "Any additional notes",
          "estimatedDuration": 120,
          "partsUsed": [
            {"partId": "ID", "partNumber": "PART-001", "partName": "Part Name", "quantity": 1, "unitCost": 99.99, "totalCost": 99.99}
          ],
          "tasks": [
            {
              "sequence": 1,
              "title": "Task title",
              "description": "What to do",
              "estimatedDurationMinutes": 30,
              "requiredSkills": ["skill1", "skill2"],
              "safetyNotes": "Safety considerations"
            }
          ]
        }
        
        CRITICAL REQUIREMENTS:
        - estimatedDuration must be an INTEGER (minutes, e.g., 120 not "120 minutes")
        - estimatedDurationMinutes in tasks must be an INTEGER
        - quantity, unitCost, totalCost must be valid numbers
        - Return ONLY valid JSON, no additional text before or after
        - Status should be "open" for new work orders
        - type should be one of: corrective, preventive, emergency
        - priority should be one of: critical, high, medium, low
        """;

    private readonly AIProjectClient _projectClient;
    private readonly CosmosDbService _cosmosDb;
    private readonly IFaultMappingService _faultMapping;
    private readonly string _modelDeploymentName;
    private readonly ILogger<RepairPlannerAgent> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RepairPlannerAgent(
        AIProjectClient projectClient,
        CosmosDbService cosmosDb,
        IFaultMappingService faultMapping,
        string modelDeploymentName,
        ILogger<RepairPlannerAgent> logger)
    {
        _projectClient = projectClient ?? throw new ArgumentNullException(nameof(projectClient));
        _cosmosDb = cosmosDb ?? throw new ArgumentNullException(nameof(cosmosDb));
        _faultMapping = faultMapping ?? throw new ArgumentNullException(nameof(faultMapping));
        _modelDeploymentName = modelDeploymentName ?? throw new ArgumentNullException(nameof(modelDeploymentName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure JSON options to handle numbers-as-strings from LLM responses
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
    }

    /// <summary>
    /// Ensures the agent is registered with Azure AI Foundry.
    /// Creates or updates the agent version with current instructions.
    /// </summary>
    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Ensuring agent '{AgentName}' is registered with Foundry", AgentName);

            var definition = new PromptAgentDefinition(model: _modelDeploymentName)
            {
                Instructions = AgentInstructions
            };

            await _projectClient.Agents.CreateAgentVersionAsync(
                AgentName,
                new AgentVersionCreationOptions(definition),
                ct);

            _logger.LogInformation("Agent '{AgentName}' registered successfully", AgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure agent version");
            throw;
        }
    }

    /// <summary>
    /// Orchestrates the repair planning workflow:
    /// 1. Gets required skills and parts from fault mapping
    /// 2. Queries available technicians and inventory
    /// 3. Invokes the LLM agent to generate the repair plan
    /// 4. Saves the work order to Cosmos DB
    /// </summary>
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(
        DiagnosedFault fault,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Planning repair for fault '{FaultType}' on machine '{MachineId}' (severity: {Severity})",
                fault.FaultType,
                fault.MachineId,
                fault.Severity);

            // Step 1: Determine required skills and parts from mapping
            var requiredSkills = _faultMapping.GetRequiredSkills(fault.FaultType);
            var requiredPartNumbers = _faultMapping.GetRequiredParts(fault.FaultType);

            _logger.LogDebug("Fault mapping - Skills: [{Skills}], Parts: [{Parts}]",
                string.Join(", ", requiredSkills),
                string.Join(", ", requiredPartNumbers));

            // Step 2: Query available technicians and parts from Cosmos DB
            var availableTechnicians = await _cosmosDb.GetAvailableTechniciansBySkillsAsync(
                requiredSkills,
                ct);
            var partsInventory = await _cosmosDb.GetPartsByNumbersAsync(
                requiredPartNumbers,
                ct);

            _logger.LogInformation(
                "Found {TechnicianCount} available technicians and {PartCount} parts from inventory",
                availableTechnicians.Count,
                partsInventory.Count);

            // Step 3: Build context for the LLM agent
            var contextBuilder = BuildAgentContext(fault, availableTechnicians, partsInventory);

            // Step 4: Invoke the LLM agent via Foundry SDK
            var agent = _projectClient.GetAIAgent(name: AgentName);
            _logger.LogDebug("Invoking LLM agent to generate repair plan");

            var response = await agent.RunAsync(contextBuilder, null, null, ct);

            string responseText = response.Text ?? string.Empty;
            _logger.LogDebug("LLM response received: {Response}", responseText);

            // Step 5: Parse JSON response with number-from-string handling
            var workOrder = ParseWorkOrderResponse(responseText, fault);

            // Step 6: Assign best-fit technician if available
            if (availableTechnicians.Count > 0)
            {
                workOrder.AssignedTo = availableTechnicians[0].Id;
                _logger.LogDebug("Assigned technician '{TechnicianId}' to work order", workOrder.AssignedTo);
            }

            // Step 7: Populate parts usage with cost information
            PopulatePartsUsage(workOrder, partsInventory);

            // Step 8: Validate and save to Cosmos DB
            var savedWorkOrder = await _cosmosDb.CreateWorkOrderAsync(workOrder, ct);
            _logger.LogInformation(
                "Work order '{WorkOrderNumber}' successfully created and saved",
                savedWorkOrder.WorkOrderNumber);

            return savedWorkOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to plan and create work order for fault '{FaultType}'", fault.FaultType);
            throw;
        }
    }

    /// <summary>
    /// Builds the context prompt for the LLM agent with fault, technician, and parts information.
    /// </summary>
    private string BuildAgentContext(
        DiagnosedFault fault,
        List<Technician> technicians,
        List<Part> parts)
    {
        var contextLines = new List<string>
        {
            "=== FAULT INFORMATION ===",
            $"Fault Type: {fault.FaultType}",
            $"Machine ID: {fault.MachineId}",
            $"Severity: {fault.Severity}",
            $"Confidence: {fault.ConfidenceScore}%",
            $"Description: {fault.Description}",
            $"Diagnosed At: {fault.DiagnosedAt:O}",
            $"Notes: {fault.Notes}",
            "",
            "=== AVAILABLE TECHNICIANS ===",
        };

        if (technicians.Count > 0)
        {
            foreach (var tech in technicians.Take(5)) // Limit to top 5
            {
                contextLines.Add(
                    $"- {tech.Name} (ID: {tech.Id}, Certification: {tech.CertificationLevel}/5, " +
                    $"Experience: {tech.YearsExperience}yr, Skills: {string.Join(", ", tech.Skills)})");
            }
        }
        else
        {
            contextLines.Add("- No technicians available with required skills");
        }

        contextLines.Add("");
        contextLines.Add("=== PARTS IN INVENTORY ===");

        if (parts.Count > 0)
        {
            foreach (var part in parts.Take(10)) // Limit to top 10
            {
                contextLines.Add(
                    $"- {part.PartNumber}: {part.Name} (In Stock: {part.QuantityInStock}, Cost: ${part.UnitCost})");
            }
        }
        else
        {
            contextLines.Add("- No parts available for this fault type");
        }

        contextLines.Add("");
        contextLines.Add("=== TASK ===");
        contextLines.Add("Generate a comprehensive repair plan as valid JSON.");
        contextLines.Add("Include specific tasks with time estimates, required skills, and safety notes.");
        contextLines.Add("Assign the most qualified available technician if possible.");

        return string.Join(Environment.NewLine, contextLines);
    }

    /// <summary>
    /// Parses the JSON response from the LLM agent into a WorkOrder object.
    /// Handles number-from-string conversion since LLMs may return numbers as strings.
    /// </summary>
    private WorkOrder ParseWorkOrderResponse(string response, DiagnosedFault fault)
    {
        try
        {
            // Trim whitespace
            var trimmed = response.Trim();

            // Attempt to extract JSON if response contains extra text
            if (!trimmed.StartsWith("{"))
            {
                var jsonStart = trimmed.IndexOf('{');
                var jsonEnd = trimmed.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    trimmed = trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }
            }

            _logger.LogDebug("Parsing JSON response: {JsonPreview}", trimmed[..Math.Min(200, trimmed.Length)]);

            // Deserialize with number-from-string handling
            var workOrder = JsonSerializer.Deserialize<WorkOrder>(trimmed, _jsonOptions)
                ?? throw new InvalidOperationException("Deserialized work order is null");

            // Validate critical fields
            if (string.IsNullOrWhiteSpace(workOrder.MachineId))
            {
                workOrder.MachineId = fault.MachineId;
            }

            if (string.IsNullOrWhiteSpace(workOrder.Title))
            {
                workOrder.Title = $"Repair: {fault.FaultType}";
            }

            if (string.IsNullOrWhiteSpace(workOrder.Description))
            {
                workOrder.Description = fault.Description;
            }

            // Set defaults for missing values
            workOrder.Status ??= "open";
            workOrder.Priority ??= MapSeverityToPriority(fault.Severity);
            workOrder.Type ??= "corrective";
            workOrder.EstimatedDuration = Math.Max(workOrder.EstimatedDuration, 30); // At least 30 minutes

            _logger.LogInformation(
                "Successfully parsed work order with {TaskCount} tasks and {PartCount} parts",
                workOrder.Tasks.Count,
                workOrder.PartsUsed.Count);

            return workOrder;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON: {Response}", response);
            throw new InvalidOperationException("LLM response is not valid JSON", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing work order response");
            throw;
        }
    }

    /// <summary>
    /// Populates parts usage information with current cost data from inventory.
    /// </summary>
    private void PopulatePartsUsage(WorkOrder workOrder, List<Part> partsInventory)
    {
        if (workOrder.PartsUsed == null || workOrder.PartsUsed.Count == 0)
        {
            _logger.LogDebug("No parts used in work order");
            return;
        }

        var partLookup = partsInventory.ToDictionary(p => p.PartNumber);
        decimal totalCost = 0;

        foreach (var partUsage in workOrder.PartsUsed)
        {
            if (partLookup.TryGetValue(partUsage.PartNumber, out var part))
            {
                // Update with current cost from inventory
                partUsage.UnitCost = part.UnitCost;
                partUsage.TotalCost = partUsage.Quantity * part.UnitCost;
                partUsage.PartId = part.Id;
                totalCost += partUsage.TotalCost;

                _logger.LogDebug(
                    "Updated part {PartNumber}: Quantity={Qty}, Cost=${Cost}",
                    partUsage.PartNumber,
                    partUsage.Quantity,
                    partUsage.TotalCost);
            }
            else
            {
                _logger.LogWarning("Part {PartNumber} not found in inventory", partUsage.PartNumber);
            }
        }

        // Update total estimated cost (parts only; labor would be added separately)
        workOrder.EstimatedCost = totalCost;
        _logger.LogInformation("Work order parts cost calculated: ${Cost}", totalCost);
    }

    /// <summary>
    /// Maps fault severity to work order priority.
    /// </summary>
    private static string MapSeverityToPriority(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => "medium"
        };
}
