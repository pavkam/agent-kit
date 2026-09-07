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
/// precedence is a composition error rather than an arbitrary choice, because
/// silently preferring whichever source loaded first would make an agent's
/// behavior depend on registration order.
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
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = [.. sources];
        ArgumentException.ThrowIfContainsNull(_sources, nameof(sources));

        var seen = new HashSet<AgentDefinitionSourceId>();
        foreach (var source in _sources)
        {
            if (!seen.Add(source.SourceId))
            {
                throw new ArgumentException(
                    $"Value must not contain duplicate definition source id '{source.SourceId}'.",
                    nameof(sources));
            }
        }
    }

    /// <inheritdoc/>
    /// <value>
    /// Always <see langword="false"/>. This catalog composes once and then
    /// republishes only when a caller explicitly refreshes it.
    /// </value>
    public bool SupportsDynamicPublication => false;

    /// <inheritdoc/>
    public async ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        Volatile.Read(ref _snapshot)
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
    /// Two sources of equal precedence publish the same agent identity.
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
            var winners = new Dictionary<AgentId, (AgentDefinition Definition, int Precedence, AgentDefinitionSourceId Source)>();
            var order = new List<AgentId>();

            foreach (var source in _sources)
            {
                var contribution = await source.ReadAsync(cancellationToken).ConfigureAwait(false);

                foreach (var definition in contribution.Definitions)
                {
                    if (!winners.TryGetValue(definition.Id, out var existing))
                    {
                        winners[definition.Id] = (definition, contribution.Precedence, contribution.SourceId);
                        order.Add(definition.Id);
                        continue;
                    }

                    if (contribution.Precedence == existing.Precedence)
                    {
                        throw new InvalidOperationException(
                            $"Agent id '{definition.Id}' is published by source '{existing.Source}' and "
                            + $"source '{contribution.SourceId}' at the same precedence "
                            + $"{contribution.Precedence}. Give one source a higher precedence or remove "
                            + "the duplicate definition.");
                    }

                    if (contribution.Precedence > existing.Precedence)
                    {
                        winners[definition.Id] = (definition, contribution.Precedence, contribution.SourceId);
                    }
                }
            }

            var definitions = ImmutableArray.CreateBuilder<AgentDefinition>(order.Count);
            foreach (var id in order)
            {
                definitions.Add(winners[id].Definition);
            }

            var next = new AgentCatalogSnapshot(
                new AgentCatalogVersion(Interlocked.Increment(ref _version)),
                definitions.ToImmutable());

            Volatile.Write(ref _snapshot, next);
            return next;
        }
        finally
        {
            _ = _refreshGate.Release();
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
