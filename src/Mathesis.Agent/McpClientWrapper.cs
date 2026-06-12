using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mathesis.Agent;

public sealed class McpClientWrapper : IAsyncDisposable
{
    private readonly McpClient _client;

    private McpClientWrapper(McpClient client) => _client = client;

    public static async Task<McpClientWrapper> CreateAsync(
        string serverPath,
        string workingDirectory,
        CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = serverPath,
            WorkingDirectory = workingDirectory
        });

        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        return new McpClientWrapper(client);
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
        => await _client.ListToolsAsync(cancellationToken: ct);

    public async Task<string> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken ct = default)
    {
        var result = await _client.CallToolAsync(toolName, arguments, cancellationToken: ct);
        return string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text));
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
