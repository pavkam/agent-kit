// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds the instants across which an approval and any grant it produces remain valid.</summary>
public sealed record ApprovalValidityWindow
{
    /// <summary>Initializes an approval validity window.</summary>
    /// <param name="notBefore">The earliest valid instant.</param>
    /// <param name="expiresAt">The exclusive expiry instant.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expiresAt"/> is not later than <paramref name="notBefore"/>.</exception>
    public ApprovalValidityWindow(DateTimeOffset notBefore, DateTimeOffset expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, notBefore);
        NotBefore = notBefore;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the earliest valid instant.</summary>
    public DateTimeOffset NotBefore { get; }
    /// <summary>Gets the exclusive expiry instant.</summary>
    public DateTimeOffset ExpiresAt { get; }
}
