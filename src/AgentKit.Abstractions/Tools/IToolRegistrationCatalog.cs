// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves a complete authored toolset selection against materialized immutable registrations before discovery.</summary>
/// <remarks>Implementations are concurrently callable, retain stable publications and borrowed provider bindings for their lifetime, and perform no source I/O or runtime container lookup. Publication refresh creates a new registration view; existing selections remain pinned.</remarks>
public interface IToolRegistrationCatalog
{
    /// <summary>Resolves every selected toolset, policy family, and distinct provider as one complete immutable value.</summary>
    /// <param name="request">The nonnull coherent request whose authored order and identity are preserved.</param>
    /// <param name="cancellationToken">Cancellation checked before and during bounded local selection work.</param>
    /// <returns>The complete ordered selection, including each selected source once even when multiple toolsets share it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An authored toolset or its policy family is unavailable in the materialized view.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before selection transfer.</exception>
    /// <remarks>No partial selection is returned, no provider is contacted, and an empty authored selection never falls back to registered tools.</remarks>
    public ToolDiscoverySelection ResolveSelection(ToolDiscoveryRequest request, CancellationToken cancellationToken = default);
}
