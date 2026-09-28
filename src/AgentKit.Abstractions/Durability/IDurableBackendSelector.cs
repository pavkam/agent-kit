// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one immutable durable backend descriptor for a recoverable operation.</summary>
public interface IDurableBackendSelector
{
    /// <summary>Selects a backend for one operation declaration.</summary>
    /// <param name="operation">The immutable recoverable operation descriptor.</param>
    /// <param name="cancellationToken">Cancels before selection completes.</param>
    /// <returns>A closed selection outcome.</returns>
    public ValueTask<DurableBackendSelectionResult> SelectAsync(
        RecoverableOperationDescriptor operation,
        CancellationToken cancellationToken = default);
}
