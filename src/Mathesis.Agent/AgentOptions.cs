using Mathesis.Agent.AzureFoundry;
using Mathesis.Agent.FoundryIq;

namespace Mathesis.Agent;

public sealed class AgentOptions
{
    public const string Section = "LearningAgents";

    /// <summary>Path to the Mathesis.Mcp executable.</summary>
    public string McpServerPath { get; set; } = "";

    /// <summary>Working directory for the MCP server process (where the roster JSON and mathesis.db live).</summary>
    public string McpWorkingDirectory { get; set; } = "";

    public AzureFoundryAgentOptions AzureFoundry { get; set; } = new();
    public FoundryIqOptions FoundryIq { get; set; } = new();
}
