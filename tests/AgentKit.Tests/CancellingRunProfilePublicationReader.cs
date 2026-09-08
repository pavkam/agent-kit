// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Cancels its caller token before returning a configured noncooperative read result.</summary>
internal sealed class CancellingRunProfilePublicationReader: IAgentRunProfilePublicationReader
{
    private readonly CancellationTokenSource _cancellation;
    private readonly AgentRunProfilePublicationResult _result;

    /// <summary>Initializes a ready reader that cancels before each runtime result.</summary>
    public CancellingRunProfilePublicationReader(
        AgentRunProfilePublicationSnapshot currentSnapshot,
        AgentRunProfilePublicationResult result,
        CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(cancellation);
        CurrentSnapshot = currentSnapshot;
        _result = result;
        _cancellation = cancellation;
    }

    /// <inheritdoc/>
    public AgentRunProfilePublicationSnapshot CurrentSnapshot { get; }

    /// <inheritdoc/>
    public ValueTask<AgentRunProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        CancellationToken cancellationToken = default)
    {
        _cancellation.Cancel();
        return ValueTask.FromResult(_result);
    }
}
