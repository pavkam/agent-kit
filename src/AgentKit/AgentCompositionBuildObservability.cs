// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Emits failure-isolated diagnostics for one AgentKit service-provider build.</summary>
internal static class AgentCompositionBuildObservability
{
    private const string _builtOutcome = "built";
    private const string _failedOutcome = "failed";
    private const string _rejectedOutcome = "rejected";
    private static readonly NonBlockingInstrument<Counter<long>> _builds = new();
    private static readonly NonBlockingInstrument<Histogram<double>> _duration = new();

    /// <summary>Starts one safe provider-build activity and monotonic duration measurement.</summary>
    /// <param name="timeProvider">The non-null bootstrap clock used only for duration observation.</param>
    /// <returns>The failure-isolated activity scope and an optional monotonic start timestamp.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    internal static (AgentKitActivityScope Scope, long? Timestamp) Start(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild,
            ActivityKind.Internal);
        try
        {
            return (scope, timeProvider.GetTimestamp());
        }
        catch (Exception)
        {
            return (scope, null);
        }
    }

    /// <summary>Records successful construction without allowing observers to affect the returned provider.</summary>
    /// <param name="scope">The activity scope owned by the build.</param>
    /// <param name="timeProvider">The non-null bootstrap clock used for duration observation.</param>
    /// <param name="timestamp">The optional monotonic timestamp captured when the build began.</param>
    /// <param name="logger">The directly supplied bootstrap logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="timeProvider"/>, or <paramref name="logger"/> is <see langword="null"/>.</exception>
    internal static void CompleteBuilt(
        AgentKitActivityScope scope,
        TimeProvider timeProvider,
        long? timestamp,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        Complete(scope, timeProvider, timestamp, logger, _builtOutcome, errorType: null, AgentCompositionBuildLog.Built);
    }

    /// <summary>Records metadata rejection without allowing observers to replace the composition exception.</summary>
    /// <param name="scope">The activity scope owned by the build.</param>
    /// <param name="timeProvider">The non-null bootstrap clock used for duration observation.</param>
    /// <param name="timestamp">The optional monotonic timestamp captured when the build began.</param>
    /// <param name="logger">The directly supplied bootstrap logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="timeProvider"/>, or <paramref name="logger"/> is <see langword="null"/>.</exception>
    internal static void CompleteRejected(
        AgentKitActivityScope scope,
        TimeProvider timeProvider,
        long? timestamp,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        Complete(
            scope,
            timeProvider,
            timestamp,
            logger,
            _rejectedOutcome,
            nameof(AgentCompositionException),
            static value => AgentCompositionBuildLog.Rejected(value, nameof(AgentCompositionException)));
    }

    /// <summary>Records Microsoft DI construction failure without allowing observers to replace the provider exception.</summary>
    /// <param name="scope">The activity scope owned by the build.</param>
    /// <param name="timeProvider">The non-null bootstrap clock used for duration observation.</param>
    /// <param name="timestamp">The optional monotonic timestamp captured when the build began.</param>
    /// <param name="logger">The directly supplied bootstrap logger.</param>
    /// <param name="exception">The non-null provider-construction exception whose type is safe diagnostic context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="timeProvider"/>, <paramref name="logger"/>, or <paramref name="exception"/> is <see langword="null"/>.</exception>
    internal static void CompleteFailed(
        AgentKitActivityScope scope,
        TimeProvider timeProvider,
        long? timestamp,
        ILogger logger,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(exception);
        var errorType = exception.GetType().Name;
        Complete(
            scope,
            timeProvider,
            timestamp,
            logger,
            _failedOutcome,
            errorType,
            value => AgentCompositionBuildLog.Failed(value, errorType));
    }

    /// <summary>Completes one build's activity, metrics, and log through independently isolated observer calls.</summary>
    /// <param name="scope">The non-null activity scope owned by the operation.</param>
    /// <param name="timeProvider">The non-null bootstrap clock used for duration observation.</param>
    /// <param name="timestamp">The optional monotonic timestamp captured at operation entry.</param>
    /// <param name="logger">The non-null directly supplied bootstrap logger.</param>
    /// <param name="outcome">The bounded built, rejected, or failed outcome.</param>
    /// <param name="errorType">The optional safe exception type.</param>
    /// <param name="writeLog">The source-generated structured log call for the terminal outcome.</param>
    private static void Complete(
        AgentKitActivityScope scope,
        TimeProvider timeProvider,
        long? timestamp,
        ILogger logger,
        string outcome,
        string? errorType,
        Action<ILogger> writeLog)
    {
        Debug.Assert(scope is not null, "Every observed provider build owns an activity scope.");
        Debug.Assert(timeProvider is not null, "Public construction captures a non-null bootstrap clock.");
        Debug.Assert(logger is not null, "Public construction rejects a null bootstrap logger.");
        Debug.Assert(outcome is _builtOutcome or _rejectedOutcome or _failedOutcome,
            "Provider-build metrics use only bounded outcomes.");
        Debug.Assert(writeLog is not null, "Every terminal outcome has a source-generated log call.");

        if (errorType is null)
        {
            scope.Activity.SetSuccessful(outcome);
        }
        else
        {
            scope.Activity.SetFailed(outcome, errorType);
        }

        var outcomeTag = new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome);
        try { GetBuilds()?.Add(1, outcomeTag); } catch (Exception) { }
        if (timestamp is { } started)
        {
            try
            {
                var elapsed = timeProvider.GetElapsedTime(started);
                if (elapsed >= TimeSpan.Zero)
                {
                    GetDuration()?.Record(elapsed.TotalSeconds, outcomeTag);
                }
            }
            catch (Exception)
            {
            }
        }

        try { writeLog(logger); } catch (Exception) { }
        scope.Dispose();
    }

    /// <summary>Gets the process-lifetime build counter while containing meter-listener failures.</summary>
    /// <returns>The shared counter, or <see langword="null"/> when instrument creation failed.</returns>
    private static Counter<long>? GetBuilds()
        => _builds.GetOrCreate(static () => AgentKitDiagnostics.Metrics.CreateCounter<long>(
            AgentKitMetricNames.AgentCompositionBuildCount));

    /// <summary>Gets the process-lifetime build-duration histogram while containing meter-listener failures.</summary>
    /// <returns>The shared histogram, or <see langword="null"/> when instrument creation failed.</returns>
    private static Histogram<double>? GetDuration()
        => _duration.GetOrCreate(static () => AgentKitDiagnostics.Metrics.CreateHistogram<double>(
            AgentKitMetricNames.AgentCompositionBuildDuration,
            unit: "s"));
}
