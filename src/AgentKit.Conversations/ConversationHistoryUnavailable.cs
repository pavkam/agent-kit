// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports that a bounded conversation-history read could not be completed.</summary>
/// <remarks>
/// The reason is safe for direct application display and does not expose session addresses, stored content,
/// provider details, exception messages, or authorization evidence.
/// </remarks>
public sealed record ConversationHistoryUnavailable: ConversationHistoryReadResult
{
    /// <summary>Initializes an unavailable conversation-history result.</summary>
    /// <param name="safeMessage">The nonblank, content-safe reason suitable for application display.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public ConversationHistoryUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-safe reason the history read is unavailable.</summary>
    /// <value>Nonblank text that contains no stored conversation content or sensitive routing evidence.</value>
    public string SafeMessage { get; }
}
