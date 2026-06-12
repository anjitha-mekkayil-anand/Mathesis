using Mathesis.Agent;
using Mathesis.Agent.FoundryIq;
using Mathesis.Agent.MultiAgent;
using Mathesis.Core;
using Mathesis.Readiness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
// Explicit: user secrets load regardless of environment — keys never live in appsettings.
builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddLearningAgents(builder.Configuration);

var host = builder.Build();

var grounding = host.Services.GetRequiredService<FoundryIqGroundingService>();
Console.ForegroundColor = grounding.IsConfigured ? ConsoleColor.Green : ConsoleColor.Yellow;
Console.WriteLine(grounding.StatusLine);
Console.ResetColor();
Console.WriteLine();

// --seed-kb: one-time Foundry IQ index creation + synthetic document upload
if (args.Contains("--seed-kb"))
{
    Console.WriteLine("Seeding Foundry IQ learning knowledge base...");
    var seeder = host.Services.GetRequiredService<FoundryIqIndexSeeder>();
    Console.WriteLine(await seeder.SeedAsync());
    return;
}

var opts = host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
var roster = await LoadRosterAsync(opts.McpWorkingDirectory);

// --readiness [learner]: deterministic readiness report — no LLM, no credentials.
// The pre-filter the agents build on, runnable standalone.
if (args.Contains("--readiness"))
{
    var calculator = new ReadinessCalculator();
    var learnerArg = args.SkipWhile(a => a != "--readiness").Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
    var learners = learnerArg is null
        ? roster.Learners
        : roster.Learners.Where(l => l.LearnerId.Equals(learnerArg, StringComparison.OrdinalIgnoreCase)).ToList();

    Console.WriteLine("Deterministic readiness report (no LLM involved):");
    Console.WriteLine();
    foreach (var learner in learners)
    {
        var certification = roster.FindCertification(learner.TargetCertification);
        if (certification is null)
        {
            Console.WriteLine($"{learner.LearnerId}: targets unknown certification '{learner.TargetCertification}'");
            continue;
        }

        var assessment = calculator.Assess(learner, certification);
        Console.WriteLine($"{learner.LearnerId} ({learner.Role}, target {certification.Id})");
        Console.ForegroundColor = Mathesis.Agent.MultiAgent.Narrate.BandColor(assessment.Band.ToString());
        Console.WriteLine($"  {assessment.Detail}");
        Console.ResetColor();
        Console.WriteLine();
    }
    return;
}

// --eval [N]: consistency harness — N non-interactive pipeline runs per learner;
// reports stability of the deterministic band and agreement on the LLM's
// next-step direction. Additive: exercises the pipeline, never modifies it.
if (args.Contains("--eval"))
{
    var nArg = args.SkipWhile(a => a != "--eval").Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
    var runs = int.TryParse(nArg, out var n) && n > 0 ? n : 3;
    var evalOrchestrator = host.Services.GetRequiredService<LearningPipelineOrchestrator>();

    Console.WriteLine($"Consistency evaluation — {runs} non-interactive pipeline runs per learner:");
    Console.WriteLine();

    var overallAgree = 0;
    var overallTotal = 0;
    foreach (var id in roster.Learners.Select(l => l.LearnerId))
    {
        var bands = new List<string>();
        var directions = new List<string>();
        for (var r = 0; r < runs; r++)
        {
            var res = await evalOrchestrator.RunAsync([id], interactive: false, includeManagerReport: false);
            if (res.Count == 0) continue;
            bands.Add(res[0].Band);
            directions.Add(ClassifyNextStep(res[0].NextStep));
        }
        if (bands.Count == 0) continue;

        var bandStable = bands.Distinct().Count() == 1;
        var top = directions.GroupBy(d => d).OrderByDescending(g => g.Count()).First();
        var agreement = (double)top.Count() / directions.Count * 100;
        overallAgree += top.Count();
        overallTotal += directions.Count;

        Console.ForegroundColor = agreement >= 100 ? ConsoleColor.Green
            : agreement >= 67 ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.WriteLine($"=== {id}: band {(bandStable ? "stable" : "UNSTABLE")} ({bands[0]}) · " +
                          $"next-step direction '{top.Key}' — {agreement:0}% agreement over {directions.Count} runs");
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine($"Overall next-step direction agreement: " +
                      $"{(overallTotal == 0 ? 0 : (double)overallAgree / overallTotal * 100):0}% ({overallAgree}/{overallTotal})");
    return;
}

// Full pipeline: Curator → Planner → (human gate) → Assessor → Manager Insights.
var runAll = args.Contains("--all");
var teamReport = args.Contains("--team") || runAll;
var interactive = !runAll && !Console.IsInputRedirected;

var learnerIds = runAll
    ? roster.Learners.Select(l => l.LearnerId).ToList()
    : [args.FirstOrDefault(a => !a.StartsWith("--")) ?? "EMP-001"];

if (args.Contains("--team") && !runAll)
    learnerIds = []; // --team alone: manager report only

var orchestrator = host.Services.GetRequiredService<LearningPipelineOrchestrator>();
var results = await orchestrator.RunAsync(learnerIds, interactive, teamReport || args.Contains("--team"));

foreach (var result in results)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"=== {result.LearnerId} ({result.CertificationId}) ===");
    Console.ResetColor();
    Console.Write("Readiness:   ");
    Console.ForegroundColor = Mathesis.Agent.MultiAgent.Narrate.BandColor(result.Band);
    Console.WriteLine($"{result.ReadinessScore}/100 ({result.Band})");
    Console.ResetColor();
    Console.WriteLine($"Plan:        {(result.PlanProposed ? "proposed — pending manager approval" : "not needed")}");
    if (result.AssessmentSummary is not null)
    {
        Console.WriteLine($"Assessment:  {result.AssessmentSummary}");
    }
    Console.WriteLine($"Next step:   {result.NextStep}");
    Console.WriteLine();
}

// Buckets a free-text next step into a comparable direction for agreement scoring.
static string ClassifyNextStep(string nextStep)
{
    var s = nextStep.ToLowerInvariant();
    if (s.Contains("book")) return "book-exam";
    if (s.Contains("follow the proposed study plan")) return "follow-plan";
    if (s.Contains("extend") || s.Contains("revis") || s.Contains("reassess") ||
        s.Contains("focus") || s.Contains("more time") || s.Contains("additional"))
        return "revise-reassess";
    return "other";
}

static async Task<RosterConfig> LoadRosterAsync(string mcpWorkingDirectory)
{
    return await RosterConfig.LoadAsync(
        Path.Combine(mcpWorkingDirectory, "learners.json"),
        Path.Combine(mcpWorkingDirectory, "certifications.json"),
        Path.Combine(mcpWorkingDirectory, "learning-history.json"));
}
