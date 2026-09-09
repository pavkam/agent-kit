// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures immutable evidence that a resolved call was accepted for invocation.</summary>
/// <remarks>The grant identifier is historical correlation only; this value neither contains nor reactivates a grant.</remarks>
public sealed record ToolCallAcceptanceEvidence
{
    /// <summary>Initializes accepted-invocation evidence.</summary>
    /// <param name="invocationGrantId">The nondefault historical invocation-grant identity.</param>
    /// <param name="validatedArgumentsFingerprint">The fingerprint of the accepted canonical arguments.</param>
    /// <param name="acceptedAt">The captured acceptance timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="invocationGrantId"/> or <paramref name="validatedArgumentsFingerprint"/> is default.</exception>
    public ToolCallAcceptanceEvidence(GrantId invocationGrantId, InputFingerprint validatedArgumentsFingerprint, DateTimeOffset acceptedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(invocationGrantId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(validatedArgumentsFingerprint, default);
        InvocationGrantId = invocationGrantId;
        ValidatedArgumentsFingerprint = validatedArgumentsFingerprint;
        AcceptedAt = acceptedAt;
    }
    /// <summary>Gets the historical invocation-grant identity.</summary>
    /// <value>A nondefault correlation identity.</value>
    public GrantId InvocationGrantId { get; }

    /// <summary>Gets the fingerprint of arguments accepted for invocation.</summary>
    /// <value>Validation/acceptance evidence.</value>
    public InputFingerprint ValidatedArgumentsFingerprint { get; }

    /// <summary>Gets the acceptance timestamp.</summary>
    /// <value>The recorded UTC-capable timestamp fact.</value>
    public DateTimeOffset AcceptedAt { get; }
}
