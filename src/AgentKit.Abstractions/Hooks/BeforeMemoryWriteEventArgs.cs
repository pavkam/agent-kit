// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The event raised after memory policy allowed a proposal and before the durable record is written.</summary>
/// <remarks>
/// The record, scope, classification, and retention are read-only. The only writable member is <see cref="Veto"/>: a hook
/// may refuse the write, and a refusal is recorded as a policy denial. No store write, grant, or enforcement intent exists
/// yet when this point runs.
/// </remarks>
public sealed class BeforeMemoryWriteEventArgs: AgentHookEventArgs, IAgentScopedHookStage, IShortCircuitingHookArgs
{
    /// <summary>Initializes the event arguments.</summary>
    /// <param name="dispatch">The dispatch identity for <see cref="AgentHookPoints.BeforeMemoryWrite"/>.</param>
    /// <param name="context">The captured memory operation the write belongs to.</param>
    /// <param name="record">The record about to be written.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BeforeMemoryWriteEventArgs(HookDispatchMetadata dispatch, MemoryOperationContext context, DurableMemoryRecord record)
        : base(dispatch)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(record);
        Context = context;
        Record = record;
    }

    /// <summary>Gets the captured memory operation the write belongs to.</summary>
    public MemoryOperationContext Context { get; }

    /// <summary>Gets the agent that owns the write.</summary>
    public AgentId AgentId => Context.AgentId;

    /// <summary>Gets the session the write came from, when it has one.</summary>
    public SessionId? SessionId => Context.SessionId;

    /// <summary>Gets the record about to be written.</summary>
    public DurableMemoryRecord Record { get; }

    /// <summary>Gets or sets the refusal that stops the write, or <see langword="null"/> to let it continue.</summary>
    public MemoryHookVeto? Veto { get; set; }

    /// <inheritdoc/>
    public bool IsShortCircuited => Veto is not null;

    /// <inheritdoc/>
    public override object? CaptureMutableState() => Veto;

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot) => Veto = snapshot as MemoryHookVeto;
}
