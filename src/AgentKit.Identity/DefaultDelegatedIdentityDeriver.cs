// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Derives a same-tenant, same-principal identity that only retains parent claims and assurance.</summary>
internal sealed class DefaultDelegatedIdentityDeriver: IDelegatedIdentityDeriver
{
    private readonly TimeProvider _timeProvider;
    private readonly AgentIdentityOptionsSnapshot _options;
    private readonly IIdentityValidationPolicy _validation;
    private readonly ILogger<DefaultDelegatedIdentityDeriver> _logger;

    /// <summary>Initializes the default narrowing derivation policy.</summary>
    /// <param name="timeProvider">The clock used to timestamp a retained delegation link.</param>
    /// <param name="options">The validated immutable identity options.</param>
    /// <param name="validation">The policy that revalidates parent evidence before derivation.</param>
    /// <param name="logger">The structured diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public DefaultDelegatedIdentityDeriver(TimeProvider timeProvider, AgentIdentityOptionsSnapshot options, IIdentityValidationPolicy validation, ILogger<DefaultDelegatedIdentityDeriver> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _options = options;
        _validation = validation;
        _logger = logger;
    }
    /// <inheritdoc/>
    public async ValueTask<IdentityResolutionResult> DeriveAsync(DelegatedIdentityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var activity = IdentityObservability.Start(AgentKitActivityNames.IdentityDerive);
        try
        {
            _ = activity?.SetTag(AgentKitTagNames.TenantId, request.Parent.TenantId.ToString());
            _ = activity?.SetTag(AgentKitTagNames.PrincipalId, request.Parent.PrincipalId.ToString());
        }
        catch (Exception) { }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IdentityResolutionResult result;
            if (request.Parent.Evidence is not { } evidence)
            {
                result = new IdentityRejected(new IdentityFailure(IdentityFailureKind.Malformed, "Authentication evidence is required."));
            }
            else if (await _validation.ValidateAsync(request.Parent, cancellationToken).ConfigureAwait(false) is var validationResult && validationResult is not IdentityValidationPassed)
            {
                result = validationResult is IdentityValidationRejected validationRejection
                    ? new IdentityRejected(validationRejection.Failure)
                    : new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "Identity validation returned an unsupported result.", evidence.Issuer));
            }
            else if (request.Parent.DelegationChain.Length >= _options.MaximumDelegationDepth
                || request.Parent.DelegationChain.Select(static link => link.Id).Distinct().Count() != request.Parent.DelegationChain.Length
                || request.Parent.DelegationChain.Any(link => link.Id == request.DelegationId || link.TenantId != request.Parent.TenantId)
                || RetainedHistoryBroadens(request.Parent))
            {
                result = new IdentityRejected(new IdentityFailure(IdentityFailureKind.DelegationWouldBroaden, "The delegation chain exceeds its permitted depth or contains a cycle.", evidence.Issuer));
            }
            else if (request.MaximumAssurance > request.Parent.Assurance || request.Claims.Any(claim => !request.Parent.Claims.Contains(claim)))
            {
                result = new IdentityRejected(new IdentityFailure(IdentityFailureKind.DelegationWouldBroaden, "Delegation may only retain a subset of parent claims and assurance.", evidence.Issuer));
            }
            else
            {
                var child = new ExecutionIdentity(
                    request.Parent.TenantId,
                    request.Parent.PrincipalId,
                    request.Parent.SubjectKind,
                    evidence,
                    request.Claims,
                    request.Parent.DelegationChain.Add(new DelegationIdentityLink(request.DelegationId, request.Parent.TenantId, request.Parent.PrincipalId, evidence.Issuer, evidence.Id, request.Parent.Version, _timeProvider.GetUtcNow(), request.Claims, request.MaximumAssurance)),
                    request.MaximumAssurance,
                    request.Parent.Version);
                result = new IdentityResolved(child);
            }

            return IdentityObservability.CompleteDerivation(result, activity, _logger);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            IdentityObservability.CancelDerivation(activity, _logger);
            throw;
        }
        catch (Exception exception)
        {
            try { IdentityLog.DeriveFailed(_logger, exception.GetType().Name); } catch (Exception) { }
            return IdentityObservability.CompleteDerivation(
                new IdentityRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "Identity derivation is unavailable.", request.Parent.Evidence.Issuer)),
                activity,
                _logger);
        }
    }

    private static bool RetainedHistoryBroadens(ExecutionIdentity parent)
    {
        Debug.Assert(parent is not null, "The externally validated parent identity must be available.");
        for (var index = 1; index < parent.DelegationChain.Length; index++)
        {
            var previous = parent.DelegationChain[index - 1];
            var current = parent.DelegationChain[index];
            if (current.Assurance > previous.Assurance || current.Claims.Any(claim => !previous.Claims.Contains(claim)))
            {
                return true;
            }
        }

        return parent.DelegationChain is [.., var latest]
            && (parent.Assurance > latest.Assurance || parent.Claims.Any(claim => !latest.Claims.Contains(claim)));
    }
}
