// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Publishes the engine-wide, versioned set of agent definitions and resolves
/// one agent identity to its definition.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is shared by the whole engine and is used concurrently, so
/// implementations must be thread-safe and are normally registered as
/// singletons.
/// </para>
/// <para>
/// Composition is additive over registered
/// <see cref="IAgentDefinitionSource"/> instances, ordered by source
/// precedence. A new snapshot is published only after complete validation; an
/// invalid reload leaves the previous snapshot active rather than taking
/// running agents offline.
/// </para>
/// <para>
/// The catalog holds declarative configuration only. It never owns loops,
/// providers, stores, or credentials, which is what lets a snapshot be handed
/// around freely without conveying authority.
/// </para>
/// </remarks>
public interface IAgentDefinitionCatalog
{
    /// <summary>Gets the synchronously materialized immutable snapshot, or <see langword="null"/> before trusted bootstrap publication.</summary>
    /// <value>This property never performs source I/O and is safe for composition validation.</value>
    public AgentCatalogSnapshot? CurrentSnapshot { get; }

    /// <summary>
    /// Gets whether this catalog can publish new snapshots after the engine
    /// is built.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when later publication is possible. New
    /// admissions must re-resolve their pinned definition whenever this is
    /// true; retained snapshots remain recovery evidence rather than authority
    /// for new work.
    /// </value>
    public bool SupportsDynamicPublication { get; }

    /// <summary>
    /// Gets the current immutable catalog snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The current snapshot. The same instance may be returned to many
    /// callers because it is immutable.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one agent identity against the current snapshot.
    /// </summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// <see cref="ResolvedAgentDefinition"/> when the agent exists and is
    /// usable, <see cref="AgentDefinitionNotFound"/> when no such agent is
    /// hosted, or <see cref="InvalidAgentDefinition"/> when the agent exists
    /// but its definition cannot be used.
    /// </returns>
    /// <remarks>
    /// An unknown or unusable agent is a typed outcome rather than an
    /// exception, because agent identities routinely arrive from outside the
    /// process.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<AgentDefinitionResolution> ResolveAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default);
}
