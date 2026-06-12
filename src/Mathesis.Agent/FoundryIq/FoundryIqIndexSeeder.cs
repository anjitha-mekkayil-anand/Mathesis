using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Options;

namespace Mathesis.Agent.FoundryIq;

/// <summary>
/// One-time setup: creates (or updates) the Foundry IQ learning knowledge index and
/// uploads the synthetic documents from the knowledge base folder.
/// Invoked via <c>dotnet run --project src/Mathesis.Runner -- --seed-kb</c>.
/// </summary>
public sealed class FoundryIqIndexSeeder(IOptions<AgentOptions> options)
{
    private static readonly (string File, string Title, string Certification, string Topic, string Source)[] Documents =
    [
        ("az204-study-guide.md", "AZ-204 Certification Enablement Guide (Synthetic)",
            "AZ-204", "study-guide", "Engineering Certification Enablement Guide v1.2 (Synthetic)"),
        ("az400-study-guide.md", "AZ-400 Certification Enablement Guide (Synthetic)",
            "AZ-400", "study-guide", "Engineering Certification Enablement Guide v1.2 (Synthetic)"),
        ("dp203-study-guide.md", "DP-203 Certification Enablement Guide (Synthetic)",
            "DP-203", "study-guide", "Engineering Certification Enablement Guide v1.2 (Synthetic)"),
        ("study-patterns-guide.md", "Recommended Study Patterns (Synthetic)",
            "All", "study-patterns", "Learning & Development Playbook §3 (Synthetic)"),
        ("workload-insights-report.md", "Workload and Learning Correlation Report (Synthetic)",
            "All", "workload-insights", "Quarterly Workload Insights Report (Synthetic)"),
        ("team-learning-report.md", "Quarterly Team Learning Performance Summary (Synthetic)",
            "All", "team-performance", "Quarterly Learning Performance Summary (Synthetic)"),
        ("assessment-writing-guide.md", "Assessment Question Writing Guide (Synthetic)",
            "All", "assessment-guide", "Learning & Development Playbook §5 (Synthetic)")
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        var opts = options.Value.FoundryIq;

        if (string.IsNullOrEmpty(opts.SearchEndpoint) || string.IsNullOrEmpty(opts.ApiKey))
            throw new InvalidOperationException(
                "FoundryIq:SearchEndpoint / ApiKey not set. Set via user secrets: " +
                "dotnet user-secrets set \"LearningAgents:FoundryIq:SearchEndpoint\" \"<endpoint>\" --project src/Mathesis.Runner");

        if (string.IsNullOrEmpty(opts.KnowledgeBasePath) || !Directory.Exists(opts.KnowledgeBasePath))
            throw new InvalidOperationException(
                $"FoundryIq:KnowledgeBasePath '{opts.KnowledgeBasePath}' not found. " +
                "Point it at the learning-kb folder in appsettings.json.");

        var indexClient = new SearchIndexClient(
            new Uri(opts.SearchEndpoint), new AzureKeyCredential(opts.ApiKey));

        var index = new SearchIndex(opts.IndexName)
        {
            Fields = new FieldBuilder().Build(typeof(KnowledgeDocument))
        };
        await indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: ct);

        var docs = Documents.Select(d => new KnowledgeDocument
        {
            Id = Path.GetFileNameWithoutExtension(d.File),
            Title = d.Title,
            Content = File.ReadAllText(Path.Combine(opts.KnowledgeBasePath, d.File)),
            Certification = d.Certification,
            Topic = d.Topic,
            Source = d.Source
        }).ToList();

        var searchClient = indexClient.GetSearchClient(opts.IndexName);
        await searchClient.UploadDocumentsAsync(docs, cancellationToken: ct);

        return $"Index '{opts.IndexName}' created/updated — {docs.Count} synthetic documents uploaded.";
    }
}
