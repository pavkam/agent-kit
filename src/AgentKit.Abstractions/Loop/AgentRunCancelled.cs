// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run was cancelled before it reached a final assistant message.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Any
/// truthful partial output the cancelled attempt produced is preserved as
/// an interrupted message in durable history before this outcome is
/// returned.
/// </remarks>
public sealed record AgentRunCancelled: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunCancelled"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public AgentRunCancelled(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
