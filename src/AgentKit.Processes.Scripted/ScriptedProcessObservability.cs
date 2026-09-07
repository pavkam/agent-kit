// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Coordinates content-free Microsoft diagnostics for deterministic process stages.</summary>
internal static partial class ScriptedProcessObservability
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ProcessOperationCount);

    /// <summary>Runs one deterministic process stage under the same causal contract as the operating-system adapter.</summary>
    /// <typeparam name="TResult">The typed stage result.</typeparam>
    /// <param name="logger">The component logger receiving terminal events.</param>
    /// <param name="activityName">The stable shared activity name.</param>
    /// <param name="stage">The bounded process stage.</param>
    /// <param name="operationId">The process operation identity.</param>
    /// <param name="securityRequestId">The authorizing request identity when the stage consumes a grant.</param>
    /// <param name="action">The semantic deterministic stage.</param>
    /// <param name="classify">Maps the result to its bounded status name.</param>
    /// <param name="isSuccessful">Determines whether the typed result completed the stage successfully.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The unchanged typed stage result.</returns>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        string activityName,
        string stage,
        ProcessOperationId operationId,
        SecurityRequestId? securityRequestId,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, string> classify,
        Func<TResult, bool> isSuccessful,
        CancellationToken cancellationToken)
    {
        using var activity = AgentKitDiagnostics.Activities.StartActivity(activityName);
        _ = activity?.SetTag(AgentKitTagNames.ProcessOperationId, operationId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.ProcessStage, stage);
        _ = activity?.SetTag(AgentKitTagNames.SecurityRequestId, securityRequestId?.ToString());
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            var outcome = classify(result).ToLowerInvariant();
            if (isSuccessful(result))
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, classify(result));
            }

            Completed(logger, stage, operationId, securityRequestId, outcome);
            Record(stage, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            Completed(logger, stage, operationId, securityRequestId, "cancelled");
            Record(stage, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            Failed(
                logger,
                stage,
                operationId,
                securityRequestId,
                exception.GetType().FullName ?? exception.GetType().Name);
            Record(stage, "failed");
            throw;
        }
    }

    private static void Record(string stage, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ProcessStage, stage),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one deterministic terminal result without process input or output content.</summary>
    /// <param name="logger">The logger receiving the event.</param>
    /// <param name="stage">The bounded process stage.</param>
    /// <param name="processOperationId">The process operation identity.</param>
    /// <param name="securityRequestId">The authorizing request identity when available.</param>
    /// <param name="outcome">The normalized terminal status.</param>
    [LoggerMessage(12100, LogLevel.Debug, "Scripted process stage {Stage} for operation {ProcessOperationId} and security request {SecurityRequestId} completed with outcome {Outcome}.")]
    private static partial void Completed(
        ILogger logger,
        string stage,
        ProcessOperationId processOperationId,
        SecurityRequestId? securityRequestId,
        string outcome);

    /// <summary>Records an unexpected deterministic-stage exception without process input or output content.</summary>
    /// <param name="logger">The logger receiving the event.</param>
    /// <param name="stage">The bounded process stage.</param>
    /// <param name="processOperationId">The process operation identity.</param>
    /// <param name="securityRequestId">The authorizing request identity when available.</param>
    /// <param name="errorType">The exception type raised by the deterministic boundary.</param>
    [LoggerMessage(12101, LogLevel.Error, "Scripted process stage {Stage} for operation {ProcessOperationId} and security request {SecurityRequestId} failed with error type {ErrorType}.")]
    private static partial void Failed(
        ILogger logger,
        string stage,
        ProcessOperationId processOperationId,
        SecurityRequestId? securityRequestId,
        string errorType);
}
