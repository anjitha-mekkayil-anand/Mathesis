using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;

namespace Mathesis.Agent.FoundryIq;

/// <summary>
/// One synthetic learning knowledge document in the Azure AI Search index.
/// Field attributes drive index creation via <c>FieldBuilder</c>.
/// </summary>
public sealed class KnowledgeDocument
{
    [SimpleField(IsKey = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [SearchableField]
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [SearchableField]
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("certification")]
    public string Certification { get; set; } = "";

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "";

    [SimpleField]
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";
}
