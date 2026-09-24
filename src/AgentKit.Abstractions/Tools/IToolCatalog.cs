// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Captures one run-bound tool catalog by discovering selected toolsets, merging contributions, and preflighting
/// schemas before handing ownership to an <see cref="IToolCatalogCapture"/>.
/// </summary>
/// <remarks>
/// Implementations are safe to call concurrently. Each successful <see cref="CaptureAsync"/> returns an independent
/// capture whose snapshot is immutable; the coordinator retains neither the capture nor run-scoped services after
/// the call completes.
/// </remarks>
public interface IToolCatalog
{
    /// <summary>Discovers, merges, and preflights one complete catalog for <paramref name="request"/>.</summary>
    /// <param name="request">The nonnull run-bound discovery request, including authored toolsets and model capabilities.</param>
    /// <param name="cancellationToken">Cancels work before ownership of a capture transfers to the caller.</param>
    /// <returns>An owned capture ready for resolver and executor use.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Merge or schema preflight rejected the discovered contributions.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before capture completes.</exception>
    public ValueTask<IToolCatalogCapture> CaptureAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}
