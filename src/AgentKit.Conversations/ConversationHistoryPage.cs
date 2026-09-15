// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Contains one bounded, ordered projection of messages from the bound session branch.</summary>
/// <remarks>
/// The page bound applies to scanned session entries, so operational entries may advance <see cref="NextCursor"/>
/// without producing a message. <see cref="Messages"/> contains only immutable <see cref="MessageSessionEntry.Message"/>
/// values in their source entry order. Passing <see cref="NextCursor"/> to the next read continues without rereading
/// entries.
/// </remarks>
public sealed record ConversationHistoryPage: ConversationHistoryReadResult
{
    /// <summary>Initializes one successful conversation-history page.</summary>
    /// <param name="messages">The initialized immutable messages in source entry order.</param>
    /// <param name="nextCursor">The stable whole-session sequence through which this read scanned.</param>
    /// <param name="complete">
    /// <see langword="true"/> when no later entry was visible on the bound branch at read time; otherwise
    /// <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default, uninitialized array.</exception>
    public ConversationHistoryPage(
        ImmutableArray<AgentMessage> messages,
        SessionSequence nextCursor,
        bool complete)
    {
        ArgumentException.ThrowIfDefault(messages);
        Messages = messages;
        NextCursor = nextCursor;
        Complete = complete;
    }

    /// <summary>Gets the immutable messages in their source session-entry order.</summary>
    /// <value>An initialized array containing only messages committed on the selected branch path.</value>
    public ImmutableArray<AgentMessage> Messages { get; }

    /// <summary>Gets the sequence through which the underlying session record was scanned.</summary>
    /// <value>The cursor to pass as the next read's exclusive starting sequence.</value>
    public SessionSequence NextCursor { get; }

    /// <summary>Gets whether the read reached the branch tip visible at read time.</summary>
    /// <value><see langword="true"/> when no later session entry was visible; otherwise <see langword="false"/>.</value>
    public bool Complete { get; }

    /// <inheritdoc/>
    public bool Equals(ConversationHistoryPage? other) =>
        other is not null
        && Messages.SequenceEqual(other.Messages)
        && NextCursor.Equals(other.NextCursor)
        && Complete == other.Complete;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var message in Messages)
        {
            hash.Add(message);
        }

        hash.Add(NextCursor);
        hash.Add(Complete);
        return hash.ToHashCode();
    }
}
