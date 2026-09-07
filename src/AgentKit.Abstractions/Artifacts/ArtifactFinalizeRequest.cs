// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic publication of one staged preparation.</summary>
public sealed record ArtifactFinalizeRequest
{
    /// <summary>Initializes a publication request.</summary>
    /// <param name="preparationId">The staged preparation.</param>
    /// <param name="agentId">The acting agent.</param>
    /// <param name="sessionId">The optional owning session.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="idempotencyKey">The caller-owned replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public ArtifactFinalizeRequest(ArtifactPreparationId preparationId, AgentId agentId, SessionId? sessionId, ToolCallId? toolCallId, OperationCorrelation correlation, ExecutionIdentity identity, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; AgentId = agentId; SessionId = sessionId; ToolCallId = toolCallId;
        Correlation = correlation; Identity = identity; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the staged preparation.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the acting agent.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the optional owning session.</summary>
    public SessionId? SessionId { get; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId? ToolCallId { get; }
    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
