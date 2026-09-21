// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests protected access to one exact committed artifact version.</summary>
public sealed record ArtifactReadRequest
{
    /// <summary>Initializes a committed-artifact read request.</summary>
    /// <param name="agentId">The acting agent.</param>
    /// <param name="sessionId">The optional active session.</param>
    /// <param name="toolCallId">The causing tool call, when applicable.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="authorization">The captured authorization evidence for authority selection.</param>
    /// <param name="reference">The exact portable reference.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    public ArtifactReadRequest(AgentId agentId, SessionId? sessionId, ToolCallId? toolCallId, OperationCorrelation correlation, ExecutionIdentity identity, SecurityAuthorizationContext authorization, ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNotEqual(
            authorization.Scope, new SecurityAuthorizationScope(agentId, sessionId, correlation), nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        AgentId = agentId;
        SessionId = sessionId;
        ToolCallId = toolCallId;
        Correlation = correlation;
        Identity = identity;
        Authorization = authorization;
        Reference = reference;
    }

    /// <summary>Gets the acting agent.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the optional active session.</summary>
    public SessionId? SessionId { get; }
    /// <summary>Gets the causing tool call, when applicable.</summary>
    public ToolCallId? ToolCallId { get; }
    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the captured authorization evidence.</summary>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the exact portable reference.</summary>
    public ArtifactReference Reference { get; }
}
