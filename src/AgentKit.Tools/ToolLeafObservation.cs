// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Observes one built-in tool leaf invocation through the shared activity, meter, and a package-owned log event.</summary>
/// <remarks>
/// <para>
/// Every invocation runs under one GenAI-compatible <c>execute_tool</c> activity tagged with the stable tool identity,
/// increments one bounded outcome counter, and writes one content-free log event through the owning package's
/// <see cref="ToolLeafLogEvents"/>. Tool arguments, results, paths, and exception messages never enter a signal.
/// </para>
/// <para>
/// Observation is best-effort: listener, meter, and logger failures are contained and never change the invocation
/// result or the exception the tool raised. Cancellation and exceptions are recorded and then rethrown unchanged.
/// </para>
/// </remarks>
public static class ToolLeafObservation
{
    private static readonly Counter<long> _operationCounter =
        AgentKitDiagnostics.Metrics.CreateCounter<long>(
            AgentKitMetricNames.ToolLeafOperationCount,
            unit: "{operation}",
            description: "Number of terminal built-in tool leaf invocations.");

    /// <summary>Runs one tool invocation under the shared observation wrapper.</summary>
    /// <param name="toolId">The stable tool identity; it must not be default.</param>
    /// <param name="callId">The model-requested call identity correlated into the log event.</param>
    /// <param name="logger">The logger owned by the calling tool package.</param>
    /// <param name="events">The calling package's bound terminal log events.</param>
    /// <param name="invocation">The tool's own invocation work, started exactly once.</param>
    /// <returns>The unchanged result of <paramref name="invocation"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="toolId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/>, <paramref name="events"/>, or <paramref name="invocation"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The invocation was cancelled; it is rethrown after being recorded.</exception>
    /// <remarks>An exception thrown by <paramref name="invocation"/> is recorded as <c>faulted</c> and rethrown unchanged.</remarks>
    public static async ValueTask<ToolInvocationResult> RunAsync(
        ToolId toolId,
        ToolCallId callId,
        ILogger logger,
        ToolLeafLogEvents events,
        Func<ValueTask<ToolInvocationResult>> invocation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default, nameof(toolId));
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(invocation);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.ExecuteTool,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ExecuteTool },
                { AgentKitTagNames.ToolId, toolId.ToString() },
            });
        try
        {
            var result = await invocation().ConfigureAwait(false);
            var outcome = result.Outcome.Kind == ToolCallOutcomeKind.Success ? "succeeded" : "rejected";
            Observe(() =>
            {
                _ = scope.Activity?.SetStatus(ActivityStatusCode.Ok);
                _ = scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome);
                Record(outcome);
                events.Completed(logger, toolId, callId, outcome);
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            Observe(() =>
            {
                scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException));
                Record("cancelled");
                events.Cancelled(logger, toolId, callId);
            });
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().Name;
            Observe(() =>
            {
                scope.Activity.SetFailed("faulted", errorType);
                Record("faulted");
                events.Faulted(logger, toolId, callId, errorType);
            });
            throw;
        }
    }

    private static void Record(string outcome) =>
        _operationCounter.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    private static void Observe(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Observation failure never changes the invocation result or exception.
        }
    }
}
