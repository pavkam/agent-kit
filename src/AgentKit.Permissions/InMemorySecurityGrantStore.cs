// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Provides a process-local, concurrency-safe security grant store for standalone hosts and deterministic tests.</summary>
/// <remarks>
/// Grant evidence and remaining-use state are retained for the lifetime of this singleton service. The store is not durable
/// across process loss; hosts requiring recovery must replace it through dependency injection with a durable implementation
/// that preserves the same atomic consumption contract.
/// </remarks>
public sealed class InMemorySecurityGrantStore(TimeProvider timeProvider): ISecurityGrantStore
{
    private readonly ConcurrentDictionary<GrantId, GrantState> _grants = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateGrant(grant);
        cancellationToken.ThrowIfCancellationRequested();

        var state = new GrantState(grant);
        var registered = _grants.GetOrAdd(grant.Id, state);
        return GrantMatches(registered.Grant, grant)
            ? ValueTask.CompletedTask
            : throw new InvalidOperationException(
                $"Grant identifier '{grant.Id}' is already registered with different evidence.");
    }

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_grants.TryGetValue(grant.Id, out var state))
        {
            return ValueTask.FromResult(Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown."));
        }

        lock (state.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!GrantMatches(state.Grant, grant))
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Tampered, state.RemainingUses, "The security grant evidence does not match its authoritative record."));
            }

            if (state.Revoked || enforcement.RevocationVersion != grant.RevocationVersion)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Revoked, state.RemainingUses, "The security grant is revoked or stale."));
            }

            var now = _timeProvider.GetUtcNow();
            if (now < grant.NotBefore || now >= grant.ExpiresAt)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Expired, state.RemainingUses, "The security grant is outside its validity window."));
            }

            if (!EnforcementMatches(grant, enforcement))
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Mismatch, state.RemainingUses, "The concrete effect does not match the security grant."));
            }

            if (state.RemainingUses == 0)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Exhausted, 0, "The security grant has no remaining uses."));
            }

            state.RemainingUses--;
            return ValueTask.FromResult(Result(GrantConsumptionStatus.Consumed, state.RemainingUses, "The security grant was consumed."));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_grants.TryGetValue(grantId, out var state))
        {
            return ValueTask.FromResult(false);
        }

        lock (state.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Revoked = true;
            return ValueTask.FromResult(true);
        }
    }

    private static bool EnforcementMatches(SecurityGrant grant, SecurityEnforcementRequest enforcement) =>
        grant.Scope == enforcement.Scope
        && grant.Identity == enforcement.Identity
        && grant.Audience == enforcement.Audience
        && grant.Kind == enforcement.Kind
        && grant.Effect == enforcement.Effect
        && grant.InputFingerprint == enforcement.InputFingerprint
        && grant.Resources.SequenceEqual(enforcement.Resources);

    private static bool GrantMatches(SecurityGrant expected, SecurityGrant actual) =>
        expected.Id == actual.Id
        && expected.RequestId == actual.RequestId
        && expected.Scope == actual.Scope
        && expected.Identity == actual.Identity
        && expected.Audience == actual.Audience
        && expected.Kind == actual.Kind
        && expected.Effect == actual.Effect
        && expected.Resources.SequenceEqual(actual.Resources)
        && expected.InputFingerprint == actual.InputFingerprint
        && expected.PolicyVersion == actual.PolicyVersion
        && expected.RevocationVersion == actual.RevocationVersion
        && expected.NotBefore == actual.NotBefore
        && expected.ExpiresAt == actual.ExpiresAt
        && expected.AllowedUses == actual.AllowedUses;

    private static GrantConsumptionResult Result(GrantConsumptionStatus status, int remainingUses, string message) =>
        new(status, remainingUses, message);

    private static void ValidateGrant(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant.Scope);
        ArgumentNullException.ThrowIfNull(grant.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(grant.Resources);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(grant.ExpiresAt, grant.NotBefore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grant.AllowedUses);
    }

    private sealed class GrantState(SecurityGrant grant)
    {
        public SecurityGrant Grant { get; } = grant;
        public object SyncRoot { get; } = new();
        public int RemainingUses { get; set; } = grant.AllowedUses;
        public bool Revoked { get; set; }
    }
}
