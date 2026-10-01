// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>Reports that provider egress did not produce a response, with the stable provider failure it maps to.</summary>
/// <remarks>
/// A refusal is fail-closed: missing authority, a denial at any boundary, unavailable grant enforcement, unavailable
/// required audit, an expired deadline, cancellation, and every transport failure all refuse. The failure carries only
/// a fixed safe message; request bodies, credentials, headers, and destination paths never appear in it.
/// </remarks>
public sealed record ProviderEgressRefused: ProviderEgressResult
{
    /// <summary>Initializes a new instance of the <see cref="ProviderEgressRefused"/> record.</summary>
    /// <param name="failure">The normalized failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ProviderEgressRefused(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalized failure.</summary>
    /// <value>
    /// A failure of kind <see cref="ProviderFailureKind.Authorization"/> for every authority, grant, audit, and
    /// policy refusal; <see cref="ProviderFailureKind.Cancellation"/> when the caller cancelled;
    /// <see cref="ProviderFailureKind.Timeout"/> when the deadline elapsed; and
    /// <see cref="ProviderFailureKind.Unavailable"/>, <see cref="ProviderFailureKind.ProtocolViolation"/>, or
    /// <see cref="ProviderFailureKind.InvalidRequest"/> for transport outcomes.
    /// </value>
    public ProviderFailure Failure { get; init; }
}
