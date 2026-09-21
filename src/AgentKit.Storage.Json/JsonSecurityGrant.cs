// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityGrant"/>, the immutable evidence of narrowly bounded authority whose remaining uses live in a grant store.</summary>
/// <remarks>
/// <para>
/// A grant is the most consequential value this package persists, so reconstruction deliberately routes through the real
/// domain constructors rather than object initializers. That matters for two reasons. First, the domain type cross-checks
/// <see cref="NotBefore"/> against <see cref="ExpiresAt"/> inside each property's init accessor, and only the constructor
/// assigns them in the order that lets both checks see real values; a <c>with</c> expression or object initializer would
/// compare against a default instant. Second, the constructors are where the bounded-authority invariants live, so untrusted
/// persisted bytes are revalidated exactly as freshly issued evidence is.
/// </para>
/// <para>
/// <see cref="Authorization"/> is optional and its absence is meaningful: a grant issued through the unpinned path captured no
/// authorization context, and inventing one on read would fabricate snapshot-bound authority the issuing authority never
/// granted. <see cref="ToDomain"/> therefore selects between the two domain constructors on exactly that distinction, so a
/// grant persisted without captured authorization never gains one. When authorization is present, the domain constructor also
/// requires its scope, identity, and policy-snapshot version to match the grant's own, which this mirror satisfies because
/// both sides are projected from the same original values.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty grant identity.</param>
/// <param name="RequestId">The raw value of the non-empty security request that caused the grant.</param>
/// <param name="Scope">The non-null exact execution scope the grant is bound to.</param>
/// <param name="Identity">The non-null authenticated execution identity the grant was issued for.</param>
/// <param name="Authorization">The complete captured authorization evidence evaluated by the authority, or <see langword="null"/> for a grant issued through the unpinned path.</param>
/// <param name="Audience">The non-blank canonical component identifier of the only component permitted to consume the grant.</param>
/// <param name="Kind">The protected operation kind.</param>
/// <param name="Effect">The exact material effect.</param>
/// <param name="Resources">The ordered, non-default, non-empty canonical resources the authority is bound to.</param>
/// <param name="InputFingerprint">The non-blank exact normalized input fingerprint.</param>
/// <param name="PolicyVersion">The positive policy version that issued the grant.</param>
/// <param name="RevocationVersion">The positive revocation epoch captured at issue time.</param>
/// <param name="NotBefore">The earliest valid instant, which must be strictly earlier than <paramref name="ExpiresAt"/>.</param>
/// <param name="ExpiresAt">The exclusive expiry instant.</param>
/// <param name="AllowedUses">The positive maximum number of successful consumptions.</param>
/// <param name="ApprovalResponseId">The optional terminal approval response that bound this grant, or null when none was recorded.</param>
public sealed record JsonSecurityGrant(
    Guid Id,
    Guid RequestId,
    JsonSecurityAuthorizationScope Scope,
    JsonExecutionIdentity Identity,
    JsonSecurityAuthorizationContext? Authorization,
    string Audience,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<JsonProtectedResource> Resources,
    string InputFingerprint,
    long PolicyVersion,
    long RevocationVersion,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int AllowedUses,
    Guid? ApprovalResponseId = null)
{
    /// <summary>Projects one domain grant into its portable JSON representation.</summary>
    /// <param name="value">The non-null grant to project.</param>
    /// <returns>A document carrying every unwrapped identity and bound, a never-default ordered resource array, and a null <see cref="Authorization"/> exactly when the grant captured none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityGrant FromDomain(SecurityGrant value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ImmutableArray<JsonProtectedResource> resources =
            value.Resources.IsDefault ? [] : [.. value.Resources.Select(JsonProtectedResource.FromDomain)];
        return new JsonSecurityGrant(
            value.Id.Value,
            value.RequestId.Value,
            JsonSecurityAuthorizationScope.FromDomain(value.Scope),
            JsonExecutionIdentity.FromDomain(value.Identity),
            value.Authorization is { } authorization
                ? JsonSecurityAuthorizationContext.FromDomain(authorization)
                : null,
            value.Audience.Value,
            value.Kind,
            value.Effect,
            resources,
            value.InputFingerprint.Value,
            value.PolicyVersion.Value,
            value.RevocationVersion.Value,
            value.NotBefore,
            value.ExpiresAt,
            value.AllowedUses,
            value.Approval?.Value);
    }

    /// <summary>Reconstructs the exact domain grant this document was projected from.</summary>
    /// <returns>A grant equal to the projected original, retaining captured authorization only when the document carried it.</returns>
    /// <remarks>
    /// The authorization-bearing constructor overload is selected only when <see cref="Authorization"/> is present, so an
    /// unpinned grant is never silently upgraded to a snapshot-bound one. Both overloads assign the validity window in the
    /// order the domain type requires, which is why this method never uses a <c>with</c> expression or object initializer.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Scope"/> or <see cref="Identity"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="Audience"/> or <see cref="InputFingerprint"/> is blank, <see cref="Resources"/> is empty or contains a null element, or a present <see cref="Authorization"/> disagrees with the grant's scope, identity, or policy version.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty, <see cref="Kind"/> or <see cref="Effect"/> is undefined, <see cref="PolicyVersion"/>, <see cref="RevocationVersion"/>, or <see cref="AllowedUses"/> is not positive, or <see cref="ExpiresAt"/> is not later than <see cref="NotBefore"/>.</exception>
    public SecurityGrant ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Identity);
        var persisted = Resources.IsDefault ? [] : Resources;
        ArgumentException.ThrowIfContainsNull(persisted, nameof(Resources));
        var id = new GrantId(Id);
        var requestId = new SecurityRequestId(RequestId);
        var scope = Scope.ToDomain();
        var identity = Identity.ToDomain();
        var audience = new ComponentId(Audience);
        ImmutableArray<ProtectedResource> resources =
            [.. persisted.Select(static resource => resource.ToDomain())];
        var inputFingerprint = new InputFingerprint(InputFingerprint);
        var policyVersion = new SecurityPolicyVersion(PolicyVersion);
        var revocationVersion = new SecurityRevocationVersion(RevocationVersion);
        var grant = Authorization is { } authorization
            ? new SecurityGrant(
                id,
                requestId,
                scope,
                identity,
                authorization.ToDomain(),
                audience,
                Kind,
                Effect,
                resources,
                inputFingerprint,
                policyVersion,
                revocationVersion,
                NotBefore,
                ExpiresAt,
                AllowedUses)
            : new SecurityGrant(
                id,
                requestId,
                scope,
                identity,
                audience,
                Kind,
                Effect,
                resources,
                inputFingerprint,
                policyVersion,
                revocationVersion,
                NotBefore,
                ExpiresAt,
                AllowedUses);
        return ApprovalResponseId is Guid approvalId
            ? grant with { Approval = new ApprovalResponseId(approvalId) }
            : grant;
    }

    /// <summary>Compares grants by ordered resource contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate grant to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and nested document is equal and both resource sequences are element-wise equal in the same order.</returns>
    /// <remarks>
    /// Compiler-generated record equality would compare <see cref="Resources"/> by backing-array identity, so two documents
    /// decoded from byte-identical JSON would compare unequal. This override restores the same ordered content equality the
    /// mirrored <see cref="SecurityGrant"/> guarantees.
    /// </remarks>
    public bool Equals(JsonSecurityGrant? other) =>
        other is not null
        && Id == other.Id
        && RequestId == other.RequestId
        && Scope == other.Scope
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && Resources.AsSpan().SequenceEqual(other.Resources.AsSpan())
        && InputFingerprint == other.InputFingerprint
        && PolicyVersion == other.PolicyVersion
        && RevocationVersion == other.RevocationVersion
        && NotBefore == other.NotBefore
        && ExpiresAt == other.ExpiresAt
        && AllowedUses == other.AllowedUses
        && ApprovalResponseId == other.ApprovalResponseId;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonSecurityGrant?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, nested document, and each ordered resource.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(RequestId);
        hash.Add(Scope);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Audience);
        hash.Add(Kind);
        hash.Add(Effect);
        foreach (var resource in Resources.AsSpan())
        {
            hash.Add(resource);
        }

        hash.Add(InputFingerprint);
        hash.Add(PolicyVersion);
        hash.Add(RevocationVersion);
        hash.Add(NotBefore);
        hash.Add(ExpiresAt);
        hash.Add(AllowedUses);
        hash.Add(ApprovalResponseId);
        return hash.ToHashCode();
    }
}
