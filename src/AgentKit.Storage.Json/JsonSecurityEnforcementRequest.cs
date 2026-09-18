// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityEnforcementRequest"/>, the fresh concrete effect evidence checked immediately before a grant use is consumed.</summary>
/// <remarks>
/// <para>
/// Enforcement evidence is what proves the effect actually about to happen matches the effect that was authorized, so it is
/// persisted verbatim as part of a consumed-intent receipt. Every member is recomputed by the effecting boundary rather than
/// copied from the grant, and reconstruction preserves that distinction: the mirror never borrows a value from an enclosing
/// document.
/// </para>
/// <para>
/// <see cref="Authorization"/> is optional and its absence is meaningful, because a legacy enforcement path presents no
/// captured context. <see cref="ToDomain"/> selects between the two domain constructors on exactly that distinction, so
/// enforcement evidence persisted without captured authorization never gains one. When authorization is present, the domain
/// constructor requires its scope and identity to match the enforcement request's own, which holds here because both sides are
/// projected from the same original values.
/// </para>
/// </remarks>
/// <param name="Scope">The non-null actual execution scope of the concrete effect.</param>
/// <param name="Identity">The non-null actual execution identity performing the concrete effect.</param>
/// <param name="Authorization">The complete captured authorization evidence presented by the protected operation, or <see langword="null"/> for a legacy enforcement path.</param>
/// <param name="Audience">The non-blank canonical component identifier of the effecting component.</param>
/// <param name="Kind">The concrete protected operation kind.</param>
/// <param name="Effect">The concrete material effect.</param>
/// <param name="Resources">The ordered, non-default, non-empty concrete canonical resources.</param>
/// <param name="InputFingerprint">The non-blank concrete normalized input fingerprint.</param>
/// <param name="RevocationVersion">The positive current revocation epoch observed at enforcement time.</param>
public sealed record JsonSecurityEnforcementRequest(
    JsonSecurityAuthorizationScope Scope,
    JsonExecutionIdentity Identity,
    JsonSecurityAuthorizationContext? Authorization,
    string Audience,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<JsonProtectedResource> Resources,
    string InputFingerprint,
    long RevocationVersion)
{
    /// <summary>Projects one domain enforcement request into its portable JSON representation.</summary>
    /// <param name="value">The non-null enforcement evidence to project.</param>
    /// <returns>A document carrying the projected scope and identity, a never-default ordered resource array, and a null <see cref="Authorization"/> exactly when the request captured none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityEnforcementRequest FromDomain(SecurityEnforcementRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ImmutableArray<JsonProtectedResource> resources =
            value.Resources.IsDefault ? [] : [.. value.Resources.Select(JsonProtectedResource.FromDomain)];
        return new JsonSecurityEnforcementRequest(
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
            value.RevocationVersion.Value);
    }

    /// <summary>Reconstructs the exact domain enforcement request this document was projected from.</summary>
    /// <returns>Enforcement evidence equal to the projected original, retaining captured authorization only when the document carried it.</returns>
    /// <remarks>
    /// The authorization-bearing constructor overload is selected only when <see cref="Authorization"/> is present, so legacy
    /// enforcement evidence is never silently upgraded to snapshot-bound evidence. The audience, input fingerprint, and
    /// revocation epoch are rebuilt through their own value-type constructors, which validate more strictly than the
    /// enforcement constructor itself; persisted evidence that was never built through those constructors is therefore
    /// rejected rather than reconstructed as a default-valued binding an effecting boundary would compare against.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Scope"/> or <see cref="Identity"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="Audience"/> or <see cref="InputFingerprint"/> is blank, <see cref="Resources"/> is empty or contains a null element, or a present <see cref="Authorization"/> disagrees with the request's scope or identity.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Kind"/> or <see cref="Effect"/> is undefined, <see cref="RevocationVersion"/> is not positive, or a nested identity is empty.</exception>
    public SecurityEnforcementRequest ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Identity);
        var persisted = Resources.IsDefault ? [] : Resources;
        ArgumentException.ThrowIfContainsNull(persisted, nameof(Resources));
        var scope = Scope.ToDomain();
        var identity = Identity.ToDomain();
        var audience = new ComponentId(Audience);
        ImmutableArray<ProtectedResource> resources =
            [.. persisted.Select(static resource => resource.ToDomain())];
        var inputFingerprint = new InputFingerprint(InputFingerprint);
        var revocationVersion = new SecurityRevocationVersion(RevocationVersion);
        return Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                scope,
                identity,
                authorization.ToDomain(),
                audience,
                Kind,
                Effect,
                resources,
                inputFingerprint,
                revocationVersion)
            : new SecurityEnforcementRequest(
                scope,
                identity,
                audience,
                Kind,
                Effect,
                resources,
                inputFingerprint,
                revocationVersion);
    }

    /// <summary>Compares enforcement evidence by ordered resource contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate enforcement evidence to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and nested document is equal and both resource sequences are element-wise equal in the same order.</returns>
    /// <remarks>
    /// Compiler-generated record equality would compare <see cref="Resources"/> by backing-array identity, so two documents
    /// decoded from byte-identical JSON would compare unequal. This override restores the same ordered content equality the
    /// mirrored <see cref="SecurityEnforcementRequest"/> guarantees.
    /// </remarks>
    public bool Equals(JsonSecurityEnforcementRequest? other) =>
        other is not null
        && Scope == other.Scope
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && Resources.AsSpan().SequenceEqual(other.Resources.AsSpan())
        && InputFingerprint == other.InputFingerprint
        && RevocationVersion == other.RevocationVersion;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonSecurityEnforcementRequest?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, nested document, and each ordered resource.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
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
        hash.Add(RevocationVersion);
        return hash.ToHashCode();
    }
}
