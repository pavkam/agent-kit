// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References committed terminal tool evidence without projecting or reconstructing its authoritative result.</summary>
/// <remarks>The session owner establishes that <see cref="SessionEntryId"/> committed; this value only preserves correlation for revalidation.</remarks>
public sealed record CommittedToolResultReference
{
    /// <summary>Initializes a committed tool-result reference.</summary>
    /// <param name="sessionEntryId">The terminal-record session entry.</param>
    /// <param name="toolCallId">The authoritative call identity.</param>
    /// <param name="turnId">The turn containing the correlated assistant call.</param>
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

    /// <summary>Gets the terminal-record entry identity.</summary>
    public SessionEntryId SessionEntryId { get; }

    /// <summary>Gets the correlated tool-call identity.</summary>
    public ToolCallId ToolCallId { get; }

    /// <summary>Gets the correlated turn identity.</summary>
    public TurnId TurnId { get; }
}
