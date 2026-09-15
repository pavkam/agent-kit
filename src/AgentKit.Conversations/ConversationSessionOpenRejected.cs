// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports a content-safe open rejection without changing the current conversation binding.</summary>
public sealed record ConversationSessionOpenRejected: ConversationSessionOpenResult
{
    /// <summary>Initializes an open rejection.</summary>
    /// <param name="safeMessage">The nonblank content-safe reason.</param>
    public ConversationSessionOpenRejected(string safeMessage)
    { ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage); SafeMessage = safeMessage; }
    /// <summary>Gets the content-safe rejection reason.</summary><value>Nonblank text.</value>
    public string SafeMessage { get; }
}
