// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Represents the terminal outcome of one bounded conversation-history read.</summary>
/// <remarks>
/// This closed result hierarchy distinguishes a successfully read page from an unavailable read without exposing
/// session-store implementation details. Callers continue successful reads with the returned
/// <see cref="ConversationHistoryPage.NextCursor"/> until <see cref="ConversationHistoryPage.Complete"/> is
/// <see langword="true"/>.
/// </remarks>
public abstract record ConversationHistoryReadResult
{
    /// <summary>Initializes the closed base for conversation-history read outcomes.</summary>
    private protected ConversationHistoryReadResult()
    {
    }
}
