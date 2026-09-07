// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Contributes a complete set of model descriptors to the engine-wide
/// catalog.
/// </summary>
/// <remarks>
/// <para>
/// Sources are additive and are read concurrently, so implementations must be
/// thread-safe. They are normally registered as singletons.
/// </para>
/// <para>
/// Discovery is itself a capability. A source may be a reviewed static
/// baseline, a generated vendor-feed snapshot, an application override, or
/// authoritative credential-scoped runtime discovery. AgentKit does not assume
/// every provider supports live discovery, and a source must not promote
/// name-based capability guesses to verified facts.
/// </para>
/// <para>
/// Reading is side-effect free with respect to agent state. A source that
/// performs network discovery owns its own caching and failure policy, and a
/// failed read must not corrupt the catalog's last good snapshot.
/// </para>
/// </remarks>
public interface IModelDescriptorSource
{
    /// <summary>Gets this source's stable identity.</summary>
    /// <value>
    /// Used to attribute composition diagnostics such as duplicate aliases to
    /// the registration that produced them.
    /// </value>
    public ModelDescriptorSourceId SourceId { get; }

    /// <summary>
    /// Reads this source's complete current contribution.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The complete descriptor set this source currently publishes. An empty
    /// contribution is valid.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<ModelDescriptorSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default);
}
