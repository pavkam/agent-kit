// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Portable JSON mirror of <see cref="ApprovalScopeBinding"/>, the exact grant-relevant scope a human approved or denied.</summary>
/// <remarks>
/// <para>
/// The binding is the authority-bearing part of an approval: it is what a later grant is issued against, so it is persisted
/// in full and revalidated on read through the real domain constructor rather than assembled with an object initializer.
/// That constructor re-enforces that the approved use count never exceeds the requested count and that the approved expiry
/// never outlives the request deadline, so a hand-edited or corrupted log cannot widen authority beyond what was asked for.
/// </para>
/// <para>
/// <see cref="PolicyVersion"/> and <see cref="RevocationVersion"/> are unwrapped to their underlying primitives and rebuilt
/// through their validating constructors. Equality is the compiler-generated record equality, which is already exact here
/// because <see cref="JsonSecurityRequest"/> defines ordered content equality for its resource array.
/// </para>
/// </remarks>
/// <param name="Request">The non-null complete normalized security request whose immutable fields were captured.</param>
/// <param name="PolicyVersion">The positive evaluated policy version.</param>
/// <param name="RevocationVersion">The positive revocation epoch captured at approval time.</param>
/// <param name="NotBefore">The first permitted grant instant, which must be strictly earlier than <paramref name="ExpiresAt"/>.</param>
/// <param name="ExpiresAt">The exclusive approval and grant expiry, which must not exceed the request deadline.</param>
/// <param name="AllowedUses">The positive approved use bound, which must not exceed the request's requested uses.</param>
public sealed record JsonApprovalScopeBinding(
    JsonSecurityRequest Request,
    long PolicyVersion,
    long RevocationVersion,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int AllowedUses)
{
    /// <summary>Projects one domain approval binding into its portable JSON representation.</summary>
    /// <param name="value">The non-null binding to project.</param>
    /// <returns>A document carrying the projected request document and every unwrapped version and bound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonApprovalScopeBinding FromDomain(ApprovalScopeBinding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonApprovalScopeBinding(
            JsonSecurityRequest.FromDomain(value.Request),
            value.PolicyVersion.Value,
            value.RevocationVersion.Value,
            value.NotBefore,
            value.ExpiresAt,
            value.AllowedUses);
    }

    /// <summary>Reconstructs the exact domain binding this document was projected from.</summary>
    /// <returns>A binding equal to the projected original, including the ordered canonical resources of its request.</returns>
    /// <remarks>
    /// The request is rebuilt first so the approval constructor can compare the approved use count and expiry against the
    /// request that justified them. Persisted evidence is therefore revalidated exactly as freshly captured evidence is.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Request"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">The persisted request is malformed; see <see cref="JsonSecurityRequest.ToDomain"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PolicyVersion"/> or <see cref="RevocationVersion"/> is not positive, <see cref="AllowedUses"/> is not positive or exceeds the request, <see cref="ExpiresAt"/> is not later than <see cref="NotBefore"/>, or <see cref="ExpiresAt"/> exceeds the request deadline.</exception>
    public ApprovalScopeBinding ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Request);
        return new ApprovalScopeBinding(
            Request.ToDomain(),
            new SecurityPolicyVersion(PolicyVersion),
            new SecurityRevocationVersion(RevocationVersion),
            NotBefore,
            ExpiresAt,
            AllowedUses);
    }
}
