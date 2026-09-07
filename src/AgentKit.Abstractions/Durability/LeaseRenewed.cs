// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The lease remains owned by the caller and its expiry has been extended.
/// </summary>
/// <remarks>
/// Renewal extends time only. It never changes the operation identity and
/// never allocates a new <see cref="FencingToken"/>, because a new generation
/// would invalidate the caller's own in-flight writes and defeat the purpose
/// of holding continuous ownership.
/// </remarks>
public sealed record LeaseRenewed: LeaseRenewalResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseRenewed"/> record.
    /// </summary>
    /// <param name="expiresAt">
    /// The extended expiry instant, produced from the lease manager's
    /// injected <see cref="TimeProvider"/>.
    /// </param>
    public LeaseRenewed(DateTimeOffset expiresAt) => ExpiresAt = expiresAt;

    /// <summary>Gets the extended expiry instant.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
