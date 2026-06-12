namespace Mathesis.Core;

/// <summary>
/// One certification definition, loaded from certifications.json. This is the
/// Fabric IQ concept layer: the semantic seed connecting certification, skills,
/// domain weights, and recommended effort — the ontology agents reason over.
/// </summary>
public sealed class CertificationConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<CertificationDomain> Domains { get; set; } = [];
    public double RecommendedHours { get; set; }

    /// <summary>Practice score (0–100) a learner should reach before booking.</summary>
    public double PassThreshold { get; set; } = 75;
}

/// <summary>A weighted skill domain within a certification.</summary>
public sealed class CertificationDomain
{
    public string Name { get; set; } = "";

    /// <summary>Relative exam weight, 0–1. Domain weights sum to ~1 per certification.</summary>
    public double Weight { get; set; }
}
