// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Factories that build an <see cref="ExecutionIdentity"/> from the facts a trusted ingress already holds, so a
/// host does not assemble evidence, fingerprints, and empty claim arrays by hand.
/// </summary>
/// <remarks>
/// <para>
/// These factories do not authenticate anything. The caller is the trusted ingress: it states which issuer
/// authenticated the subject, by what method, and when, and the factory records those statements as
/// <see cref="AuthenticationEvidence"/> with a content-safe fingerprint derived from the issuer, subject, method,
/// and time. No credential, token, or secret is ever part of the input, so none can leak into the fingerprint.
/// </para>
/// <para>
/// Use <c>ForService</c> for a workload running under a platform-issued identity (a managed identity, a service
/// account, a Kubernetes token) and <c>ForHuman</c> for a person your authentication middleware has already
/// signed in. Hosts with several issuers, claim normalization, or delegation chains compose
/// <c>AddAgentIdentity</c> and resolve through <c>IExecutionIdentityResolver</c> instead.
/// </para>
/// </remarks>
public static class ExecutionIdentityExtensions
{
    /// <summary>Static factory members exposed on <see cref="ExecutionIdentity"/>.</summary>
    extension(ExecutionIdentity)
    {
        /// <summary>
        /// Creates the identity of a non-human workload that a platform issuer authenticated.
        /// </summary>
        /// <param name="tenantId">The tenant the workload acts within.</param>
        /// <param name="principalId">The stable principal the platform issued, such as a managed-identity object id or a service-account name.</param>
        /// <param name="issuer">The authority that authenticated the workload.</param>
        /// <param name="method">The content-safe authentication method label, such as <c>"managed-identity"</c> or <c>"workload-identity-federation"</c>.</param>
        /// <param name="authenticatedAt">When the issuer authenticated the workload.</param>
        /// <param name="expiresAt">When that authentication stops being valid, or <see langword="null"/> when the issuer does not bound it.</param>
        /// <param name="assurance">The assurance the issuer's method warrants; defaults to <see cref="IdentityAssuranceLevel.Strong"/>, which platform-issued credentials normally meet.</param>
        /// <param name="claims">Additional normalized claims the issuer vouched for, or empty.</param>
        /// <returns>An identity of kind <see cref="ExecutionSubjectKind.Service"/> with version 1 and no delegation chain.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="tenantId"/>, <paramref name="principalId"/>, <paramref name="issuer"/>, or <paramref name="method"/> is blank,
        /// or <paramref name="claims"/> contains a null element.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="expiresAt"/> is not after <paramref name="authenticatedAt"/>, or <paramref name="assurance"/> is undefined.
        /// </exception>
        public static ExecutionIdentity ForService(
            TenantId tenantId,
            PrincipalId principalId,
            IdentityIssuerId issuer,
            string method,
            DateTimeOffset authenticatedAt,
            DateTimeOffset? expiresAt = null,
            IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Strong,
            ImmutableArray<IdentityClaim> claims = default) =>
            Create(tenantId, principalId, ExecutionSubjectKind.Service, issuer, method, authenticatedAt, expiresAt, assurance, claims);

        /// <summary>
        /// Creates the identity of a person that an authentication system already signed in.
        /// </summary>
        /// <param name="tenantId">The tenant the person belongs to.</param>
        /// <param name="principalId">The stable subject identifier the issuer assigned, never a display name or email that can change.</param>
        /// <param name="issuer">The authority that authenticated the person, such as your OIDC provider.</param>
        /// <param name="method">The content-safe authentication method label, such as <c>"oidc"</c> or <c>"saml"</c>.</param>
        /// <param name="authenticatedAt">When the issuer authenticated the person.</param>
        /// <param name="expiresAt">When the sign-in stops being valid, or <see langword="null"/> when the issuer does not bound it.</param>
        /// <param name="assurance">The assurance the sign-in warrants; defaults to <see cref="IdentityAssuranceLevel.Basic"/>. Raise it only when the method justifies it.</param>
        /// <param name="claims">Additional normalized claims the issuer vouched for, or empty.</param>
        /// <returns>An identity of kind <see cref="ExecutionSubjectKind.Human"/> with version 1 and no delegation chain.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="tenantId"/>, <paramref name="principalId"/>, <paramref name="issuer"/>, or <paramref name="method"/> is blank,
        /// or <paramref name="claims"/> contains a null element.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="expiresAt"/> is not after <paramref name="authenticatedAt"/>, or <paramref name="assurance"/> is undefined.
        /// </exception>
        public static ExecutionIdentity ForHuman(
            TenantId tenantId,
            PrincipalId principalId,
            IdentityIssuerId issuer,
            string method,
            DateTimeOffset authenticatedAt,
            DateTimeOffset? expiresAt = null,
            IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic,
            ImmutableArray<IdentityClaim> claims = default) =>
            Create(tenantId, principalId, ExecutionSubjectKind.Human, issuer, method, authenticatedAt, expiresAt, assurance, claims);
    }

    /// <summary>
    /// Computes the content-safe fingerprint that identifies one authentication event without carrying any secret.
    /// </summary>
    /// <param name="issuer">The authenticating issuer.</param>
    /// <param name="subject">The authenticated principal.</param>
    /// <param name="method">The authentication method label.</param>
    /// <param name="authenticatedAt">The authentication instant.</param>
    /// <returns>An algorithm-qualified SHA-256 hash over the four inputs.</returns>
    /// <exception cref="ArgumentException">Any text input is blank.</exception>
    public static AuthenticationEvidenceFingerprint Fingerprint(
        IdentityIssuerId issuer,
        PrincipalId subject,
        string method,
        DateTimeOffset authenticatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(subject.Value, nameof(subject));
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var material = string.Join('\n', issuer.Value, subject.Value, method, authenticatedAt.ToUniversalTime().ToString("O"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return new AuthenticationEvidenceFingerprint(new ContentHash($"sha256:{Convert.ToHexStringLower(hash)}"));
    }

    /// <summary>Builds the identity both factories share, validating every documented constraint before construction.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="principalId">The stable principal.</param>
    /// <param name="subjectKind">The subject kind the public factory selected.</param>
    /// <param name="issuer">The authenticating issuer.</param>
    /// <param name="method">The authentication method label.</param>
    /// <param name="authenticatedAt">The authentication instant.</param>
    /// <param name="expiresAt">The optional authentication expiry.</param>
    /// <param name="assurance">The assurance level.</param>
    /// <param name="claims">The normalized claims, or a default array for none.</param>
    /// <returns>A version-1 identity without a delegation chain.</returns>
    /// <exception cref="ArgumentException">A text input is blank or <paramref name="claims"/> contains a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="assurance"/> is undefined or <paramref name="expiresAt"/> is not after <paramref name="authenticatedAt"/>.</exception>
    internal static ExecutionIdentity Create(
        TenantId tenantId,
        PrincipalId principalId,
        ExecutionSubjectKind subjectKind,
        IdentityIssuerId issuer,
        string method,
        DateTimeOffset authenticatedAt,
        DateTimeOffset? expiresAt,
        IdentityAssuranceLevel assurance,
        ImmutableArray<IdentityClaim> claims)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId.Value, nameof(principalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentOutOfRangeException.ThrowIfUndefined(assurance);

        var fingerprint = Fingerprint(issuer, principalId, method, authenticatedAt);
        var evidence = new AuthenticationEvidence(
            new AuthenticationEvidenceId($"{issuer.Value}:{principalId.Value}:{authenticatedAt.ToUniversalTime():O}"),
            issuer,
            method,
            authenticatedAt,
            expiresAt,
            fingerprint);

        return new ExecutionIdentity(
            tenantId,
            principalId,
            subjectKind,
            evidence,
            claims.IsDefault ? [] : claims,
            delegationChain: [],
            assurance,
            new IdentityVersion(1));
    }
}
