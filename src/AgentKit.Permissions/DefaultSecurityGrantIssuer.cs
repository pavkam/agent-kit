// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Mints bounded grants from allow or approved scope evidence using host-configured ceilings.</summary>
/// <remarks>This issuer assembles grant evidence only; the authority registers the returned grant after audit succeeds.</remarks>
public sealed class DefaultSecurityGrantIssuer: ISecurityGrantIssuer
{
    private readonly IIdentifierGenerator<GrantId> _grantIds;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maximumGrantLifetime;
    private readonly int _maximumGrantUses;

    /// <summary>Initializes the first-party grant issuer.</summary>
    /// <param name="grantIds">The grant identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated permission configuration.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultSecurityGrantIssuer(
        IIdentifierGenerator<GrantId> grantIds,
        TimeProvider timeProvider,
        IOptions<AgentPermissionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(grantIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        var optionValues = options.Value;
        ArgumentNullException.ThrowIfNull(optionValues);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(optionValues.MaximumGrantLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.MaximumGrantUses);
        _grantIds = grantIds;
        _timeProvider = timeProvider;
        _maximumGrantLifetime = optionValues.MaximumGrantLifetime;
        _maximumGrantUses = optionValues.MaximumGrantUses;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityGrant> IssueAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion,
        ApprovalScopeBinding? approvedBinding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (approvedBinding is not null)
        {
            ArgumentException.ThrowIfNotEqual(approvedBinding.Request.Id, request.Id, nameof(approvedBinding));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var maximumExpiry = now + _maximumGrantLifetime;
        if (approvedBinding is not null && approvedBinding.ExpiresAt < maximumExpiry)
        {
            maximumExpiry = approvedBinding.ExpiresAt;
        }

        var grantId = _grantIds.Create();
        var expiresAt = request.Deadline < maximumExpiry ? request.Deadline : maximumExpiry;
        var allowedUses = Math.Min(request.RequestedUses, approvedBinding?.AllowedUses ?? _maximumGrantUses);
        var grant = request.Authorization is { } captured
            ? new SecurityGrant(
                grantId,
                request.Id,
                request.Scope,
                request.Identity,
                captured,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                policyVersion,
                revocationVersion,
                now,
                expiresAt,
                allowedUses)
            : new SecurityGrant(
                grantId,
                request.Id,
                request.Scope,
                request.Identity,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                policyVersion,
                revocationVersion,
                now,
                expiresAt,
                allowedUses);
        return new ValueTask<SecurityGrant>(grant);
    }
}
