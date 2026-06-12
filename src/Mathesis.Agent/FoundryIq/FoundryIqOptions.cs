namespace Mathesis.Agent.FoundryIq;

/// <summary>
/// Foundry IQ knowledge retrieval settings (Azure AI Search). Blank endpoint or
/// key means grounding is skipped — the agents run ungrounded, nothing fails.
/// </summary>
public sealed class FoundryIqOptions
{
    public string SearchEndpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string IndexName { get; set; } = "mathesis-learning-kb";

    /// <summary>Folder containing the synthetic learning knowledge documents to seed.</summary>
    public string KnowledgeBasePath { get; set; } = "";
}
