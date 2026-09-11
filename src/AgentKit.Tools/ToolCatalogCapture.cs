// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Frozen;
using System.Runtime.ExceptionServices;

/// <summary>Retains a validated catalog's exact source graph and drains acquisitions before releasing its owners.</summary>
/// <remarks>
/// This run-owned object consumes an already merged catalog. It performs no discovery, alias policy,
/// service lookup, or invocation. Sources are owned only after complete constructor validation.
/// Acquisition and closure are race-safe; external callbacks and diagnostics run outside the state gate.
/// </remarks>
public sealed class ToolCatalogCapture: IToolCatalogCapture
{
    private readonly FrozenDictionary<ToolIdentity, (ToolDescriptor Tool, IToolProviderCapture Source)> _bindings;
    private readonly ImmutableArray<IToolProviderCapture> _sources;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCatalogCapture> _logger;
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private bool _cleanupStarted;
    private long _retentions;

    /// <summary>Validates an exact retained source graph before accepting ownership of every source capture.</summary>
    /// <param name="snapshot">The nonnull already merged catalog, including selected empty sources.</param>
    /// <param name="sources">One nonnull capture for each exact source-version entry, with no extra, missing, default, or duplicate normalized key. Each selected descriptor must match its source publication in full.</param>
    /// <param name="timeProvider">The nonnull replaceable clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull type-specific logger for safe lifecycle metadata.</param>
    /// <exception cref="ArgumentNullException">A required reference, source capture, or source snapshot is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key in <paramref name="sources"/> is default.</exception>
    /// <exception cref="ArgumentException">Normalized source keys, versions, identities, or selected descriptor content differ from the supplied snapshot.</exception>
    /// <remarks>
    /// Reads each source snapshot once after validating local arguments. A rejected constructor leaves
    /// all sources with the caller, including when a third-party snapshot getter throws. Unselected
    /// source descriptors are allowed but cannot be acquired here. The owner graph must not own or await
    /// this catalog or its leases. Borrowed invokers retain their source or host disposal owner.
    /// </remarks>
    public ToolCatalogCapture(
        ToolCatalogSnapshot snapshot,
        ImmutableDictionary<ToolSourceId, IToolProviderCapture> sources,
        TimeProvider timeProvider,
        ILogger<ToolCatalogCapture> logger)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        var captured = new Dictionary<ToolSourceId, IToolProviderCapture>();
        foreach (var pair in sources)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sources));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(sources));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(sources));
        }
        ArgumentException.ThrowIfNotEqual(captured.Count, snapshot.SourceVersions.Count, nameof(sources));
        foreach (var sourceId in captured.Keys)
        {
            ArgumentException.ThrowIfNotEqual(snapshot.SourceVersions.ContainsKey(sourceId), true, nameof(sources));
        }

        var publications = new Dictionary<ToolSourceId, Dictionary<ToolIdentity, ToolDescriptor>>();
        var ordered = captured.OrderBy(static pair => pair.Key.Value, StringComparer.Ordinal).ToArray();
        foreach (var pair in ordered)
        {
            var publication = pair.Value.Snapshot;
            ArgumentNullException.ThrowIfNull(publication, nameof(sources));
            ArgumentException.ThrowIfNotEqual(publication.SourceId, pair.Key, nameof(sources));
            ArgumentException.ThrowIfNotEqual(publication.SourceVersion, snapshot.SourceVersions[pair.Key], nameof(sources));
            publications.Add(pair.Key, publication.Tools.ToDictionary(static tool => new ToolIdentity(tool.Id, tool.Version)));
        }
        var bindings = new Dictionary<ToolIdentity, (ToolDescriptor Tool, IToolProviderCapture Source)>();
        foreach (var tool in snapshot.Tools)
        {
            var identity = new ToolIdentity(tool.Id, tool.Version);
            ArgumentException.ThrowIfNotEqual(publications[tool.SourceId].TryGetValue(identity, out var published), true, nameof(sources));
            ArgumentException.ThrowIfNotEqual(published, tool, nameof(sources));
            bindings.Add(identity, (tool, captured[tool.SourceId]));
        }

        _bindings = bindings.ToFrozenDictionary();
        _sources = [.. ordered.Select(static pair => pair.Value)];
        Snapshot = snapshot;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ToolCatalogSnapshot Snapshot { get; }

    /// <inheritdoc/>
    /// <remarks>Source exceptions propagate. If acquisition and its cleanup both fail, an aggregate retains both failures in that order.</remarks>
    public async ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        const string operation = AgentKitActivityNames.ToolCatalogInvokerAcquire;
        var started = TryGetTimestamp();
        using var scope = Start(operation, identity);
        try
        {
            (ToolDescriptor Tool, IToolProviderCapture Source)? binding = null;
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_closed && _bindings.TryGetValue(identity, out var selected))
                {
                    _retentions = checked(_retentions + 1);
                    binding = selected;
                }
            }

            var result = binding is { } retained
                ? await AcquireRetainedAsync(identity, retained.Tool, retained.Source, cancellationToken).ConfigureAwait(false)
                : new ToolInvokerUnavailable(identity, "The exact tool is unavailable from this catalog capture.");
            Complete(scope.Activity, operation, result is ToolInvokerAcquired ? "acquired" : "unavailable", started);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Complete(scope.Activity, operation, "cancelled", started);
            throw;
        }
        catch (Exception error)
        {
            Complete(scope.Activity, operation, "failed", started, error);
            throw;
        }
    }

    /// <summary>Closes acquisition and releases every retained source after pending acquisitions and leases drain.</summary>
    /// <returns>The shared cleanup completion, including any original source failure or aggregate of multiple failures.</returns>
    /// <remarks>Every source cleanup is started once before awaiting any of them. Multiple failures are ordered by ordinal source ID, independent of completion order. Repeated calls do not retry cleanup.</remarks>
    public async ValueTask DisposeAsync()
    {
        const string operation = AgentKitActivityNames.ToolCatalogCaptureClose;
        var started = TryGetTimestamp();
        using var scope = Start(operation);
        try
        {
            bool cleanup;
            lock (_gate)
            {
                _closed = true;
                cleanup = TryStartCleanup();
            }
            if (cleanup)
            {
                _ = DisposeSourcesAsync();
            }
            await _disposal.Task.ConfigureAwait(false);
            Complete(scope.Activity, operation, "closed", started);
        }
        catch (Exception error)
        {
            Complete(scope.Activity, operation, "failed", started, error);
            throw;
        }
    }

    private async ValueTask<ToolInvokerLeaseResult> AcquireRetainedAsync(ToolIdentity identity, ToolDescriptor tool, IToolProviderCapture source, CancellationToken cancellationToken)
    {
        Debug.Assert(identity != default && tool is not null && source is not null, "A registered retention has a complete captured binding.");
        IToolInvokerLease? owned = null;
        ToolInvokerLeaseResult? result = null;
        Exception? failure = null;
        try
        {
            var acquired = await source.AcquireInvokerAsync(identity, cancellationToken).ConfigureAwait(false);
            switch (acquired)
            {
                case ToolInvokerAcquired success:
                    owned = success.Lease;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (owned.Tool != tool || owned.SourceVersion != Snapshot.SourceVersions[tool.SourceId])
                    {
                        throw new InvalidOperationException("The source returned a lease for a different captured binding.");
                    }
                    var invoker = owned.Invoker ?? throw new InvalidOperationException("The source returned a lease without an invoker.");
                    var inner = owned;
                    var lease = new ToolInvokerLease(tool, Snapshot.SourceVersions[tool.SourceId], invoker, () => ReleaseLeaseAsync(inner));
                    lock (_gate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!_closed)
                        {
                            result = new ToolInvokerAcquired(lease);
                            owned = null;
                        }
                    }
                    break;
                case ToolInvokerUnavailable unavailable when unavailable.Identity == identity:
                    cancellationToken.ThrowIfCancellationRequested();
                    break;
                default:
                    throw new InvalidOperationException("The source returned an invalid acquisition result.");
            }
        }
        catch (Exception error)
        {
            failure = error;
        }

        if (result is not null)
        {
            return result;
        }
        if (owned is not null)
        {
            failure = Combine(failure, await DisposeOwnedAsync(owned).ConfigureAwait(false));
        }
        try
        {
            await EndRetentionAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = Combine(failure, error);
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return new ToolInvokerUnavailable(identity, "The exact tool binding is unavailable from the retained source.");
    }

    private async ValueTask ReleaseLeaseAsync(IToolInvokerLease owned)
    {
        Debug.Assert(owned is not null, "A catalog lease owns exactly one source lease.");
        const string operation = AgentKitActivityNames.ToolCatalogInvokerRelease;
        var started = TryGetTimestamp();
        using var scope = Start(operation);
        var failure = await DisposeOwnedAsync(owned).ConfigureAwait(false);
        try
        {
            await EndRetentionAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = Combine(failure, error);
        }
        Complete(scope.Activity, operation, failure is null ? "released" : "failed", started, failure);
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async ValueTask EndRetentionAsync()
    {
        bool cleanup;
        lock (_gate)
        {
            Debug.Assert(_retentions > 0, "Each pending acquisition or transferred lease releases one retention exactly once.");
            _retentions--;
            cleanup = TryStartCleanup();
        }
        if (cleanup)
        {
            _ = DisposeSourcesAsync();
            await _disposal.Task.ConfigureAwait(false);
        }
    }

    private bool TryStartCleanup()
    {
        Debug.Assert(_gate.IsHeldByCurrentThread, "The capture gate serializes cleanup ownership.");
        if (!_closed || _retentions != 0 || _cleanupStarted)
        {
            return false;
        }
        _cleanupStarted = true;
        return true;
    }

    private async Task DisposeSourcesAsync()
    {
        const string operation = AgentKitActivityNames.ToolCatalogCaptureDisposeSources;
        var started = TryGetTimestamp();
        Exception? failure = null;
        using (var scope = Start(operation))
        {
            try
            {
                var releases = _sources.Select(static source => DisposeOwnedAsync(source)).ToArray();
                var failures = (await Task.WhenAll(releases).ConfigureAwait(false)).OfType<Exception>().ToArray();
                failure = failures.Length switch { 0 => null, 1 => failures[0], _ => new AggregateException("Retained tool source cleanup failed.", failures) };
            }
            catch (Exception error)
            {
                failure = error;
            }
            Complete(scope.Activity, operation, failure is null ? "disposed" : "failed", started, failure);
        }
        _ = failure is null ? _disposal.TrySetResult() : _disposal.TrySetException(failure);
    }

    private static async Task<Exception?> DisposeOwnedAsync(IAsyncDisposable owned)
    {
        Debug.Assert(owned is not null, "Cleanup receives a retained owner.");
        try
        {
            await owned.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static Exception? Combine(Exception? first, Exception? second) =>
        first is null ? second : second is null ? first : new AggregateException("Tool acquisition and cleanup failed.", first, second);

    private AgentKitActivityScope Start(string operation, ToolIdentity? identity = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "Catalog capture stages use shared names.");
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, operation },
            { AgentKitTagNames.TenantId, Snapshot.Identity.TenantId.Value },
            { AgentKitTagNames.PrincipalId, Snapshot.Identity.PrincipalId.Value },
            { AgentKitTagNames.AgentId, Snapshot.AgentId.ToString() },
            { AgentKitTagNames.SessionId, Snapshot.SessionId.ToString() },
            { AgentKitTagNames.RunId, Snapshot.RunId.ToString() },
            { AgentKitTagNames.ToolCatalogVersion, Snapshot.Version.Value },
        };
        if (identity is { } tool)
        {
            tags.Add(AgentKitTagNames.ToolId, tool.Id.Value);
            tags.Add(AgentKitTagNames.ToolVersion, tool.Version.Value);
        }
        var scope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, tags);
        Observe(() => ToolLog.CatalogCaptureStarted(_logger, operation, Snapshot.Identity.TenantId, Snapshot.Identity.PrincipalId, Snapshot.AgentId, Snapshot.SessionId, Snapshot.RunId, Snapshot.Version));
        return scope;
    }

    private void Complete(Activity? activity, string operation, string outcome, long? started, Exception? error = null)
    {
        Debug.Assert(operation is AgentKitActivityNames.ToolCatalogInvokerAcquire or AgentKitActivityNames.ToolCatalogInvokerRelease
            or AgentKitActivityNames.ToolCatalogCaptureClose or AgentKitActivityNames.ToolCatalogCaptureDisposeSources, "Metric operations are bounded.");
        Debug.Assert(outcome is "acquired" or "unavailable" or "cancelled" or "closed" or "released" or "disposed" or "failed", "Metric outcomes are bounded.");
        var errorType = error?.GetType().FullName;
        var level = OutcomeLevel(outcome);
        Observe(() => ToolLog.CatalogCaptureCompleted(_logger, level, operation, outcome, Snapshot.Identity.TenantId, Snapshot.Identity.PrincipalId, Snapshot.AgentId, Snapshot.SessionId, Snapshot.RunId, Snapshot.Version, errorType));
        Observe(() =>
        {
            if (outcome is "failed" or "cancelled" or "unavailable")
            {
                activity.SetFailed(outcome, errorType ?? outcome);
            }
            else
            {
                activity.SetSuccessful(outcome);
            }
        });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, operation }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolCatalogCaptureMetrics.Count.Add(1, tags));
        if (started is { } timestamp)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero)
                {
                    ToolCatalogCaptureMetrics.Duration.Record(elapsed.TotalSeconds, tags);
                }
            });
        }
    }

    private static LogLevel OutcomeLevel(string outcome) => outcome switch
    {
        "failed" => LogLevel.Error,
        "unavailable" => LogLevel.Warning,
        "cancelled" => LogLevel.Information,
        _ => LogLevel.Debug,
    };

    private long? TryGetTimestamp()
    {
        try { return _timeProvider.GetTimestamp(); }
        catch { return null; }
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "Every observation callback is supplied by the capture.");
        try { observation(); }
        catch { /* Observation never changes capture ownership. */ }
    }
}
