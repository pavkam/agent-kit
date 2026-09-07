// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The first-party agent-definition catalog. It composes every registered
/// source into one immutable, versioned snapshot using explicit source
/// precedence.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is engine-wide and read concurrently, so it is registered as a
/// singleton and is thread-safe. Reads are lock-free once a snapshot has been
/// published; only composition is serialized.
/// </para>
/// <para>
/// When two sources publish the same <see cref="AgentId"/>, the higher
/// <see cref="AgentDefinitionSourceSnapshot.Precedence"/> wins. Equal
/// precedence permits structurally identical definitions but rejects different
/// content, because silently preferring whichever source loaded first would
/// make an agent's behavior depend on registration order.
/// </para>
/// <para>
/// A failed composition leaves the previously published snapshot active, so a
/// broken reload cannot take running agents offline.
/// </para>
/// </remarks>
internal sealed class DefaultAgentDefinitionCatalog: IAgentDefinitionCatalog, IDisposable
{
    private readonly ImmutableArray<IAgentDefinitionSource> _sources;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<(AgentId, AgentDefinitionRevision), AgentDefinition> _publishedBindings = [];
    private AgentCatalogSnapshot? _snapshot;
    private long _version;

    /// <summary>
    /// Initializes the catalog over its additively registered sources.
    /// </summary>
    /// <param name="sources">
    /// The registered definition sources, composed by precedence.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sources"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sources"/> contains a <see langword="null"/> element or
    /// two sources declaring the same
    /// <see cref="IAgentDefinitionSource.SourceId"/>.
    /// </exception>
    public DefaultAgentDefinitionCatalog(IEnumerable<IAgentDefinitionSource> sources)
        : this(sources, [])
    {
    }

    /// <summary>
    /// Initializes the catalog and synchronously publishes a bootstrap catalog
    /// only when every registered source has a trusted materialized snapshot.
    /// </summary>
    /// <param name="sources">The unique registered definition sources.</param>
    /// <param name="bootstrapSnapshots">
    /// The unique materialized snapshots supplied by the trusted host
    /// bootstrap boundary. Every snapshot must name a registered source.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sources"/> or <paramref name="bootstrapSnapshots"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Either collection contains <see langword="null"/>, source identities
    /// are duplicated, or a bootstrap snapshot names an unregistered source.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Fully covered bootstrap snapshots contain conflicting definitions at
    /// equal precedence.
    /// </exception>
    public DefaultAgentDefinitionCatalog(
        IEnumerable<IAgentDefinitionSource> sources,
        IEnumerable<AgentDefinitionSourceSnapshot> bootstrapSnapshots)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(bootstrapSnapshots);

        _sources = [.. sources];
        ArgumentException.ThrowIfContainsNull(_sources, nameof(sources));

        ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(_sources, nameof(sources));

        var snapshots = bootstrapSnapshots.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(snapshots, nameof(bootstrapSnapshots));
        ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots, nameof(bootstrapSnapshots));
        ArgumentException.ThrowIfUnknownAgentDefinitionSource(
            snapshots,
            _sources,
            nameof(bootstrapSnapshots),
            nameof(sources));

        if (snapshots.Length == _sources.Length)
        {
            var snapshot = Compose(snapshots, Interlocked.Increment(ref _version));
            RememberBindings(snapshot);
            Volatile.Write(ref _snapshot, snapshot);
        }
    }

    /// <inheritdoc/>
    /// <value>
    /// Always <see langword="true"/>. Explicit refreshes can publish a new
    /// snapshot, so every new admission must revalidate its pinned definition.
    /// </value>
    public bool SupportsDynamicPublication => true;

    /// <inheritdoc/>
    public AgentCatalogSnapshot? CurrentSnapshot => Volatile.Read(ref _snapshot);

    /// <inheritdoc/>
    public async ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        cancellationToken.IsCancellationRequested
            ? await ValueTask.FromCanceled<AgentCatalogSnapshot>(cancellationToken)
            : Volatile.Read(ref _snapshot)
        ?? await RefreshAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<AgentDefinitionResolution> ResolveAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var definition = snapshot.FindDefinition(agentId);

        return definition is null
            ? new AgentDefinitionNotFound(agentId)
            : new ResolvedAgentDefinition(definition, snapshot.Version);
    }

    /// <summary>
    /// Recomposes every source into a new published snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels composition.</param>
    /// <returns>The newly published snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Two sources of equal precedence publish different content for the same
    /// agent identity, a source returns a malformed contribution, or a
    /// previously published revision is rebound to different content.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public async ValueTask<AgentCatalogSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshots = ImmutableArray.CreateBuilder<AgentDefinitionSourceSnapshot>(_sources.Length);

            foreach (var source in _sources)
            {
                var contribution = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (contribution is null || contribution.SourceId != source.SourceId)
                {
                    throw new InvalidOperationException("A definition source returned a malformed contribution.");
                }

                snapshots.Add(contribution);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var candidate = Compose(snapshots.ToImmutable(), _version);
            ValidateBindings(candidate);
            var next = new AgentCatalogSnapshot(
                new AgentCatalogVersion(Interlocked.Increment(ref _version)),
                candidate.Definitions);

            RememberBindings(next);
            Volatile.Write(ref _snapshot, next);
            return next;
        }
        finally
        {
            _ = _refreshGate.Release();
        }
    }

    private static AgentCatalogSnapshot Compose(ImmutableArray<AgentDefinitionSourceSnapshot> snapshots, long version)
    {
        var winners = new Dictionary<AgentId, (AgentDefinition Definition, int Precedence, AgentDefinitionSourceId Source)>();
        foreach (var contribution in snapshots)
        {
            foreach (var definition in contribution.Definitions)
            {
                if (!winners.TryGetValue(definition.Id, out var existing)
                    || contribution.Precedence > existing.Precedence)
                {
                    winners[definition.Id] = (definition, contribution.Precedence, contribution.SourceId);
                }
                else if (contribution.Precedence == existing.Precedence && existing.Definition != definition)
                {
                    throw new InvalidOperationException("Equal-precedence agent definitions must be structurally identical.");
                }
            }
        }

        return new AgentCatalogSnapshot(new AgentCatalogVersion(version), [.. winners.Values.Select(static winner => winner.Definition)]);
    }

    private void ValidateBindings(AgentCatalogSnapshot candidate)
    {
        foreach (var definition in candidate.Definitions)
        {
            if (_publishedBindings.TryGetValue((definition.Id, definition.Revision), out var existing) && !existing.Equals(definition))
            {
                throw new InvalidOperationException("A published agent revision cannot change content.");
            }
        }
    }

    private void RememberBindings(AgentCatalogSnapshot snapshot)
    {
        foreach (var definition in snapshot.Definitions)
        {
            _ = _publishedBindings.TryAdd((definition.Id, definition.Revision), definition);
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
}
