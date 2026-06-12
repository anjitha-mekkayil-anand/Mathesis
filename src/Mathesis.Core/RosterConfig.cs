using System.Text.Json;

namespace Mathesis.Core;

/// <summary>
/// The whole roster: learners, certifications, and historical outcomes — all
/// loaded from JSON at startup. Config-driven by design: adding a learner or a
/// certification is a config edit, not a code change.
/// </summary>
public sealed class RosterConfig
{
    public List<LearnerConfig> Learners { get; set; } = [];
    public List<CertificationConfig> Certifications { get; set; } = [];
    public List<LearningHistoryEntry> History { get; set; } = [];

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static async Task<RosterConfig> LoadAsync(
        string learnersPath, string certificationsPath, string historyPath, CancellationToken ct = default)
    {
        var roster = new RosterConfig
        {
            Learners = await LoadFileAsync<List<LearnerConfig>>(learnersPath, ct),
            Certifications = await LoadFileAsync<List<CertificationConfig>>(certificationsPath, ct),
            History = await LoadFileAsync<List<LearningHistoryEntry>>(historyPath, ct)
        };
        return roster;
    }

    public LearnerConfig? FindLearner(string learnerId) =>
        Learners.FirstOrDefault(l => l.LearnerId.Equals(learnerId, StringComparison.OrdinalIgnoreCase));

    public CertificationConfig? FindCertification(string certificationId) =>
        Certifications.FirstOrDefault(c => c.Id.Equals(certificationId, StringComparison.OrdinalIgnoreCase));

    private static async Task<T> LoadFileAsync<T>(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<T>(json, _json)
            ?? throw new InvalidOperationException($"Could not parse '{path}'.");
    }
}
