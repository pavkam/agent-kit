// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The first-party model catalog. It composes every registered descriptor
/// source into one immutable, versioned snapshot and keeps the last good
/// snapshot when a refresh fails.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is engine-wide and is read concurrently by every agent
/// definition, so it is registered as a singleton and is thread-safe. Reads
/// are lock-free once a snapshot has been published; only composition is
/// serialized.
/// </para>
/// <para>
/// Sources are composed in deterministic registration order. A duplicate
/// alias is a composition error rather than a last-one-wins merge, because
/// silently preferring one provider's descriptor over another's would change
/// which endpoint a run reaches without anyone asking for it.
/// </para>
/// <para>
/// This class holds configuration only. It never constructs provider clients
/// or resolves credentials, which is what makes handing a snapshot to
/// selection safe.
/// </para>
/// </remarks>
internal sealed class DefaultModelCatalog: IModelCatalog, IDisposable
{
    private readonly ImmutableArray<IModelDescriptorSource> _sources;
    private readonly ILogger<DefaultModelCatalog> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private ModelCatalogSnapshot? _snapshot;
    private long _version;

    /// <summary>
    /// Initializes the catalog over its additively registered sources.
    /// </summary>
    /// <param name="sources">
    /// The registered descriptor sources, composed in registration order.
    /// </param>
    /// <param name="logger">
    /// The logger used to record composition outcomes.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sources"/> or <paramref name="logger"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sources"/> contains a <see langword="null"/> element,
    /// or two sources declare the same
    /// <see cref="IModelDescriptorSource.SourceId"/>.
    /// </exception>
    public DefaultModelCatalog(
        IEnumerable<IModelDescriptorSource> sources,
        ILogger<DefaultModelCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);

        _sources = [.. sources];
        ArgumentException.ThrowIfContainsNull(_sources, nameof(sources));
        ThrowIfDuplicateSourceId(_sources, nameof(sources));

        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The first call composes the catalog; later calls return the published
    /// immutable snapshot without re-reading sources.
    /// </remarks>
    public async ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _snapshot)
        ?? await RefreshAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Recomposes every source into a new published snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels composition.</param>
    /// <returns>The newly published snapshot.</returns>
    /// <remarks>
    /// When composition fails and a previous snapshot exists, that snapshot
    /// stays active and the failure propagates to the caller who asked for the
    /// refresh. A broken configuration reload therefore cannot take running
    /// agents offline.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Two sources contribute the same model alias.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public async ValueTask<ModelCatalogSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.ModelCatalogRefresh);
        var entered = false;
        try
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            var descriptors = ImmutableArray.CreateBuilder<ModelDescriptor>();
            var owners = new Dictionary<ModelAlias, ModelDescriptorSourceId>();

            foreach (var source in _sources)
            {
                var contribution = await source.ReadAsync(cancellationToken).ConfigureAwait(false);

                foreach (var descriptor in contribution.ConversationModels)
                {
                    if (owners.TryGetValue(descriptor.Alias, out var existingOwner))
                    {
                        throw new InvalidOperationException(
                            $"Model alias '{descriptor.Alias}' is contributed by both source "
                            + $"'{existingOwner}' and source '{contribution.SourceId}'. "
                            + "Aliases must be unique across descriptor sources.");
                    }

                    owners[descriptor.Alias] = contribution.SourceId;
                    descriptors.Add(descriptor);
                }
            }

            var next = new ModelCatalogSnapshot(
                new ModelCatalogVersion(Interlocked.Increment(ref _version)),
                descriptors.ToImmutable());

            Volatile.Write(ref _snapshot, next);
            ProviderLog.CatalogComposed(
                _logger,
                next.Version.Value,
                next.ConversationModels.Length);
            _ = activity?.SetTag(AgentKitTagNames.ModelCatalogVersion, next.Version.Value);
            activity.SetSuccessful("published");
            ProviderMetrics.RecordCatalogRefresh("published");

            return next;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ProviderLog.CatalogRefreshCancelled(_logger);
            ProviderMetrics.RecordCatalogRefresh("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            ProviderLog.CatalogRefreshFailed(_logger, errorType);
            ProviderMetrics.RecordCatalogRefresh("failed");
            throw;
        }
        finally
        {
            if (entered)
            {
                _ = _refreshGate.Release();
            }
        }
    }

    /// <summary>
    /// Releases the semaphore that serializes catalog composition.
    /// </summary>
    /// <remarks>
    /// The container owns this singleton and disposes it at provider
    /// shutdown. Published snapshots are immutable and remain valid for any
    /// caller still holding one.
    /// </remarks>
    public void Dispose() => _refreshGate.Dispose();

    private static void ThrowIfDuplicateSourceId(
        ImmutableArray<IModelDescriptorSource> sources,
        string paramName)
    {
        var seen = new HashSet<ModelDescriptorSourceId>();
        foreach (var source in sources)
        {
            if (!seen.Add(source.SourceId))
            {
                throw new ArgumentException(
                    $"Value must not contain duplicate descriptor source id '{source.SourceId}'.",
                    paramName);
            }
        }
    }
}
