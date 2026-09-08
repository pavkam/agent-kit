// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reads host-published run profiles by exact agent and definition revision.</summary>
public interface IAgentRunProfilePublicationReader
{
    /// <summary>Gets the side-effect-free materialized snapshot used for readiness and composition validation.</summary>
    /// <value>The immutable snapshot, or null before the implementation is ready.</value>
    public AgentRunProfilePublicationSnapshot? CurrentSnapshot { get; }

    /// <summary>Reads one exact published binding without latest-revision fallback.</summary>
    /// <param name="agentId">The non-default agent identity.</param>
    /// <param name="agentDefinitionRevision">The exact nonnegative definition revision.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>A found publication or typed unavailable result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default or the revision is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<AgentRunProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        CancellationToken cancellationToken = default);
}
