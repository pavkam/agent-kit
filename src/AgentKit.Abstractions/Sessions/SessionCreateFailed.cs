// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Session creation failed before any session state was durably committed.
/// </summary>
public sealed record SessionCreateFailed: SessionCreateResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreateFailed"/> record.</summary>
    /// <param name="safeMessage">A safe, non-sensitive description of the failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public SessionCreateFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a safe, non-sensitive description of the failure.</summary>
    public string SafeMessage { get; init; }
}
