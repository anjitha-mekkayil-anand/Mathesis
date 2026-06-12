using ModelContextProtocol.Client;
using OpenAI.Chat;
using System.Text.Json;

namespace Mathesis.Agent.MultiAgent;

/// <summary>One tool call the model made during a loop, with stringified args.</summary>
internal sealed record CapturedToolCall(string Name, IReadOnlyDictionary<string, string> Args);

/// <summary>
/// What a loop run produced: the model's final prose, whether that prose was
/// actually a plain-text tool-call payload (Mistral quirk), and every tool call made.
/// </summary>
internal sealed record LoopOutcome(string FinalText, bool FinalTextWasToolJson, IReadOnlyList<CapturedToolCall> ToolCalls);

/// <summary>
/// The shared LLM-with-tools loop used by every agent in the pipeline.
/// Each agent gets its own system prompt and its own subset of the MCP tools —
/// the tool surface per agent is the role boundary.
/// </summary>
internal static class FoundryChatLoop
{
    public static async Task<LoopOutcome> RunAsync(
        ChatClient chatClient,
        McpClientWrapper mcp,
        IList<McpClientTool> mcpTools,
        IReadOnlySet<string> allowedTools,
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        var chatOptions = new ChatCompletionOptions();
        foreach (var tool in mcpTools.Where(t => allowedTools.Contains(t.Name)))
        {
            chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description ?? "",
                BinaryData.FromString(tool.JsonSchema.GetRawText())));
        }

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userMessage)
        };

        var captured = new List<CapturedToolCall>();

        while (true)
        {
            var response = await chatClient.CompleteChatAsync(messages, chatOptions, ct);
            var completion = response.Value;

            messages.Add(new AssistantChatMessage(completion));

            if (completion.FinishReason == ChatFinishReason.Stop)
            {
                var rawText = completion.Content.FirstOrDefault()?.Text ?? "";

                // Mistral-small sometimes emits tool calls as plain JSON text instead
                // of a proper ToolCalls finish reason. Detect, execute them anyway
                // (so the store still gets written), and tell the caller the text
                // wasn't prose.
                var wasToolJson = false;
                if (rawText.TrimStart().StartsWith("[{") && rawText.Contains("\"name\""))
                {
                    wasToolJson = await TryExecutePlainTextToolCallsAsync(
                        rawText, allowedTools, mcp, captured, ct);
                }

                return new LoopOutcome(rawText, wasToolJson, captured);
            }

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                foreach (var toolCall in completion.ToolCalls)
                {
                    var argsJson = toolCall.FunctionArguments.ToString();
                    var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? [];

                    captured.Add(new CapturedToolCall(
                        toolCall.FunctionName,
                        args.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")));

                    var result = await mcp.CallToolAsync(toolCall.FunctionName, args, ct);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                }
            }
        }
    }

    private static async Task<bool> TryExecutePlainTextToolCallsAsync(
        string json,
        IReadOnlySet<string> allowedTools,
        McpClientWrapper mcp,
        List<CapturedToolCall> captured,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var executedAny = false;
            foreach (var call in doc.RootElement.EnumerateArray())
            {
                if (!call.TryGetProperty("name", out var nameProp) ||
                    nameProp.GetString() is not { } name ||
                    !allowedTools.Contains(name) ||
                    !call.TryGetProperty("arguments", out var argsProp))
                {
                    continue;
                }

                var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsProp.GetRawText()) ?? [];
                captured.Add(new CapturedToolCall(
                    name,
                    args.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")));

                await mcp.CallToolAsync(name, args, ct);
                executedAny = true;
            }
            return executedAny;
        }
        catch (JsonException)
        {
            return false; // looked like tool JSON but wasn't — treat as prose
        }
    }
}
