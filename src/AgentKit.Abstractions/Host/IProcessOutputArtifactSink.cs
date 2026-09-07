// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Preserves complete bounded process output through an application-selected artifact coordinator.</summary>
public interface IProcessOutputArtifactSink
{
    /// <summary>Stages and publishes one complete output stream without changing process settlement.</summary>
    /// <param name="request">The complete output and causal execution facts.</param>
    /// <param name="cancellationToken">Cancels preservation without repeating the process effect.</param>
    /// <returns>A portable artifact reference or a safe typed rejection.</returns>
    public Task<ProcessOutputArtifactResult> StoreAsync(ProcessOutputArtifactRequest request, CancellationToken cancellationToken = default);
}
