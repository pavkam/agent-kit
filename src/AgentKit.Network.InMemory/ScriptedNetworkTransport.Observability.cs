// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

public sealed partial class ScriptedNetworkTransport
{
    private static readonly Counter<long> _sends = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.NetworkOperationCount,
        unit: "{operation}",
        description: "Number of terminal deterministic network-send outcomes.");

    /// <inheritdoc/>
    public async ValueTask<NetworkSendResult> SendAsync(
        NetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.NetworkSend,
            ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.NetworkOperationId, request.Id.ToString());
        _ = activity?.SetTag(AgentKitTagNames.NetworkStage, "send");
        try
        {
            var result = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result.GetType().Name.ToLowerInvariant();
            if (result is NetworkResponseReceived)
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

    /// <summary>Records one bounded terminal send outcome without destination or request content.</summary>
    /// <param name="outcome">The normalized terminal result kind.</param>
    private static void Record(string outcome) =>
        _sends.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.NetworkStage, "send"),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Emits a content-free terminal scripted-send event.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="outcome">The normalized terminal result kind.</param>
    [LoggerMessage(14110, LogLevel.Debug, "Scripted network send {NetworkOperationId} completed with outcome {Outcome}.")]
    private static partial void LogCompleted(
        ILogger logger,
        NetworkOperationId networkOperationId,
        string outcome);

    /// <summary>Emits an unexpected exception type without exception, destination, or request content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(14111, LogLevel.Error, "Scripted network send {NetworkOperationId} failed with error type {ErrorType}.")]
    private static partial void LogFailed(
        ILogger logger,
        NetworkOperationId networkOperationId,
        string errorType);
}
