// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Contains failures from shared diagnostics around synchronous codec operations.</summary>
internal static class SessionEntryCodecObservation
{
    /// <summary>Runs one semantic codec operation while isolating all timing, logging, tracing, and metric failures.</summary>
    /// <typeparam name="T">The typed codec result.</typeparam>
    /// <param name="timeProvider">The required diagnostic clock.</param><param name="logger">The required content-free logger.</param><param name="operation">The bounded operation name.</param><param name="action">The semantic codec delegate.</param>
    /// <param name="initialEvidence">Validated encode evidence, or null until decoding succeeds.</param>
    /// <returns>The unchanged semantic result.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception><exception cref="ArgumentException"><paramref name="operation"/> is blank.</exception>
    internal static T Observe<T>(TimeProvider timeProvider, ILogger logger, string operation,
        SessionEntry? initialEvidence, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        long? started = null;
        Try(() => started = timeProvider.GetTimestamp());
        var scope = AgentKitActivityScope.Start(AgentKitActivityNames.SessionEntryCodec, ActivityKind.Internal,
            [new KeyValuePair<string, object?>(AgentKitTagNames.SessionEntryCodecOperation, operation)]);
        var outcome = "faulted";
        try
        {
            var result = action();
            outcome = ResultOutcome(result);
            var evidence = ResultEvidence(result) ?? initialEvidence;
            Try(() => TagEvidence(scope.Activity, evidence));
            Try(() => scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            Try(() => scope.Activity?.SetStatus(outcome == "rejected" ? ActivityStatusCode.Error : ActivityStatusCode.Ok));
            if (evidence is null)
            {
                Try(() => SessionLog.CodecCompleted(logger, operation, outcome));
            }
            else
            {
                Try(() => LogCompleted(logger, operation, outcome, evidence));
            }
            return result;
        }
        catch (Exception exception)
        {
            Try(() => scope.Activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            Try(() => scope.Activity?.SetTag(AgentKitTagNames.ErrorType, exception.GetType().Name));
            Try(() => scope.Activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name));
            if (initialEvidence is null)
            {
                Try(() => SessionLog.CodecFaulted(logger, operation, exception.GetType().Name));
            }
            else
            {
                Try(() => LogFaulted(logger, operation, exception.GetType().Name, initialEvidence));
            }
            throw;
        }
        finally
        {
            var tags = new TagList { { AgentKitTagNames.SessionEntryCodecOperation, operation }, { AgentKitTagNames.Outcome, outcome } };
            Try(() => SessionEntryCodecMetrics.Operations.Add(1, tags));
            if (started is { } timestamp)
            {
                Try(() =>
                {
                    var elapsed = timeProvider.GetElapsedTime(timestamp);
                    if (elapsed >= TimeSpan.Zero)
                    {
                        SessionEntryCodecMetrics.Duration.Record(elapsed.TotalSeconds, tags);
                    }
                });
            }

            scope.Dispose();
        }
    }

    private static string ResultOutcome<T>(T result) => result switch
    {
        SessionEntryEncoded => "encoded",
        SessionEntryEncodeRejected => "rejected",
        SessionEntryDecoded => "decoded",
        SessionEntryOpaque => "opaque",
        SessionEntryDecodeRejected => "rejected",
        _ => "unknown",
    };

    private static SessionEntry? ResultEvidence<T>(T result) => result switch
    {
        SessionEntryDecoded decoded => decoded.Decoded.Entry,
        _ => null,
    };

    private static void TagEvidence(Activity? activity, SessionEntry? evidence)
    {
        if (activity is null || evidence is null)
        {
            return;
        }

        _ = activity.SetTag(AgentKitTagNames.AgentId, evidence.Address.AgentId.ToString());
        _ = activity.SetTag(AgentKitTagNames.SessionId, evidence.Address.SessionId.ToString());
        _ = activity.SetTag(AgentKitTagNames.SessionEntryId, evidence.Id.ToString());
        _ = activity.SetTag(AgentKitTagNames.SessionBranchId, evidence.BranchId.ToString());
        _ = activity.SetTag(AgentKitTagNames.OperationId, evidence.Correlation.OperationId.ToString());
        _ = activity.SetTag(AgentKitTagNames.ExecutionLaneId, Lane(evidence)?.ToString());
        _ = activity.SetTag(AgentKitTagNames.RunId, Run(evidence.Correlation)?.ToString());
        _ = activity.SetTag(AgentKitTagNames.TurnId,
            (evidence.Correlation as InRunOperationCorrelation)?.TurnId?.ToString());
    }

    private static void LogCompleted(ILogger logger, string operation, string outcome, SessionEntry evidence) =>
        SessionLog.CorrelatedCodecCompleted(logger, operation, outcome, evidence.Address.AgentId,
            evidence.Address.SessionId, evidence.Id, evidence.BranchId, Lane(evidence), evidence.Correlation.OperationId,
            Run(evidence.Correlation), (evidence.Correlation as InRunOperationCorrelation)?.TurnId);

    private static void LogFaulted(ILogger logger, string operation, string errorType, SessionEntry evidence) =>
        SessionLog.CorrelatedCodecFaulted(logger, operation, errorType, evidence.Address.AgentId,
            evidence.Address.SessionId, evidence.Id, evidence.BranchId, Lane(evidence), evidence.Correlation.OperationId,
            Run(evidence.Correlation), (evidence.Correlation as InRunOperationCorrelation)?.TurnId);

    private static ExecutionLaneId? Lane(SessionEntry evidence) => evidence switch
    {
        ExecutionLaneProvisionedSessionEntry lane => lane.ExecutionLaneId,
        InputPromotedSessionEntry promotion => promotion.ExecutionLaneId,
        _ => null,
    };

    private static RunId? Run(OperationCorrelation correlation) => correlation switch
    {
        InRunOperationCorrelation inRun => inRun.RunId,
        _ => null,
    };

    private static void Try(Action action)
    {
        Debug.Assert(action is not null, "The caller supplies one best-effort diagnostic action.");
        try
        {
            action();
        }
        catch
        {
            // Instrumentation cannot change the semantic codec operation.
        }
    }
}
