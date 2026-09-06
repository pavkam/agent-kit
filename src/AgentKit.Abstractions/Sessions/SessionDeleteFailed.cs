// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The deletion could not complete because of a store or transport failure.</summary>
public sealed record SessionDeleteFailed: SessionDeleteResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionDeleteFailed"/> record.</summary>
    /// <param name="safeMessage">A safe, non-sensitive description of the failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public SessionDeleteFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a safe, non-sensitive description of the failure.</summary>
    public string SafeMessage { get; init; }
}
