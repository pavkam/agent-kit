// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Resolves one trusted assertion through its keyed issuer, ordered policies, and validation policy.</summary>
internal sealed class ExecutionIdentityResolver: IExecutionIdentityResolver
{
    private readonly IIdentityIssuerCatalog _issuers;
    private readonly OrderedIdentityNormalizationPolicies _normalization;
    private readonly IIdentityValidationPolicy _validation;
    private readonly ILogger<ExecutionIdentityResolver> _logger;

    /// <summary>Initializes a scoped resolver over the captured issuer and policy catalogs.</summary>
    /// <param name="issuers">The immutable trusted issuer catalog.</param>
    /// <param name="normalization">The deterministically ordered normalization policies.</param>
    /// <param name="validation">The terminal identity validation policy.</param>
    /// <param name="logger">The structured diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public ExecutionIdentityResolver(IIdentityIssuerCatalog issuers, OrderedIdentityNormalizationPolicies normalization, IIdentityValidationPolicy validation, ILogger<ExecutionIdentityResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(issuers);
        ArgumentNullException.ThrowIfNull(normalization);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(logger);
        _issuers = issuers;
        _normalization = normalization;
        _validation = validation;
        _logger = logger;
    }
    /// <inheritdoc/>
    public async ValueTask<IdentityResolutionResult> ResolveAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        var activity = IdentityObservability.Start(AgentKitActivityNames.IdentityResolve);
        try { _ = activity?.SetTag(AgentKitTagNames.IdentityIssuer, assertion.Issuer.ToString()); } catch (Exception) { }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var issuer = _issuers.Find(assertion.Issuer);
            if (issuer is null)
            {
                return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.UnknownIssuer, "The identity issuer is not configured.", assertion.Issuer)), activity, _logger);
            }

            var descriptor = issuer.Descriptor;
            if (descriptor.Id != assertion.Issuer)
            {
                return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Malformed, "The identity issuer descriptor does not match the assertion issuer.", assertion.Issuer)), activity, _logger);
            }

            var issuerResult = await issuer.NormalizeAsync(assertion, cancellationToken).ConfigureAwait(false);
            if (issuerResult is IdentityNormalizationRejected issuerRejection)
            {
                return Complete(new IdentityRejected(issuerRejection.Failure), activity, _logger);
            }

            if (issuerResult is not IdentityNormalized issuerNormalized)
            {
                return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "Identity issuer returned an unsupported normalization result.", assertion.Issuer)), activity, _logger);
            }

            if (!TryCaptureCandidate(issuerNormalized.Identity, assertion.Issuer, descriptor.Version, out var candidate))
            {
                return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Malformed, "Mapped authentication evidence has an unexpected issuer.", assertion.Issuer)), activity, _logger);
            }
            var capturedEvidence = candidate.Evidence;
            var capturedVersion = candidate.Version;
            foreach (var policy in _normalization.Policies)
            {
                var previousCandidate = candidate;
                var result = await policy.NormalizeAsync(new IdentityNormalizationRequest(assertion, candidate), cancellationToken).ConfigureAwait(false);
                if (result is IdentityNormalizationRejected rejection)
                {
                    return Complete(new IdentityRejected(rejection.Failure), activity, _logger);
                }

                if (result is not IdentityNormalized normalized ||
                    normalized.Identity.TenantId != previousCandidate.TenantId ||
                    normalized.Identity.PrincipalId != previousCandidate.PrincipalId ||
                    normalized.Identity.SubjectKind != previousCandidate.SubjectKind ||
                    normalized.Identity.Evidence != capturedEvidence ||
                    normalized.Identity.Version != capturedVersion ||
                    !normalized.Identity.DelegationChain.AsSpan().SequenceEqual(previousCandidate.DelegationChain.AsSpan()) ||
                    normalized.Identity.Assurance > previousCandidate.Assurance ||
                    normalized.Identity.Claims.Any(claim => !previousCandidate.Claims.Contains(claim)) ||
                    !TryCaptureCandidate(normalized.Identity, assertion.Issuer, descriptor.Version, out candidate))
                {
                    return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "A normalization policy returned an unsupported result.", assertion.Issuer)), activity, _logger);
                }
            }

            var validationResult = await _validation.ValidateAsync(candidate, cancellationToken).ConfigureAwait(false);
            return validationResult switch
            {
                IdentityValidationRejected rejection => Complete(new IdentityRejected(rejection.Failure), activity, _logger),
                IdentityValidationPassed => Complete(new IdentityResolved(candidate), activity, _logger),
                _ => Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "Identity validation returned an unsupported result.", assertion.Issuer)), activity, _logger)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            IdentityObservability.CancelResolution(activity, _logger);
            throw;
        }
        catch (Exception exception)
        {
            try { IdentityLog.ResolveFailed(_logger, exception.GetType().Name); } catch (Exception) { }
            return Complete(new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "Identity resolution is unavailable.", assertion.Issuer)), activity, _logger);
        }
    }

    private static IdentityResolutionResult Complete(IdentityResolutionResult result, Activity? activity, ILogger logger) => IdentityObservability.CompleteResolution(result, activity, logger);

    private static bool TryCaptureCandidate(ExecutionIdentity candidate, IdentityIssuerId issuerId, IdentityVersion version, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ExecutionIdentity? captured)
    {
        if (candidate.Evidence is not { } evidence || evidence.Issuer != issuerId)
        {
            captured = null;
            return false;
        }

        captured = new ExecutionIdentity(candidate.TenantId, candidate.PrincipalId, candidate.SubjectKind, evidence, candidate.Claims, candidate.DelegationChain, candidate.Assurance, version);
        return true;
    }
}
