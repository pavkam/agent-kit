// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable evidence one history-preparation pass evaluates.</summary>
/// <remarks>
/// The request carries no session execution capability or hook dispatch context: preparation is a deterministic
/// in-memory pass. Callers supply the exact source cursor and the already-loaded messages.
/// </remarks>
public sealed record HistoryPreparationRequest
{
    /// <summary>Initializes one history-preparation request.</summary>
    /// <param name="sourceCursor">The exact source-history watermark for the loaded messages.</param>
    /// <param name="messages">The eligible conversation messages in ascending commit order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceCursor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default array or contains null.</exception>
    public HistoryPreparationRequest(MessageCursor sourceCursor, ImmutableArray<AgentMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(sourceCursor);
        ArgumentException.ThrowIfContainsNull(messages);
        SourceCursor = sourceCursor;
        Messages = messages;
    }

    /// <summary>Gets the exact source-history watermark.</summary>
    public MessageCursor SourceCursor { get; }

    /// <summary>Gets the eligible conversation messages in ascending commit order.</summary>
    public ImmutableArray<AgentMessage> Messages { get; }
}
