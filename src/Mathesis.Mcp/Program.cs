using Mathesis.Core;
using Mathesis.Data;
using Mathesis.Mcp;
using Mathesis.Readiness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// stdout is reserved for the MCP protocol — redirect all Console output to stderr.
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);

var dbPath = builder.Configuration["Mcp:DatabasePath"] ?? "mathesis.db";
var learnersPath = builder.Configuration["Mcp:LearnersPath"] ?? "learners.json";
var certificationsPath = builder.Configuration["Mcp:CertificationsPath"] ?? "certifications.json";
var historyPath = builder.Configuration["Mcp:HistoryPath"] ?? "learning-history.json";

var store = new SqliteLearningStore($"Data Source={dbPath}");
await store.InitializeAsync();

var roster = await RosterConfig.LoadAsync(learnersPath, certificationsPath, historyPath);

builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<ILearningStore>(store);
builder.Services.AddSingleton<IApprovalQueue>(store);
builder.Services.AddSingleton(roster);
builder.Services.AddSingleton<ReadinessCalculator>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MathesisTools>();

var host = builder.Build();
await host.RunAsync();

await store.DisposeAsync();
