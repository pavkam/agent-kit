// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>A strict in-memory grant store that consumes each registered grant once and records every enforcement.</summary>
/// <remarks>
/// Consumption succeeds only when the presented grant is the registered grant and every enforcement field matches, so a
/// test proves the effecting boundary presented evidence identical to what the authority authorized.
/// </remarks>
public sealed class ConsumingGrantStore: ISecurityGrantStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<GrantId, SecurityGrant> _grants = [];
    private readonly HashSet<GrantId> _consumed = [];
    private readonly List<SecurityEnforcementRequest> _enforcements = [];

    /// <summary>Gets or sets an exception thrown from every consumption, simulating an unavailable store.</summary>
    /// <value>The exception to throw, or <see langword="null"/> to consume normally.</value>
    public Exception? ConsumptionFault { get; set; }

    /// <summary>Gets a snapshot of every enforcement evidence presented for consumption, in order.</summary>
    public IReadOnlyList<SecurityEnforcementRequest> Enforcements
    {
        get
        {
            lock (_gate)
            {
                return [.. _enforcements];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            if (!_grants.TryAdd(grant.Id, grant) && _grants[grant.Id] != grant)
            {
                throw new InvalidOperationException($"Grant identifier '{grant.Id}' is already registered with different evidence.");
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ConsumptionFault is { } fault)
        {
            throw fault;
        }

        lock (_gate)
        {
            _enforcements.Add(enforcement);
            var status = !Matches(grant, enforcement)
                ? GrantConsumptionStatus.Mismatch
                : _consumed.Add(grant.Id) ? GrantConsumptionStatus.Consumed : GrantConsumptionStatus.Exhausted;
            return ValueTask.FromResult(new GrantConsumptionResult(
                status,
                0,
                status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied.",
                status == GrantConsumptionStatus.Consumed
                    ? new SecurityEnforcementIntentReceipt(
                        intent.Id,
                        grant.Id,
                        grant.RequestId,
                        enforcement,
                        intent.RequiredFence,
                        SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                        DateTimeOffset.UnixEpoch)
                    : null));
        }
    }

    /// <inheritdoc/>
    public ValueTask<GrantRevocationResult> RevokeAsync(
        GrantId grantId,
        RevocationReason reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
    }

    private bool Matches(SecurityGrant grant, SecurityEnforcementRequest enforcement) =>
        _grants.TryGetValue(grant.Id, out var registered)
        && registered == grant
        && grant.Scope == enforcement.Scope
        && grant.Identity == enforcement.Identity
        && grant.Authorization == enforcement.Authorization
        && grant.Audience == enforcement.Audience
        && grant.Kind == enforcement.Kind
        && grant.Effect == enforcement.Effect
        && grant.Resources.SequenceEqual(enforcement.Resources)
        && grant.InputFingerprint == enforcement.InputFingerprint
        && grant.RevocationVersion == enforcement.RevocationVersion;
}
