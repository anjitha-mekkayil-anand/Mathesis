namespace Mathesis.Agent.AzureFoundry;

public sealed class AzureFoundryAgentOptions
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string DeploymentName { get; set; } = "";
}
