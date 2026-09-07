// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The lease expired or was taken over, so the caller no longer owns the
/// operation and must stop immediately.
/// </summary>
/// <remarks>
/// <para>
/// Losing a lease is not a transient error to retry through. The caller has
/// lost authority: it must stop performing effects, stop writing to the
/// journal, and abandon the operation to whichever worker now owns it. Its
/// subsequent writes would be rejected as
/// <see cref="DurableRecordFenced"/> anyway.
/// </para>
/// <para>
/// This outcome does not say whether an in-flight effect completed. Whatever
/// the losing worker already dispatched remains an unknown-outcome case for
/// the new owner to reconcile.
/// </para>
/// </remarks>
public sealed record LeaseLost: LeaseRenewalResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseLost"/> record.
    /// </summary>
    /// <param name="currentToken">
    /// The authoritative ownership generation now in force, when the lease
    /// manager could observe it, or <see langword="null"/> when ownership is
    /// simply no longer held.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="currentToken"/> is supplied as the default,
    /// unallocated token. Absence is expressed as <see langword="null"/>.
    /// </exception>
    public LeaseLost(FencingToken? currentToken = null)
    {
        if (currentToken is { } token)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(token, default, nameof(currentToken));
        }

        CurrentToken = currentToken;
    }

    /// <summary>
    /// Gets the authoritative ownership generation now in force, or
    /// <see langword="null"/> when it could not be observed.
    /// </summary>
    public FencingToken? CurrentToken { get; }
}
