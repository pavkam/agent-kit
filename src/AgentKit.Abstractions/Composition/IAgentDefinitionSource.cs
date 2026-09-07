// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Contributes a complete set of agent definitions to the engine-wide
/// catalog.
/// </summary>
/// <remarks>
/// <para>
/// Sources are additive and are read concurrently, so implementations must be
/// thread-safe. They are normally registered as singletons.
/// </para>
/// <para>
/// A source may publish definitions from code, configuration, a database, or
/// a remote control plane. Whatever the origin, it returns its complete
/// current set; the catalog composes sources by precedence rather than
/// merging partial edits.
/// </para>
/// <para>
/// Reading must not mutate agent state. A source that performs I/O owns its
/// own caching and failure policy, and a failed read must leave the catalog's
/// last good snapshot intact.
/// </para>
/// </remarks>
public interface IAgentDefinitionSource
{
    /// <summary>Gets this source's stable identity.</summary>
    /// <value>
    /// Used to attribute conflicts and diagnostics to the registration that
    /// produced them.
    /// </value>
    public AgentDefinitionSourceId SourceId { get; }

    /// <summary>
    /// Reads this source's complete current contribution.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The complete definition set this source currently publishes. An empty
    /// contribution is valid.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default);
}
