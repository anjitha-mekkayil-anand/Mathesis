using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mathesis.Core;
using Mathesis.Data;
using Mathesis.Readiness;
using ModelContextProtocol.Server;

namespace Mathesis.Mcp;

/// <summary>
/// The seven MCP tools the learning agents can call: four reads and three writes.
/// Deliberately minimal — and note what is absent: there is no approve_plan,
/// no mark_certified, no update_rating tool. Agents can analyse and recommend;
/// activating a study plan is the manager's decision, made in the dashboard.
/// The tool surface is the guardrail.
/// </summary>
[McpServerToolType]
public sealed class MathesisTools(
    RosterConfig roster,
    ILearningStore store,
    ReadinessCalculator calculator)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "list_learners")]
    [Description("Lists all learners in the roster with their role, target certification, and work-activity signals (meeting/focus hours, preferred learning slot).")]
    public string ListLearners()
    {
        var result = roster.Learners.Select(l => new
        {
            learner_id = l.LearnerId,
            name = l.Name,
            role = l.Role,
            target_certification = l.TargetCertification,
            meeting_hours_per_week = l.MeetingHoursPerWeek,
            focus_hours_per_week = l.FocusHoursPerWeek,
            preferred_learning_slot = l.PreferredLearningSlot
        });
        return JsonSerializer.Serialize(result, _json);
    }

    [McpServerTool(Name = "get_learner_readiness")]
    [Description("Returns the deterministic readiness assessment for a learner against their target certification: score (0-100), band (Ready / Borderline / NotReady), component breakdown, weak domains, study hours, and practice score. Call this first when assessing a learner.")]
    public string GetLearnerReadiness(
        [Description("Learner ID, e.g. 'EMP-001'")] string learner_id)
    {
        var learner = roster.FindLearner(learner_id);
        if (learner is null)
            return JsonSerializer.Serialize(new { error = $"Unknown learner '{learner_id}'." }, _json);

        var certification = roster.FindCertification(learner.TargetCertification);
        if (certification is null)
            return JsonSerializer.Serialize(new { error = $"Learner '{learner_id}' targets unknown certification '{learner.TargetCertification}'." }, _json);

        var assessment = calculator.Assess(learner, certification);
        return JsonSerializer.Serialize(new
        {
            learner_id = assessment.LearnerId,
            certification_id = assessment.CertificationId,
            readiness_score = assessment.Score,
            band = assessment.Band.ToString(),
            domain_component = assessment.DomainComponent,
            hours_component = assessment.HoursComponent,
            practice_component = assessment.PracticeComponent,
            weak_domains = assessment.GapDomains.Select(d => new { d.Name, d.Weight }),
            study_hours_logged = learner.StudyHoursLogged,
            recommended_hours = certification.RecommendedHours,
            practice_score = learner.PracticeScore,
            pass_threshold = certification.PassThreshold,
            detail = assessment.Detail
        }, _json);
    }

    [McpServerTool(Name = "get_certification")]
    [Description("Returns a certification's skill domains with exam weights, recommended study hours, and the practice pass threshold.")]
    public string GetCertification(
        [Description("Certification ID, e.g. 'AZ-204'")] string certification_id)
    {
        var certification = roster.FindCertification(certification_id);
        if (certification is null)
            return JsonSerializer.Serialize(new { error = $"Unknown certification '{certification_id}'." }, _json);

        return JsonSerializer.Serialize(new
        {
            id = certification.Id,
            name = certification.Name,
            domains = certification.Domains.Select(d => new { d.Name, d.Weight }),
            recommended_hours = certification.RecommendedHours,
            pass_threshold = certification.PassThreshold
        }, _json);
    }

    [McpServerTool(Name = "get_learning_history")]
    [Description("Returns historical (synthetic) learner outcomes for a certification: practice scores, hours studied, and exam results. Use this to ground study-hour recommendations in what actually worked.")]
    public string GetLearningHistory(
        [Description("Certification ID, e.g. 'AZ-204'. Empty returns all history.")] string certification_id = "")
    {
        var entries = string.IsNullOrWhiteSpace(certification_id)
            ? roster.History
            : roster.History.Where(h => h.Certification.Equals(certification_id, StringComparison.OrdinalIgnoreCase)).ToList();

        var passes = entries.Where(e => e.ExamOutcome.Equals("Pass", StringComparison.OrdinalIgnoreCase)).ToList();

        return JsonSerializer.Serialize(new
        {
            certification_id = string.IsNullOrWhiteSpace(certification_id) ? "all" : certification_id,
            entries = entries.Select(e => new
            {
                learner_id = e.LearnerId,
                role = e.Role,
                certification = e.Certification,
                practice_score_avg = e.PracticeScoreAvg,
                hours_studied = e.HoursStudied,
                exam_outcome = e.ExamOutcome
            }),
            pass_count = passes.Count,
            fail_count = entries.Count - passes.Count,
            avg_hours_when_passed = passes.Count == 0 ? (double?)null : Math.Round(passes.Average(e => e.HoursStudied), 1),
            avg_practice_when_passed = passes.Count == 0 ? (double?)null : Math.Round(passes.Average(e => e.PracticeScoreAvg), 1)
        }, _json);
    }

    [McpServerTool(Name = "propose_study_plan")]
    [Description("Records a proposed study plan for a learner. The plan enters the manager approval queue as 'pending' — it does NOT activate until a human approves it. Include the concrete weekly schedule and the rationale grounded in the learner's work signals and cited content.")]
    public async Task<string> ProposeStudyPlanAsync(
        [Description("Learner ID")] string learner_id,
        [Description("Certification ID the plan targets")] string certification_id,
        [Description("The concrete study plan: weekly schedule, milestones, content per domain")] string plan,
        [Description("Why this plan fits: work signals used, historical outcomes referenced, cited sources")] string rationale)
    {
        await store.RecordStudyPlanAsync(learner_id, certification_id, plan, rationale);
        return $"Study plan for '{learner_id}' ({certification_id}) recorded — status: pending manager approval.";
    }

    [McpServerTool(Name = "record_assessment")]
    [Description("Records a readiness assessment for a learner: the grounded, cited practice questions generated and the evaluation of the learner's readiness. Call after generating assessment questions.")]
    public async Task<string> RecordAssessmentAsync(
        [Description("Learner ID")] string learner_id,
        [Description("Certification ID")] string certification_id,
        [Description("The practice questions generated, each with its cited source document")] string questions,
        [Description("Evaluation summary: readiness interpretation against the pass threshold")] string evaluation)
    {
        await store.RecordAssessmentAsync(learner_id, certification_id, questions, evaluation);
        return $"Assessment for '{learner_id}' ({certification_id}) recorded.";
    }

    [McpServerTool(Name = "recommend_next_step")]
    [Description("Records the final next-step recommendation for a learner (e.g. 'book the exam', 'two more weeks on Domain X then reassess'). Call this as your final tool call.")]
    public async Task<string> RecommendNextStepAsync(
        [Description("Learner ID")] string learner_id,
        [Description("The specific recommended next step with reasoning")] string recommendation,
        [Description("The readiness score (0-100) this recommendation is based on")] double readiness_score)
    {
        await store.RecordNextStepAsync(learner_id, recommendation, readiness_score);
        return $"Next step recorded for '{learner_id}' (readiness {readiness_score}): {recommendation}";
    }
}
