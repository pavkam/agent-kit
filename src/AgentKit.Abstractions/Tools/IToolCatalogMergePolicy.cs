// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Chooses explicitly configured bindings from a complete catalog collision graph or rejects exposure.</summary>
/// <remarks>Implementations are concurrently callable and cannot mutate captured publications, create aliases or identities, grant authority, or acquire invokers. The catalog validates every returned choice against the supplied graph. Host policies may resolve configured collisions; the first-party default rejects all collisions.</remarks>
public interface IToolCatalogMergePolicy
{
    /// <summary>Resolves the complete graph in one decision before any alias is advertised.</summary>
    /// <param name="context">The nonnull complete validated contribution and collision evidence.</param>
    /// <param name="cancellationToken">Cancellation before decision transfer; cancellation propagates rather than becoming rejection.</param>
    /// <returns>A nonnull closed rejection or complete selection, containing only existing contribution evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before decision transfer.</exception>
    /// <remarks>A selection chooses exactly one candidate per distinct identity and one existing assignment per distinct alias. It cannot silently remove an unambiguous contribution. Alias bindings must agree with the selected descriptor, source version, and policy. Missing targets cannot be repaired by merge policy.</remarks>
    public ValueTask<ToolCatalogMergeDecision> ResolveAsync(ToolCatalogMergeContext context, CancellationToken cancellationToken = default);
}
