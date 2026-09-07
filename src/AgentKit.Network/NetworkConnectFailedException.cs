// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Marks a pinned-address connection failure for stable transport mapping.</summary>
internal sealed class NetworkConnectFailedException: Exception
{
    /// <summary>Initializes the internal marker exception.</summary>
    /// <param name="message">The safe diagnostic message.</param>
    /// <param name="kind">The stable failure classification.</param>
    /// <param name="innerException">The underlying socket or cancellation failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal NetworkConnectFailedException(
        string message,
        NetworkFailureKind kind = NetworkFailureKind.ConnectionFailed,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        Kind = kind;
    }

    /// <summary>Gets the stable failure classification.</summary>
    internal NetworkFailureKind Kind { get; }
}
