// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>Issues and consumes single-use goal-store grants with the exact-binding rule a real grant store enforces.</summary>
/// <remarks>A grant whose scope, identity, authorization, audience, kind, effect, resources, request digest, or revocation version differs from the enforcement a store recomputed is a mismatch, never a consumption, and each use is consumed once. Knobs let a test make consumption fail without weakening the rule for every other test.</remarks>
public sealed class TestGoalGrants: ISecurityGrantStore
{
    private readonly Dictionary<GrantId, int> _uses = [];
    private long _next;

    /// <summary>Gets or sets a status every consumption reports instead of validating, or null to validate normally.</summary>
    public GrantConsumptionStatus? ForcedStatus { get; set; }

    /// <summary>Gets or sets whether consumption throws as if the grant store were unavailable.</summary>
    public bool Fail { get; set; }

    /// <summary>Gets the number of grant uses consumed.</summary>
    public int ConsumedCount { get; private set; }

    /// <summary>Issues a single-use grant bound to one exact operation.</summary>
    /// <param name="audience">The store or dispatcher the grant names.</param>
    /// <param name="authorization">The captured authorization the operation runs under.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="resources">The protected resources.</param>
    /// <param name="fingerprint">The canonical fingerprint of the exact operation.</param>
    /// <returns>A grant the matching operation consumes exactly once.</returns>
    public SecurityGrant Issue(
        ComponentId audience,
        SecurityAuthorizationContext authorization,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var grant = new SecurityGrant(
            new GrantId(NextGuid()),
            new SecurityRequestId(NextGuid()),
            authorization.Scope,
            authorization.Identity,
            authorization,
            audience,
            kind,
            effect,
            resources,
            fingerprint,
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddYears(100),
            allowedUses: 1);
        _uses[grant.Id] = grant.AllowedUses;
        return grant;
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        _ = _uses.TryAdd(grant.Id, grant.AllowedUses);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail)
        {
            throw new InvalidOperationException("The test grant store is unavailable.");
        }

        if (ForcedStatus is { } forced)
        {
            return ValueTask.FromResult(Refused(forced, "The test grant store forced this status."));
        }

        var matches = grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Authorization == enforcement.Authorization
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint
            && grant.RevocationVersion == enforcement.RevocationVersion;
        if (!matches)
        {
            return ValueTask.FromResult(Refused(GrantConsumptionStatus.Mismatch, "The exact grant binding did not match."));
        }

        lock (_uses)
        {
            if (!_uses.TryGetValue(grant.Id, out var remaining))
            {
                return ValueTask.FromResult(Refused(GrantConsumptionStatus.Unknown, "The grant is not registered."));
            }

            if (remaining <= 0)
            {
                return ValueTask.FromResult(Refused(GrantConsumptionStatus.Exhausted, "The grant use was already consumed."));
            }

            _uses[grant.Id] = remaining - 1;
            ConsumedCount++;
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                remaining - 1,
                "The exact grant use was consumed.",
                new SecurityEnforcementIntentReceipt(
                    intent.Id,
                    grant.Id,
                    grant.RequestId,
                    enforcement,
                    intent.RequiredFence,
                    SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                    DateTimeOffset.UnixEpoch)));
        }
    }

    private static GrantConsumptionResult Refused(GrantConsumptionStatus status, string message) =>
        new(status, 0, message, null);

    /// <inheritdoc/>
    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _next);
        return new Guid((int) value, 0, 0, [0xf0, 0, 0, 0, 0, 0, 0, 1]);
    }
}
