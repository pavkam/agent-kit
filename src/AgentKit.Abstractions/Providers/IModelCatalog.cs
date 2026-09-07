// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Publishes the engine-wide, versioned view of configured models that
/// selection reads from.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is shared by every agent definition in one engine and is used
/// concurrently, so implementations must be thread-safe and are normally
/// registered as singletons.
/// </para>
/// <para>
/// Composition is additive over registered
/// <see cref="IModelDescriptorSource"/> instances. An invalid refresh leaves
/// the last good snapshot active rather than publishing a broken or empty
/// catalog into running agents.
/// </para>
/// <para>
/// The catalog holds configuration only. It never owns provider clients,
/// credentials, or connections, which is what allows a snapshot to be handed
/// to selection without granting any authority to perform I/O.
/// </para>
/// </remarks>
public interface IModelCatalog
{
    /// <summary>
    /// Gets the current immutable catalog snapshot.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that cancels the read. A cached snapshot normally completes
    /// synchronously.
    /// </param>
    /// <returns>
    /// The current snapshot. The same instance may be returned to many
    /// callers because it is immutable.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
