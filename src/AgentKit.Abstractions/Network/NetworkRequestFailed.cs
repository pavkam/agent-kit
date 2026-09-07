// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The request did not complete because of a connection, protocol, or timeout failure.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SideEffectCertain"/> distinguishes a failure known to have
/// never reached the remote peer from one where the caller cannot prove
/// that; a caller retries only when it can independently establish
/// idempotency or this value confirms nothing was sent.
/// </remarks>
public sealed record NetworkRequestFailed: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkRequestFailed"/> record.</summary>
    /// <param name="kind">The category of this failure.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="sideEffectCertain">
    /// <see langword="true"/> when the transport can prove the request was
    /// never observably sent; <see langword="false"/> when whether it was
    /// sent is uncertain.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkRequestFailed(NetworkFailureKind kind, string safeMessage, bool sideEffectCertain)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        Kind = kind;
        SafeMessage = safeMessage;
        SideEffectCertain = sideEffectCertain;
    }

    /// <summary>Gets the category of this failure.</summary>
    public NetworkFailureKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transport can prove the request
    /// was never observably sent.
    /// </summary>
    public bool SideEffectCertain { get; init; }
}
