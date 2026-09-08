// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Separates the readiness snapshot from runtime reads for pinning tests.</summary>
internal sealed class MutableRunProfilePublicationReader: IAgentRunProfilePublicationReader
{
    /// <summary>Initializes a reader with independently controlled readiness and read results.</summary>
    public MutableRunProfilePublicationReader(
        AgentRunProfilePublicationSnapshot? currentSnapshot,
        AgentRunProfilePublicationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        CurrentSnapshot = currentSnapshot;
        Result = result;
    }

    /// <inheritdoc/>
    public AgentRunProfilePublicationSnapshot? CurrentSnapshot { get; }

    /// <summary>Gets or sets the result returned by subsequent reads.</summary>
    public AgentRunProfilePublicationResult Result { get; set; }

    /// <summary>Gets the number of runtime reads.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<AgentRunProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reads++;
        return ValueTask.FromResult(Result);
    }
}
