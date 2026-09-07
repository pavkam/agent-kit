// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Validates evidence timing, anonymous use, and issuer-provided evidence revocation.</summary>
internal sealed class DefaultIdentityValidationPolicy: IIdentityValidationPolicy
{
    private readonly IIdentityIssuerCatalog _issuers;
    private readonly TimeProvider _timeProvider;
    private readonly AgentIdentityOptionsSnapshot _options;

    /// <summary>Initializes the evidence lifetime and issuer validation policy.</summary>
    /// <param name="issuers">The immutable trusted issuer catalog.</param>
    /// <param name="timeProvider">The clock used for evidence lifetime decisions.</param>
    /// <param name="options">The validated immutable identity options.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public DefaultIdentityValidationPolicy(IIdentityIssuerCatalog issuers, TimeProvider timeProvider, AgentIdentityOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(issuers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _issuers = issuers;
        _timeProvider = timeProvider;
        _options = options;
    }
    /// <inheritdoc/>
    public async ValueTask<IdentityValidationResult> ValidateAsync(ExecutionIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        var evidence = identity.Evidence;

        if (identity.SubjectKind == ExecutionSubjectKind.Anonymous && !_options.AllowAnonymous)
        {
            return Reject(IdentityFailureKind.Unsupported, "Anonymous execution is disabled.", evidence.Issuer);
        }

        var now = _timeProvider.GetUtcNow();
        if (evidence.AuthenticatedAt > now && evidence.AuthenticatedAt - now > _options.MaximumClockSkew)
        {
            return Reject(IdentityFailureKind.Malformed, "Authentication evidence is from the future.", evidence.Issuer);
        }

        if (evidence.ExpiresAt is { } expiry && expiry <= now && now - expiry >= _options.MaximumClockSkew)
        {
            return Reject(IdentityFailureKind.Expired, "Authentication evidence has expired.", evidence.Issuer);
        }

        var lifetimeExceeded = (evidence.AuthenticatedAt <= now && now - evidence.AuthenticatedAt >= _options.MaximumEvidenceLifetime) ||
                              (evidence.ExpiresAt is { } boundedExpiry && boundedExpiry - evidence.AuthenticatedAt > _options.MaximumEvidenceLifetime);
        return lifetimeExceeded
            ? Reject(IdentityFailureKind.Expired, "Authentication evidence lifetime exceeds the configured maximum.", evidence.Issuer)
            : _issuers.Find(evidence.Issuer) is { } issuer
                ? await issuer.ValidateEvidenceAsync(evidence, now, cancellationToken).ConfigureAwait(false)
                : Reject(IdentityFailureKind.UnknownIssuer, "The identity issuer is not configured.", evidence.Issuer);
    }

    private static IdentityValidationRejected Reject(IdentityFailureKind kind, string message, IdentityIssuerId issuer) => new(new IdentityFailure(kind, message, issuer));
}
