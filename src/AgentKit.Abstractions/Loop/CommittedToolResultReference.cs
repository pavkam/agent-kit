// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Preserves correlation to one committed terminal tool record without projecting or reconstructing its authoritative result.</summary>
/// <remarks>
/// This immutable value identifies evidence that a session owner has associated
/// with a tool call and turn. It carries no tool payload, does not establish
/// commitment on its own, and must not be used to recreate the authoritative
/// execution result or its bounded history projection.
/// </remarks>
public sealed record CommittedToolResultReference
{
    /// <summary>Initializes immutable correlation for one terminal tool record.</summary>
    /// <param name="sessionEntryId">The non-default session entry identity of the terminal record.</param>
    /// <param name="toolCallId">The non-default authoritative identity of the correlated tool call.</param>
    /// <param name="turnId">The non-default identity of the turn containing that assistant tool call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any identity is default.</exception>
    public CommittedToolResultReference(SessionEntryId sessionEntryId, ToolCallId toolCallId, TurnId turnId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionEntryId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolCallId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        SessionEntryId = sessionEntryId;
        ToolCallId = toolCallId;
        TurnId = turnId;
    }

    /// <summary>Gets the session entry identity for the terminal tool record.</summary>
    /// <value>A non-default identity used to locate and revalidate the record; it does not expose the record content.</value>
    public SessionEntryId SessionEntryId { get; }

    /// <summary>Gets the authoritative identity of the correlated tool call.</summary>
    /// <value>A non-default tool-call identity retained across execution, commitment, and continuation evaluation.</value>
    public ToolCallId ToolCallId { get; }

    /// <summary>Gets the identity of the turn that contained the correlated assistant tool call.</summary>
    /// <value>A non-default turn identity that prevents this reference from being associated with another turn.</value>
    public TurnId TurnId { get; }
}
