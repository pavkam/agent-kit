// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures a bounded message projection and its repairs at one exact history cursor.</summary>
public sealed record HistoryView
{
    /// <summary>Creates an immutable history view.</summary>
    /// <param name="sourceCursor">The exact source-history watermark.</param>
    /// <param name="messages">The initialized ordered projected messages.</param>
    /// <param name="repairs">The initialized ordered repair evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceCursor"/> is null.</exception>
    /// <exception cref="ArgumentException">An array is uninitialized or contains null, or a message coordinate differs from the source cursor.</exception>
    public HistoryView(MessageCursor sourceCursor, ImmutableArray<AgentMessage> messages, ImmutableArray<HistoryRepair> repairs)
    {
        ArgumentNullException.ThrowIfNull(sourceCursor);
        ArgumentException.ThrowIfContainsNull(messages);
        ArgumentException.ThrowIfContainsNull(repairs);
        foreach (var message in messages)
        {
            ArgumentException.ThrowIfNotEqual(message.AgentId, sourceCursor.AgentId, nameof(messages));
            ArgumentException.ThrowIfNotEqual(message.SessionId, sourceCursor.SessionId, nameof(messages));
            ArgumentException.ThrowIfNotEqual(message.ConversationId, sourceCursor.ConversationId, nameof(messages));
        }

        SourceCursor = sourceCursor;
        Messages = messages;
        Repairs = repairs;
    }

    /// <summary>Gets the exact source-history watermark.</summary><value>The nonnull captured cursor.</value>
    public MessageCursor SourceCursor { get; }
    /// <summary>Gets the projected messages in source order.</summary><value>An initialized sequence without null elements.</value>
    public ImmutableArray<AgentMessage> Messages { get; }
    /// <summary>Gets ordered repair evidence.</summary><value>An initialized sequence without null elements.</value>
    public ImmutableArray<HistoryRepair> Repairs { get; }

    /// <summary>Compares the exact cursor, ordered messages, and ordered repairs.</summary><param name="other">The view to compare.</param><returns>True when all evidence is equal.</returns>
    public bool Equals(HistoryView? other) => other is not null && SourceCursor == other.SourceCursor && Messages.SequenceEqual(other.Messages) && Repairs.SequenceEqual(other.Repairs);
    /// <summary>Returns a hash compatible with complete ordered equality.</summary><returns>A hash over the cursor and all ordered elements.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(SourceCursor);
        foreach (var message in Messages)
        {
            hash.Add(message);
        }

        foreach (var repair in Repairs)
        {
            hash.Add(repair);
        }
        return hash.ToHashCode();
    }
}
