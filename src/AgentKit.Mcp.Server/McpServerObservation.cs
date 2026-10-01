// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using ModelContextProtocol.Protocol;

/// <summary>Observes inbound MCP requests and tool calls through the shared AgentKit activity, meter, and logging surfaces.</summary>
/// <remarks>
/// Observation is best-effort: listener, meter, and logger failures are contained and never change the semantic response
/// or exception the wrapped operation produces. Tool arguments, results, and peer text other than a bounded tool name never
/// enter a signal, and only the bounded operation and outcome become metric dimensions.
/// </remarks>
internal sealed class McpServerObservation(ILogger logger)
{
    /// <summary>The maximum number of characters of a peer-supplied tool name retained in spans and logs.</summary>
    internal const int MaximumToolNameLength = 128;

    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.McpServerOperationCount,
        unit: "{operation}",
        description: "Number of terminal MCP server request and tool-call outcomes.");

    private readonly ILogger _logger = logger;

    /// <summary>Classifies one terminal MCP response into a bounded outcome label.</summary>
    /// <param name="response">The terminal response.</param>
    /// <returns>A bounded, content-free outcome label.</returns>
    internal static string Classify(McpResponse response) =>
        response switch
        {
            McpResponseSucceeded => "succeeded",
            McpResponseDenied => "denied",
            McpResponseUnsupportedCapability => "unsupported",
            McpResponseCancelled => "cancelled",
            McpResponseProtocolFailed => "protocol_failed",
            McpResponseUnknownEffect => "unknown_effect",
            _ => "unclassified",
        };

    /// <summary>Classifies one SDK tool-call result into a bounded outcome label.</summary>
    /// <param name="result">The terminal SDK result.</param>
    /// <returns><c>error</c> when the result reports a tool error; otherwise <c>succeeded</c>.</returns>
    internal static string ClassifyToolResult(CallToolResult result) =>
        result.IsError == true ? "error" : "succeeded";

    /// <summary>Maps an MCP request to its bounded operation label.</summary>
    /// <param name="request">The inbound request.</param>
    /// <returns>A bounded operation label.</returns>
    internal static string OperationOf(McpRequest request) =>
        request switch
        {
            McpToolsCallRequest => "tools.call",
            _ => "unsupported",
        };

    /// <summary>Truncates a peer-supplied tool name so unbounded peer text cannot become a signal value.</summary>
    /// <param name="toolName">The peer-supplied tool name.</param>
    /// <returns>At most <see cref="MaximumToolNameLength"/> characters.</returns>
    internal static string BoundToolName(string toolName) =>
        toolName.Length <= MaximumToolNameLength ? toolName : toolName[..MaximumToolNameLength];

    /// <summary>Runs one inbound MCP request under an <c>mcp.server.request</c> activity.</summary>
    /// <typeparam name="TResult">The terminal result type.</typeparam>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="operation">The bounded operation label.</param>
    /// <param name="action">The request work.</param>
    /// <param name="classify">Maps the terminal result to a bounded outcome label.</param>
    /// <returns>The unchanged result of <paramref name="action"/>.</returns>
    internal ValueTask<TResult> ObserveRequestAsync<TResult>(
        string serverKey,
        string operation,
        Func<ValueTask<TResult>> action,
        Func<TResult, string> classify) =>
        RunAsync(AgentKitActivityNames.McpServerRequest, serverKey, operation, toolName: null, action, classify);

    /// <summary>Runs one inbound MCP tool call under an <c>mcp.server.tool.call</c> activity.</summary>
    /// <typeparam name="TResult">The terminal result type.</typeparam>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="toolName">The peer-supplied tool name; it is bounded before use.</param>
    /// <param name="action">The tool-call work.</param>
    /// <param name="classify">Maps the terminal result to a bounded outcome label.</param>
    /// <returns>The unchanged result of <paramref name="action"/>.</returns>
    internal ValueTask<TResult> ObserveToolCallAsync<TResult>(
        string serverKey,
        string toolName,
        Func<ValueTask<TResult>> action,
        Func<TResult, string> classify) =>
        RunAsync(AgentKitActivityNames.McpServerToolCall, serverKey, "tools.call", BoundToolName(toolName), action, classify);

    private async ValueTask<TResult> RunAsync<TResult>(
        string activityName,
        string serverKey,
        string operation,
        string? toolName,
        Func<ValueTask<TResult>> action,
        Func<TResult, string> classify)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(classify);
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.McpServerKey, serverKey },
            { AgentKitTagNames.McpOperation, operation },
        };
        if (toolName is not null)
        {
            tags.Add(AgentKitTagNames.ToolName, toolName);
        }

        using var scope = AgentKitActivityScope.Start(activityName, ActivityKind.Server, tags);
        try
        {
            var result = await action().ConfigureAwait(false);
            var outcome = classify(result);
            Observe(() =>
            {
                if (outcome == "succeeded")
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    _ = scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome);
                    _ = scope.Activity?.SetStatus(ActivityStatusCode.Error);
                }

                Record(operation, outcome);
                if (toolName is null)
                {
                    McpServerLog.RequestCompleted(_logger, serverKey, operation, outcome);
                }
                else
                {
                    McpServerLog.ToolCallCompleted(_logger, serverKey, toolName, outcome);
                }
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            Observe(() =>
            {
                scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException));
                Record(operation, "cancelled");
                if (toolName is null)
                {
                    McpServerLog.RequestCancelled(_logger, serverKey, operation);
                }
                else
                {
                    McpServerLog.ToolCallCancelled(_logger, serverKey, toolName);
                }
            });
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().Name;
            Observe(() =>
            {
                scope.Activity.SetFailed("faulted", errorType);
                Record(operation, "faulted");
                if (toolName is null)
                {
                    McpServerLog.RequestFailed(_logger, serverKey, operation, errorType);
                }
                else
                {
                    McpServerLog.ToolCallFailed(_logger, serverKey, toolName, errorType);
                }
            });
            throw;
        }
    }

    private static void Record(string operation, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.McpOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    private static void Observe(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Observation failure never changes the semantic response.
        }
    }
}
