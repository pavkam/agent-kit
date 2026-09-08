// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Returns different readiness snapshots on successive reads to expose validation races.</summary>
internal sealed class AlternatingRunProfilePublicationReader: IAgentRunProfilePublicationReader
{
    private readonly AgentRunProfilePublicationSnapshot _first;
    private readonly AgentRunProfilePublicationSnapshot _later;

    /// <summary>Initializes the adversarial readiness sequence and runtime result.</summary>
    public AlternatingRunProfilePublicationReader(
        AgentRunProfilePublicationSnapshot first,
        AgentRunProfilePublicationSnapshot later,
        AgentRunProfilePublicationResult result)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(later);
        ArgumentNullException.ThrowIfNull(result);
        _first = first;
        _later = later;
        Result = result;
    }

    /// <summary>Gets how many readiness reads occurred.</summary>
    public int SnapshotReads { get; private set; }

    /// <summary>Gets or sets the runtime publication result.</summary>
    public AgentRunProfilePublicationResult Result { get; set; }

    /// <inheritdoc/>
    public AgentRunProfilePublicationSnapshot CurrentSnapshot =>
        ++SnapshotReads == 1 ? _first : _later;

    /// <inheritdoc/>
    public ValueTask<AgentRunProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result);
    }
}
