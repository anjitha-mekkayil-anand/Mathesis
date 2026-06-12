using Mathesis.Agent.FoundryIq;
using Mathesis.Agent.MultiAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mathesis.Agent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLearningAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.Section));
        services.AddSingleton<FoundryIqGroundingService>();
        services.AddSingleton<FoundryIqIndexSeeder>();
        services.AddSingleton<LearningPipelineOrchestrator>();

        return services;
    }
}
