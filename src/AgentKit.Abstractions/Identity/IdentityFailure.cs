// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes a typed identity rejection using bounded, credential-free diagnostic text.</summary>
public sealed record IdentityFailure
{
    /// <summary>Initializes a safe identity failure.</summary>
    /// <param name="kind">The stable failure classification.</param>
    /// <param name="safeMessage">A non-blank message safe to expose to the caller and diagnostics.</param>
    /// <param name="issuer">The issuer involved when one was established.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank or <paramref name="issuer"/> contains a default identifier.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public IdentityFailure(IdentityFailureKind kind, string safeMessage, IdentityIssuerId? issuer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) kind, nameof(kind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) kind, (int) IdentityFailureKind.Unavailable, nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        if (issuer is { } issuerValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuerValue.Value, nameof(issuer));
        }
        Kind = kind;
        SafeMessage = safeMessage;
        Issuer = issuer;
    }

    /// <summary>Gets the stable failure classification.</summary>
    public IdentityFailureKind Kind { get; }
    /// <summary>Gets the bounded credential-free diagnostic.</summary>
    public string SafeMessage { get; }
    /// <summary>Gets the relevant issuer when established.</summary>
    public IdentityIssuerId? Issuer { get; }
}
