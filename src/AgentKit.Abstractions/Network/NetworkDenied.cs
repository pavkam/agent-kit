// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The request was rejected by the configured destination policy before any network I/O occurred.</summary>
public sealed record NetworkDenied: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkDenied"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
