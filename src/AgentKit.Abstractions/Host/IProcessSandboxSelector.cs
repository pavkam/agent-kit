// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one registered sandbox provider by profile identity.</summary>
public interface IProcessSandboxSelector
{
    /// <summary>Resolves the sandbox provider registered for one profile identity.</summary>
    /// <param name="profileId">The sandbox profile identity.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A closed selection outcome.</returns>
    public ValueTask<ProcessSandboxSelectionResult> SelectAsync(
        SandboxProfileId profileId,
        CancellationToken cancellationToken = default);
}
