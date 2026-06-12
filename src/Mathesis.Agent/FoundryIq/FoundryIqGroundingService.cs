using System.Text;
using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Options;

namespace Mathesis.Agent.FoundryIq;

/// <summary>
/// The Foundry IQ knowledge retrieval layer. Before the agents reason, queries the
/// Azure AI Search learning knowledge base for content relevant to the certification
/// and the learner's weak domains, and returns a cited context block for the system
/// prompt. Grounded plans and assessments cite verified documents instead of relying
/// on pure LLM inference.
///
/// Fail-open by design: unconfigured OR unreachable Search degrades to an ungrounded
/// run with a loud console warning — retrieval problems never kill an analysis.
/// </summary>
public sealed class FoundryIqGroundingService(IOptions<AgentOptions> options)
{
    public bool IsConfigured =>
        !string.IsNullOrEmpty(options.Value.FoundryIq.SearchEndpoint) &&
        !string.IsNullOrEmpty(options.Value.FoundryIq.ApiKey);

    /// <summary>One-line status for startup output — grounding should be visible, never silent.</summary>
    public string StatusLine => IsConfigured
        ? $"[Foundry IQ] grounded: index '{options.Value.FoundryIq.IndexName}'"
        : "[Foundry IQ] ungrounded — Search endpoint not configured (agents run without citations)";

    /// <summary>
    /// Returns a cited grounded-knowledge block, or null when grounding is
    /// unconfigured, unreachable, or the index has no relevant documents.
    /// </summary>
    public async Task<string?> GetGroundedContextAsync(
        string certificationId,
        IReadOnlyCollection<string> weakDomains,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        var opts = options.Value.FoundryIq;

        try
        {
            var client = new SearchClient(
                new Uri(opts.SearchEndpoint), opts.IndexName, new AzureKeyCredential(opts.ApiKey));

            var query = weakDomains.Count > 0
                ? $"{certificationId} {string.Join(' ', weakDomains)} study guidance"
                : $"{certificationId} certification study guidance";

            var response = await client.SearchAsync<KnowledgeDocument>(
                query, new SearchOptions { Size = 3 }, ct);

            var sb = new StringBuilder();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                var doc = result.Document;
                sb.AppendLine($"--- {doc.Title} (Source: {doc.Source}) ---");
                sb.AppendLine(doc.Content.Trim());
                sb.AppendLine();
            }

            if (sb.Length == 0)
                return null;

            return
                "[GROUNDED KNOWLEDGE — Source: Foundry IQ / Azure AI Search]\n" +
                "The following synthetic learning documents were retrieved from the approved " +
                "knowledge base. Ground your plans, questions, and recommendations in these " +
                "documents, and cite the source document name.\n\n" +
                sb.ToString().TrimEnd() +
                "\n[/GROUNDED KNOWLEDGE]";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail open: a dead retrieval layer must never kill the analysis.
            Console.Error.WriteLine(
                $"[Foundry IQ] WARNING: retrieval failed — continuing ungrounded. {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
