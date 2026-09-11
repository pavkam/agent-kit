// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns a complete discovered source graph through merge and preflight until catalog handoff or cleanup.</summary>
/// <remarks>Metadata is immutable and remains readable after closure. Transfer and closure have one synchronized winner; cleanup callbacks run outside the gate and are attempted once. This internal owner does not publish model-facing tools.</remarks>
internal sealed class ToolDiscoveryCapture: IAsyncDisposable
{
    private readonly ImmutableDictionary<ToolSourceId, IToolProviderCapture> _sources;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolDiscoveryCapture> _logger;
    private readonly ILogger<ToolCatalogCapture> _catalogLogger;
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;

    /// <summary>Validates complete source membership before accepting discovered owners.</summary>
    /// <param name="selection">The nonnull original request and exact authored publications.</param>
    /// <param name="sourceSnapshots">The nonnull complete source publications read during discovery, including empty sources.</param>
    /// <param name="sources">The nonnull matching exact source owners, each appearing once by reference identity.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull discovery-owner logger.</param>
    /// <param name="catalogLogger">The nonnull logger for a later catalog owner.</param>
    /// <exception cref="ArgumentNullException">A required reference or map value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A source-map key is default.</exception>
    /// <exception cref="ArgumentException">Source keys, identities, or membership differ from selection, or multiple entries reuse an owner.</exception>
    /// <remarks>Performs no live provider/capture metadata reads and no cleanup. Rejected construction leaves all owners with the caller.</remarks>
    internal ToolDiscoveryCapture(ToolDiscoverySelection selection, ImmutableDictionary<ToolSourceId, ToolProviderSnapshot> sourceSnapshots,
        ImmutableDictionary<ToolSourceId, IToolProviderCapture> sources, TimeProvider timeProvider, ILogger<ToolDiscoveryCapture> logger, ILogger<ToolCatalogCapture> catalogLogger)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(sourceSnapshots);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(catalogLogger);
        var snapshots = ImmutableDictionary.CreateBuilder<ToolSourceId, ToolProviderSnapshot>();
        foreach (var pair in sourceSnapshots)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sourceSnapshots));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(sourceSnapshots));
            ArgumentException.ThrowIfNotEqual(pair.Key, pair.Value.SourceId, nameof(sourceSnapshots));
            ArgumentException.ThrowIfNotEqual(snapshots.TryAdd(pair.Key, pair.Value), true, nameof(sourceSnapshots));
        }
        var captured = ImmutableDictionary.CreateBuilder<ToolSourceId, IToolProviderCapture>();
        var identities = new HashSet<IToolProviderCapture>(ReferenceEqualityComparer.Instance);
        foreach (var pair in sources)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sources));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(sources));
            ArgumentException.ThrowIfNotEqual(identities.Add(pair.Value), true, nameof(sources));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(sources));
        }
        ArgumentException.ThrowIfNotEqual(snapshots.Count, selection.Providers.Length, nameof(sourceSnapshots));
        ArgumentException.ThrowIfNotEqual(captured.Count, selection.Providers.Length, nameof(sources));
        foreach (var binding in selection.Providers)
        {
            ArgumentException.ThrowIfNotEqual(snapshots.ContainsKey(binding.SourceId), true, nameof(sourceSnapshots));
            ArgumentException.ThrowIfNotEqual(captured.ContainsKey(binding.SourceId), true, nameof(sources));
        }
        Selection = selection;
        SourceSnapshots = snapshots.ToImmutable();
        _sources = captured.ToImmutable();
        _timeProvider = timeProvider;
        _logger = logger;
        _catalogLogger = catalogLogger;
    }

    /// <summary>Gets the original request and exact authored selection retained through discovery.</summary>
    /// <value>Immutable evidence without inferred aliases or authority.</value>
    internal ToolDiscoverySelection Selection { get; }

    /// <summary>Gets every exact source publication read during discovery.</summary>
    /// <value>A normalized immutable map, including empty sources; no property access contacts a provider.</value>
    internal ImmutableDictionary<ToolSourceId, ToolProviderSnapshot> SourceSnapshots { get; }

    /// <summary>Transfers the owned source graph into one already merged and preflighted catalog.</summary>
    /// <param name="snapshot">The nonnull catalog for the same complete request identity and configuration evidence.</param>
    /// <param name="cancellationToken">Cancellation checked at the synchronized handoff before local catalog construction.</param>
    /// <returns>The new catalog owner; disposing this discovery owner afterward cannot close it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException">Catalog request correlation, source versions, membership, or selected descriptors differ.</exception>
    /// <exception cref="InvalidOperationException">Closure or an earlier transfer already claimed the source graph.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before handoff.</exception>
    /// <remarks>The caller completes merge and schema/capability preflight before handoff. Construction uses retained publications, never live snapshot getters. Failed construction retains all ownership here; a successful transfer is not revoked by later cancellation.</remarks>
    internal ToolCatalogCapture TransferToCatalog(ToolCatalogSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var request = Selection.Request;
        ArgumentException.ThrowIfNotEqual(snapshot.AgentId == request.AgentId && snapshot.SessionId == request.SessionId && snapshot.RunId == request.RunId
            && snapshot.Identity == request.Identity && snapshot.SecurityPolicy == request.Authorization.PolicySnapshot
            && snapshot.AgentDefinitionRevision == request.AgentDefinitionRevision && snapshot.ConfigurationVersion == request.Configuration.Version, true, nameof(snapshot));
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoveryTransfer, request, _timeProvider, _logger);
        try
        {
            ToolCatalogCapture catalog;
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_closed) { throw new InvalidOperationException("The discovered source graph is already closed or transferred."); }
                catalog = new ToolCatalogCapture(snapshot, _sources, SourceSnapshots, _timeProvider, _catalogLogger);
                _closed = true;
                _ = _disposal.TrySetResult();
            }
            observation.Complete("transferred");
            return catalog;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled");
            throw;
        }
    }

    /// <summary>Closes the discovery owner and awaits every source cleanup unless ownership already transferred.</summary>
    /// <returns>Shared completion and failure; repeated disposal never repeats a source cleanup.</returns>
    /// <remarks>All source cleanups start before any is awaited. Multiple failures remain in ordinal source-ID order. Provider services and borrowed invokers retain their original disposal owner.</remarks>
    public async ValueTask DisposeAsync()
    {
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoveryClose, Selection.Request, _timeProvider, _logger);
        bool cleanup;
        lock (_gate)
        {
            cleanup = !_closed;
            _closed = true;
        }
        if (cleanup) { _ = DisposeSourcesAsync(); }
        await _disposal.Task.ConfigureAwait(false);
        observation.Complete("closed");
    }

    private async Task DisposeSourcesAsync()
    {
        Debug.Assert(_closed, "Closure claims cleanup before release callbacks begin.");
        try
        {
            var failures = await ReleaseSourcesAsync(Selection.Request, [.. _sources], _timeProvider, _logger).ConfigureAwait(false);
            _ = failures.IsEmpty ? _disposal.TrySetResult()
                : _disposal.TrySetException(failures.Length == 1 ? failures[0] : new AggregateException("Discovered tool source cleanup failed.", failures));
        }
        catch (Exception failure) { _ = _disposal.TrySetException(failure); }
    }

    /// <summary>Releases a complete or partial normalized source-owner set without cancelling cleanup.</summary>
    /// <param name="request">The nonnull original request for safe correlation.</param>
    /// <param name="sources">Initialized distinct nondefault source keys and distinct nonnull owner instances.</param>
    /// <param name="timeProvider">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull logger belonging to the current owner.</param>
    /// <returns>All cleanup failures in ordinal source-ID order; every callback is started before awaiting any.</returns>
    /// <exception cref="ArgumentNullException">A required reference or source owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A source key is default.</exception>
    /// <exception cref="ArgumentException">The array is default, a key repeats, or an owner is reused.</exception>
    internal static async Task<ImmutableArray<Exception>> ReleaseSourcesAsync(ToolDiscoveryRequest request,
        ImmutableArray<KeyValuePair<ToolSourceId, IToolProviderCapture>> sources, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNotEqual(sources.IsDefault, false, nameof(sources));
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        var keys = new HashSet<ToolSourceId>();
        var identities = new HashSet<IToolProviderCapture>(ReferenceEqualityComparer.Instance);
        foreach (var pair in sources)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(sources));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(sources));
            ArgumentException.ThrowIfNotEqual(keys.Add(pair.Key) && identities.Add(pair.Value), true, nameof(sources));
        }
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSources, request, timeProvider, logger);
        var releases = sources.OrderBy(static pair => pair.Key.Value, StringComparer.Ordinal).Select(pair => ReleaseAsync(pair.Key, pair.Value, request, timeProvider, logger)).ToArray();
        ImmutableArray<Exception> failures = [.. (await Task.WhenAll(releases).ConfigureAwait(false)).OfType<Exception>()];
        observation.Complete(failures.IsEmpty ? "disposed" : "failed");
        return failures;
    }

    private static async Task<Exception?> ReleaseAsync(ToolSourceId sourceId, IToolProviderCapture source, ToolDiscoveryRequest request, TimeProvider timeProvider, ILogger logger)
    {
        Debug.Assert(sourceId != default && source is not null && request is not null && timeProvider is not null && logger is not null, "Cleanup validates every source owner and observation dependency before any callback.");
        using var observation = new ToolDiscoveryObservation(AgentKitActivityNames.ToolCatalogDiscoveryDisposeSource, request, timeProvider, logger, sourceId);
        try { await source.DisposeAsync().ConfigureAwait(false); observation.Complete("disposed"); return null; }
        catch (Exception failure) { return failure; }
    }
}
