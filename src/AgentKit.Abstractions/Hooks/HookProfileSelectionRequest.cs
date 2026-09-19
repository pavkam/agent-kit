// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one hook profile selection, carrying only the scope identities available at the requesting stage.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. Some hook points occur before an <see cref="AgentId"/> is known (engine construction),
/// so <see cref="AgentId"/> here is optional rather than a fabricated default; a stage that has not resolved an
/// agent omits it.
/// </para>
/// <para>
/// <see cref="RequestedProfile"/> is the explicit selection carried by an <c>AgentDefinition.HookProfile</c> or an
/// explicit host configuration; when absent, the selector falls back to its own documented default profile.
/// </para>
/// </remarks>
public sealed record HookProfileSelectionRequest
{
    /// <summary>Initializes a new instance of the <see cref="HookProfileSelectionRequest"/> record.</summary>
    /// <param name="requestedProfile">The explicitly requested profile, or <see langword="null"/> to select the default.</param>
    /// <param name="agentId">The agent this selection is for, or <see langword="null"/> before an agent is resolved.</param>
    public HookProfileSelectionRequest(HookProfileKey? requestedProfile, AgentId? agentId)
    {
        RequestedProfile = requestedProfile;
        AgentId = agentId;
    }

    /// <summary>Gets the explicitly requested profile, or <see langword="null"/> to select the default.</summary>
    public HookProfileKey? RequestedProfile { get; }

    /// <summary>Gets the agent this selection is for, or <see langword="null"/> before an agent is resolved.</summary>
    public AgentId? AgentId { get; }
}
