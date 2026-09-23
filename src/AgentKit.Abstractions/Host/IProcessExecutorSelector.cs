// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one registered process executor profile.</summary>
public interface IProcessExecutorSelector
{
    /// <summary>Resolves the executor registered for one profile key.</summary>
    /// <param name="key">The authored executor profile key.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A closed selection outcome.</returns>
    public ValueTask<ProcessExecutorSelectionResult> SelectAsync(
        ProcessExecutorKey key,
        CancellationToken cancellationToken = default);
}
