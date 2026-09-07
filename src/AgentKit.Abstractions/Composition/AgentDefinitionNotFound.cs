// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The engine hosts no agent with the requested identity.
/// </summary>
/// <remarks>
/// This is a normal typed outcome rather than an exception, because an
/// unknown agent identity usually arrives from outside the process — a route
/// parameter, a queue message, or a stale client — and a host should be able
/// to turn it into a 404 without catching.
/// </remarks>
public sealed record AgentDefinitionNotFound: AgentDefinitionResolution
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentDefinitionNotFound"/> record.
    /// </summary>
    /// <param name="agentId">The identity that resolved to nothing.</param>
    public AgentDefinitionNotFound(AgentId agentId) => AgentId = agentId;

    /// <summary>Gets the identity that resolved to nothing.</summary>
    /// <value>
    /// Retained verbatim, including a default identity, so a caller can log
    /// exactly what was asked for.
    /// </value>
    public AgentId AgentId { get; init; }
}
