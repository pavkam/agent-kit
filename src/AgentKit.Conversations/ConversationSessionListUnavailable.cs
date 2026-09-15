// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports that conversation-session discovery is unavailable.</summary>
public sealed record ConversationSessionListUnavailable: ConversationSessionListResult
{
    /// <summary>Initializes an unavailable discovery outcome.</summary>
    /// <param name="safeMessage">The nonblank content-safe reason.</param>
    public ConversationSessionListUnavailable(string safeMessage)
    { ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage); SafeMessage = safeMessage; }
    /// <summary>Gets the content-safe reason.</summary><value>Nonblank text.</value>
    public string SafeMessage { get; }
}
