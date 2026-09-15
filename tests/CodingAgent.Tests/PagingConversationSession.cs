// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;
using AgentKit.Conversations;

/// <summary>Supplies deterministic durable-history pages to CodingAgent hydration tests.</summary>
internal sealed class PagingConversationSession(
    Func<SessionSequence, ConversationHistoryReadResult> read): IConversationSession
{
    internal List<SessionSequence> Cursors { get; } = [];

    public ValueTask<ConversationHistoryReadResult> ReadHistoryAsync(
        SessionSequence afterSequence,
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        maximumEntries.ShouldBe(128);
        Cursors.Add(afterSequence);
        return ValueTask.FromResult(read(afterSequence));
    }

    public Task<ConversationTurnResult> SendAsync(
        string userText,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConversationTurnResult(true, []));
}
