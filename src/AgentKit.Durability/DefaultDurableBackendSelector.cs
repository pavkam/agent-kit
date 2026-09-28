// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Selects the backend named by an operation's captured execution context.</summary>
/// <remarks>
/// Selection is exact and never falls back: an operation whose captured backend key has no registered descriptor is
/// rejected rather than routed to a different backend. The selector reads catalog descriptors only and performs no I/O.
/// </remarks>
/// <param name="catalog">The engine-wide catalog of registered backend descriptors.</param>
internal sealed class DefaultDurableBackendSelector(IDurableBackendCatalog catalog): IDurableBackendSelector
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask<DurableBackendSelectionResult> SelectAsync(
        RecoverableOperationDescriptor operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        var backendKey = operation.ExecutionContext.BackendKey;
        foreach (var descriptor in catalog.GetDescriptors())
        {
            if (descriptor.Key == backendKey)
            {
                return ValueTask.FromResult<DurableBackendSelectionResult>(new DurableBackendSelected(descriptor));
            }
        }

        return ValueTask.FromResult<DurableBackendSelectionResult>(
            new DurableBackendSelectionRejected("No durable backend is registered for the captured backend key."));
    }
}
