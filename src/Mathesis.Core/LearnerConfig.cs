namespace Mathesis.Core;

/// <summary>
/// One learner in the roster, loaded from learners.json. Synthetic data only —
/// identifiers are deliberately fabricated (EMP-001 style, per challenge guidance).
/// Work-activity signals (meeting/focus hours) are the Work IQ concept layer:
/// contextual inputs that drive study-window and scheduling decisions.
/// </summary>
public sealed class LearnerConfig
{
    public string LearnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string TargetCertification { get; set; } = "";

    // Work IQ concept signals (synthetic)
    public double MeetingHoursPerWeek { get; set; }
    public double FocusHoursPerWeek { get; set; }
    public string PreferredLearningSlot { get; set; } = "";

    // Progress signals
    public double StudyHoursLogged { get; set; }

    /// <summary>Latest practice assessment score, 0–100. Null = not yet assessed.</summary>
    public double? PracticeScore { get; set; }

    /// <summary>Self-rated confidence per skill domain, 1–5.</summary>
    public Dictionary<string, int> DomainRatings { get; set; } = [];
}
