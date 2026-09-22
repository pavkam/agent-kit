// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>
/// Executes prepared tool batches under the barrier-segment scheduling algorithm with bounded parallelism and
/// deterministic source-order results.
/// </summary>
/// <remarks>
/// Parallel-safe calls run together subject to <see cref="ToolRuntimeOptions.MaximumParallelInvocations"/> and
/// concurrency-key sub-barriers. Sequential, global-exclusive, and conservatively classified unspecified calls form
/// ordering barriers between parallel segments.
/// </remarks>
public sealed class BarrierSegmentToolScheduler: IToolScheduler
{
    private const ToolSchedulingMode _rejectSchedulingMode = (ToolSchedulingMode) byte.MaxValue;

    private readonly IToolResultNormalizer _resultNormalizer;
    private readonly ToolRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BarrierSegmentToolScheduler> _logger;

    /// <summary>Initializes the default barrier-segment scheduler.</summary>
    /// <param name="resultNormalizer">The normalizer that maps invoker evidence into bounded terminal content.</param>
    /// <param name="options">The configured runtime limits and unknown-scheduling policy.</param>
    /// <param name="timeProvider">The replaceable clock used for terminal timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public BarrierSegmentToolScheduler(
        IToolResultNormalizer resultNormalizer,
        IOptions<ToolRuntimeOptions> options,
        TimeProvider timeProvider,
        ILogger<BarrierSegmentToolScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(resultNormalizer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _resultNormalizer = resultNormalizer;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolBatchResult> ExecuteAsync(ToolBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Entries.Length == 0)
        {
            return new ToolBatchResult([]);
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.MaximumParallelInvocations, 0);

        var results = new ToolCallResult?[batch.Entries.Length];
        var segments = BarrierSegmentPlanner.Partition(batch, _options);
        var stopScheduling = false;

        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            if (stopScheduling || cancellationToken.IsCancellationRequested)
            {
                MarkRemainingSegmentsInterrupted(segments, segmentIndex, results, batch.Entries);
                break;
            }

            var segment = segments[segmentIndex];
            switch (segment.Kind)
            {
                case BarrierSegmentKind.Reject:
                    await ExecuteRejectSegmentAsync(segment, results, cancellationToken).ConfigureAwait(false);
                    break;
                case BarrierSegmentKind.Barrier:
                    await ExecuteBarrierSegmentAsync(segment, results, cancellationToken).ConfigureAwait(false);
                    if (batch.FailureMode == ToolBatchFailureMode.FailFast && SegmentContainsFailure(segment, results))
                    {
                        stopScheduling = true;
                    }

                    break;
                case BarrierSegmentKind.Parallel:
                    await ExecuteParallelSegmentAsync(segment, results, batch.FailureMode, cancellationToken)
                        .ConfigureAwait(false);
                    if (batch.FailureMode == ToolBatchFailureMode.FailFast && SegmentContainsFailure(segment, results))
                    {
                        stopScheduling = true;
                    }

                    break;
                default:
                    throw new InvalidOperationException("An undefined barrier segment kind was produced.");
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            for (var index = 0; index < results.Length; index++)
            {
                results[index] ??= ToolCallResultComposer.ScheduledInterrupted(
                    batch.Entries[index],
                    _timeProvider.GetUtcNow());
            }
        }

        Debug.Assert(results.All(static result => result is not null), "Every batch entry must reach one terminal result.");
        return new ToolBatchResult([.. results.Select(static result => result!)]);
    }

    private Task ExecuteRejectSegmentAsync(
        PlannedSegment segment,
        ToolCallResult?[] results,
        CancellationToken cancellationToken)
    {
        Debug.Assert(segment.Entries.Length == 1, "Reject segments contain exactly one entry.");
        cancellationToken.ThrowIfCancellationRequested();
        var entry = segment.Entries[0];
        var index = segment.EntryIndexes[0];
        results[index] = ToolCallResultComposer.ScheduledPreInvocation(
            entry,
            ToolTerminalStatus.Denied,
            "The tool call was rejected because its scheduling compatibility is unspecified and host policy refuses unknown modes.",
            _timeProvider.GetUtcNow());
        return ReleaseLeaseWithoutInvokeAsync(entry);
    }

    private async Task ExecuteBarrierSegmentAsync(
        PlannedSegment segment,
        ToolCallResult?[] results,
        CancellationToken cancellationToken)
    {
        Debug.Assert(segment.Entries.Length == 1, "Barrier segments contain exactly one entry.");
        var entry = segment.Entries[0];
        var index = segment.EntryIndexes[0];
        results[index] = await InvokeEntryAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteParallelSegmentAsync(
        PlannedSegment segment,
        ToolCallResult?[] results,
        ToolBatchFailureMode failureMode,
        CancellationToken cancellationToken)
    {
        using var failFastSource = failureMode == ToolBatchFailureMode.FailFast
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        var segmentToken = failFastSource?.Token ?? cancellationToken;
        using var parallelLimiter = new SemaphoreSlim(_options.MaximumParallelInvocations, _options.MaximumParallelInvocations);
        var keyLocks = new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        var tasks = new Task[segment.Entries.Length];
        var admissionGate = new SemaphoreSlim(1, 1);

        try
        {
            for (var offset = 0; offset < segment.Entries.Length; offset++)
            {
                var entry = segment.Entries[offset];
                var resultIndex = segment.EntryIndexes[offset];
                tasks[offset] = RunParallelEntryAsync(
                    entry,
                    resultIndex,
                    results,
                    parallelLimiter,
                    keyLocks,
                    admissionGate,
                    failFastSource,
                    segmentToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            foreach (var keyLock in keyLocks.Values)
            {
                keyLock.Dispose();
            }
        }
    }

    private async Task RunParallelEntryAsync(
        ToolBatchEntry entry,
        int resultIndex,
        ToolCallResult?[] results,
        SemaphoreSlim parallelLimiter,
        Dictionary<string, SemaphoreSlim> keyLocks,
        SemaphoreSlim admissionGate,
        CancellationTokenSource? failFastSource,
        CancellationToken segmentToken)
    {
        SemaphoreSlim? keyLock = null;
        var acquiredParallelSlot = false;
        var acquiredKeyLock = false;
        try
        {
            if (segmentToken.IsCancellationRequested)
            {
                results[resultIndex] = ToolCallResultComposer.ScheduledInterrupted(entry, _timeProvider.GetUtcNow());
                await ReleaseLeaseWithoutInvokeAsync(entry).ConfigureAwait(false);
                return;
            }

            if (entry.ExecutionHints.SchedulingMode == ToolSchedulingMode.ConcurrencyKey)
            {
                var key = entry.ExecutionHints.ConcurrencyKey!;
                lock (keyLocks)
                {
                    if (!keyLocks.TryGetValue(key, out keyLock))
                    {
                        keyLock = new SemaphoreSlim(1, 1);
                        keyLocks[key] = keyLock;
                    }
                }

                await keyLock.WaitAsync(segmentToken).ConfigureAwait(false);
                acquiredKeyLock = true;
            }

            await admissionGate.WaitAsync(segmentToken).ConfigureAwait(false);
            try
            {
                if (segmentToken.IsCancellationRequested)
                {
                    results[resultIndex] = ToolCallResultComposer.ScheduledInterrupted(entry, _timeProvider.GetUtcNow());
                    await ReleaseLeaseWithoutInvokeAsync(entry).ConfigureAwait(false);
                    return;
                }

                await parallelLimiter.WaitAsync(segmentToken).ConfigureAwait(false);
                acquiredParallelSlot = true;
            }
            finally
            {
                _ = admissionGate.Release();
            }

            var terminal = await InvokeEntryAsync(entry, segmentToken).ConfigureAwait(false);
            results[resultIndex] = terminal;
            if (failFastSource is not null
                && terminal.Status is not ToolTerminalStatus.Succeeded
                    and not ToolTerminalStatus.Denied)
            {
                try
                {
                    await failFastSource.CancelAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
        catch (OperationCanceledException) when (segmentToken.IsCancellationRequested)
        {
            if (results[resultIndex] is null)
            {
                results[resultIndex] = ToolCallResultComposer.ScheduledInterrupted(entry, _timeProvider.GetUtcNow());
                await ReleaseLeaseWithoutInvokeAsync(entry).ConfigureAwait(false);
            }
        }
        finally
        {
            if (acquiredParallelSlot)
            {
                _ = parallelLimiter.Release();
            }

            if (acquiredKeyLock && keyLock is not null)
            {
                _ = keyLock.Release();
            }
        }
    }

    private async Task<ToolCallResult> InvokeEntryAsync(ToolBatchEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var context = entry.Invocation;
            ToolInvocationResult invocation = null!;
            for (var attempt = context.Attempt; attempt <= _options.MaximumRetryAttempts; attempt++)
            {
                if (attempt != context.Attempt)
                {
                    context = CloneContextForAttempt(context, attempt, _timeProvider.GetUtcNow());
                }

                try
                {
                    invocation = await entry.InvokerLease.Invoker.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return ToolCallResultComposer.ScheduledInterrupted(entry, _timeProvider.GetUtcNow());
                }
                catch (Exception exception)
                {
                    ToolLog.Failed(_logger, context.CallId, context.Tool.Id, exception.GetType().Name);
                    invocation = new ToolInvocationResult(
                        new ToolCallOutcome(
                            ToolCallOutcomeKind.Failed,
                            ToolTerminalStatus.InvocationFailed,
                            SideEffectCertainty.DefinitelyNotPerformed,
                            retryable: false,
                            "The tool invoker faulted before producing a result.",
                            ExtensionData.Empty),
                        []);
                }

                if (invocation.Outcome.Kind == ToolCallOutcomeKind.Success
                    || !ToolInvocationRetryPolicy.ShouldRetry(
                        invocation.Outcome,
                        context.Tool.Effects,
                        attempt,
                        _options,
                        _timeProvider.GetUtcNow()))
                {
                    break;
                }
            }

            var call = ToValidatedCall(entry);
            var snapshot = ToolRuntimeNormalizationDefaults.ForResolvedTool(call.ExecutionPolicy);
            var normalization = await _resultNormalizer
                .NormalizeAsync(call, invocation, snapshot, cancellationToken)
                .ConfigureAwait(false);
            return ToolCallResultComposer.FromScheduledInvocation(entry, invocation, normalization, _timeProvider.GetUtcNow());
        }
        finally
        {
            await entry.InvokerLease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ValidatedToolCall ToValidatedCall(ToolBatchEntry entry)
    {
        var context = entry.Invocation;
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Scheduled invocations require authorization evidence on the grant.");
        return new ValidatedToolCall(
            context.AgentId,
            context.SessionId,
            context.RunId,
            context.TurnId,
            context.OperationId,
            context.CallId,
            authorization,
            new ToolCatalogVersion("agentkit.tools.scheduled"),
            new ToolAlias(context.Tool.Name),
            context.Tool,
            context.ToolVersion,
            new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("agentkit.tools.scheduled"), new ToolExecutionPolicyVersion(1)),
            entry.SourceOrdinal,
            context.Arguments,
            ToolInvocationSecurityBinding.ValidatedArgumentsFingerprint(context.Arguments),
            context.RequestedAt,
            context.InvocationStartedAt);
    }

    private static Task ReleaseLeaseWithoutInvokeAsync(ToolBatchEntry entry) => entry.InvokerLease.DisposeAsync().AsTask();

    private static bool SegmentContainsFailure(PlannedSegment segment, ToolCallResult?[] results)
    {
        foreach (var index in segment.EntryIndexes)
        {
            if (results[index] is { Status: not ToolTerminalStatus.Succeeded and not ToolTerminalStatus.Denied })
            {
                return true;
            }
        }

        return false;
    }

    private void MarkRemainingSegmentsInterrupted(
        IReadOnlyList<PlannedSegment> segments,
        int startingSegmentIndex,
        ToolCallResult?[] results,
        ImmutableArray<ToolBatchEntry> entries)
    {
        for (var segmentIndex = startingSegmentIndex; segmentIndex < segments.Count; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            for (var offset = 0; offset < segment.Entries.Length; offset++)
            {
                var resultIndex = segment.EntryIndexes[offset];
                results[resultIndex] ??= ToolCallResultComposer.ScheduledInterrupted(
                    entries[resultIndex],
                    _timeProvider.GetUtcNow());
            }
        }
    }

    private enum BarrierSegmentKind
    {
        Parallel,
        Barrier,
        Reject,
    }

    private sealed class PlannedSegment(
        BarrierSegmentKind kind,
        ImmutableArray<ToolBatchEntry> entries,
        ImmutableArray<int> entryIndexes)
    {
        public BarrierSegmentKind Kind { get; } = kind;
        public ImmutableArray<ToolBatchEntry> Entries { get; } = entries;
        public ImmutableArray<int> EntryIndexes { get; } = entryIndexes;
    }

    private static class BarrierSegmentPlanner
    {
        internal static List<PlannedSegment> Partition(ToolBatch batch, ToolRuntimeOptions options)
        {
            var segments = new List<PlannedSegment>();
            var parallelEntries = ImmutableArray.CreateBuilder<ToolBatchEntry>();
            var parallelIndexes = ImmutableArray.CreateBuilder<int>();

            for (var index = 0; index < batch.Entries.Length; index++)
            {
                var entry = batch.Entries[index];
                var effectiveMode = EffectiveSchedulingMode(entry, batch, options);
                if (effectiveMode == _rejectSchedulingMode)
                {
                    FlushParallel();
                    segments.Add(new PlannedSegment(BarrierSegmentKind.Reject, [entry], [index]));
                    continue;
                }

                if (FormsBarrier(effectiveMode))
                {
                    FlushParallel();
                    segments.Add(new PlannedSegment(BarrierSegmentKind.Barrier, [entry], [index]));
                    continue;
                }

                parallelEntries.Add(entry);
                parallelIndexes.Add(index);
            }

            FlushParallel();
            return segments;

            void FlushParallel()
            {
                if (parallelEntries.Count == 0)
                {
                    return;
                }

                segments.Add(new PlannedSegment(
                    BarrierSegmentKind.Parallel,
                    parallelEntries.ToImmutable(),
                    parallelIndexes.ToImmutable()));
                parallelEntries.Clear();
                parallelIndexes.Clear();
            }
        }

        internal static ToolSchedulingMode EffectiveSchedulingMode(
            ToolBatchEntry entry,
            ToolBatch batch,
            ToolRuntimeOptions options)
        {
            var mode = entry.ExecutionHints.SchedulingMode;
            return mode != ToolSchedulingMode.Unspecified
                ? mode
                : batch.UnknownSchedulingMode == UnknownSchedulingMode.Reject
                    || options.UnknownSchedulingMode == UnknownSchedulingMode.Reject
                    ? _rejectSchedulingMode
                    : ToolSchedulingMode.Sequential;
        }

        private static bool FormsBarrier(ToolSchedulingMode mode) =>
            mode is ToolSchedulingMode.Sequential
                or ToolSchedulingMode.GlobalExclusive
                or ToolSchedulingMode.HostScheduled
                or _rejectSchedulingMode;
    }

    private static ToolInvocationContext CloneContextForAttempt(
        ToolInvocationContext context,
        int attempt,
        DateTimeOffset invocationStartedAt) =>
        new(
            context.AgentId,
            context.SessionId,
            context.RunId,
            context.TurnId,
            context.OperationId,
            context.CallId,
            context.Tool,
            context.ToolVersion,
            context.Arguments,
            context.InvocationGrant,
            attempt,
            context.RequestedAt,
            invocationStartedAt,
            context.Deadline,
            context.Progress);
}
