using Mathesis.Core;
using Mathesis.Dashboard.Components;
using Mathesis.Data;
using Mathesis.Readiness;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration["Dashboard:DatabasePath"] ?? "mathesis.db";
var learnersPath = builder.Configuration["Dashboard:LearnersPath"] ?? "learners.json";
var certificationsPath = builder.Configuration["Dashboard:CertificationsPath"] ?? "certifications.json";
var historyPath = builder.Configuration["Dashboard:HistoryPath"] ?? "learning-history.json";

var roster = await RosterConfig.LoadAsync(learnersPath, certificationsPath, historyPath);

var store = new SqliteLearningStore($"Data Source={dbPath}");
await store.InitializeAsync();

builder.Services.AddSingleton<IApprovalQueue>(store);
builder.Services.AddSingleton(roster);
builder.Services.AddSingleton<ReadinessCalculator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
