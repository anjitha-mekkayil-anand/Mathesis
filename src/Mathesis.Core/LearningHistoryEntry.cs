namespace Mathesis.Core;

/// <summary>
/// One historical (synthetic) learner outcome, loaded from learning-history.json.
/// The Study Plan Generator uses these prior outcomes to recommend realistic
/// study hours — the "synthetic historical learner outcomes" the challenge
/// spec suggests grounding the planner in.
/// </summary>
public sealed class LearningHistoryEntry
{
    public string LearnerId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Certification { get; set; } = "";
    public double PracticeScoreAvg { get; set; }
    public double HoursStudied { get; set; }
    public string ExamOutcome { get; set; } = "";
}
