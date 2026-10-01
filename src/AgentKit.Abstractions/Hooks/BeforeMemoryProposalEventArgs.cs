// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The event raised before a memory proposal is evaluated by memory policy.</summary>
/// <remarks>
/// The proposal and every identity are read-only. The only writable member is <see cref="Veto"/>: a hook may refuse the
/// proposal, and a refusal is recorded as a policy denial. A hook cannot accept a proposal, because only a registered
/// memory policy can allow retention.
/// </remarks>
public sealed class BeforeMemoryProposalEventArgs: AgentHookEventArgs, IAgentScopedHookStage, IShortCircuitingHookArgs
{
    /// <summary>Initializes the event arguments.</summary>
    /// <param name="dispatch">The dispatch identity for <see cref="AgentHookPoints.BeforeMemoryProposal"/>.</param>
    /// <param name="proposal">The proposal about to be evaluated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="proposal"/> is null.</exception>
    public BeforeMemoryProposalEventArgs(HookDispatchMetadata dispatch, MemoryProposal proposal)
        : base(dispatch)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        Proposal = proposal;
    }

    /// <summary>Gets the agent that proposed the memory.</summary>
    public AgentId AgentId => Proposal.Context.AgentId;

    /// <summary>Gets the session the proposal came from, when it has one.</summary>
    public SessionId? SessionId => Proposal.Context.SessionId;

    /// <summary>Gets the proposal about to be evaluated.</summary>
    public MemoryProposal Proposal { get; }

    /// <summary>Gets or sets the refusal that stops the proposal, or <see langword="null"/> to let it continue.</summary>
    public MemoryHookVeto? Veto { get; set; }

    /// <inheritdoc/>
    public bool IsShortCircuited => Veto is not null;

    /// <inheritdoc/>
    public override object? CaptureMutableState() => Veto;

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot) => Veto = snapshot as MemoryHookVeto;
}
