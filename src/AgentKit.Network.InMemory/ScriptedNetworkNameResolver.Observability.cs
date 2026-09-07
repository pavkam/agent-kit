// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

public sealed partial class ScriptedNetworkNameResolver
{
    private static readonly Counter<long> _queries = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.NetworkOperationCount,
        unit: "{operation}",
        description: "Number of terminal deterministic network-resolution outcomes.");

    /// <inheritdoc/>
    public async ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.NetworkResolve,
            ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.NetworkOperationId, request.Id.ToString());
        _ = activity?.SetTag(AgentKitTagNames.NetworkStage, "resolve");
        try
        {
            var result = await ResolveCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result.GetType().Name.ToLowerInvariant();
            if (result is NetworkResolved)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }

            LogCompleted(_logger, request.Id, outcome);
            Record(outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            LogCompleted(_logger, request.Id, "cancelled");
            Record("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            LogFailed(_logger, request.Id, errorType);
            Record("failed");
            throw;
        }
    }

    /// <summary>Records one bounded terminal resolution outcome without destination content.</summary>
    /// <param name="outcome">The normalized terminal result kind.</param>
    private static void Record(string outcome) =>
        _queries.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.NetworkStage, "resolve"),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Emits a content-free terminal scripted-resolution event.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="outcome">The normalized terminal result kind.</param>
    [LoggerMessage(14100, LogLevel.Debug, "Scripted network resolution {NetworkOperationId} completed with outcome {Outcome}.")]
    private static partial void LogCompleted(
        ILogger logger,
        NetworkOperationId networkOperationId,
        string outcome);

    /// <summary>Emits an unexpected exception type without exception or destination content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(14101, LogLevel.Error, "Scripted network resolution {NetworkOperationId} failed with error type {ErrorType}.")]
    private static partial void LogFailed(
        ILogger logger,
        NetworkOperationId networkOperationId,
        string errorType);
}
