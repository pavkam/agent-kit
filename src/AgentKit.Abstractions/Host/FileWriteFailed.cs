// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The write failed because of an unexpected typed I/O failure at the host boundary.</summary>
public sealed record FileWriteFailed: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteFailed"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive description of the failure.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public FileWriteFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive description of the failure.</summary>
    public string SafeMessage { get; init; }
}
