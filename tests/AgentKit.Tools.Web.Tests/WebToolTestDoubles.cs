// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

internal sealed class StrictGrantStore: ISecurityGrantStore
{
    private readonly Dictionary<GrantId, SecurityGrant> _grants = [];
    private readonly HashSet<GrantId> _consumed = [];

    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        _grants.Add(grant.Id, grant);
        return ValueTask.CompletedTask;
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        var matches = Matches(grant, enforcement);
        var status = !matches
            ? GrantConsumptionStatus.Mismatch
            : _consumed.Add(grant.Id) ? GrantConsumptionStatus.Consumed : GrantConsumptionStatus.Exhausted;
        return ValueTask.FromResult(new GrantConsumptionResult(
            status,
            0,
            status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied."));
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enforcements.Add(enforcement);
        var matches = Matches(grant, enforcement);
        var status = !matches
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

    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
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

internal sealed class RecordingSecurityAuthority(StrictGrantStore store): ISecurityAuthority
{
    private int _grantSequence;

    internal int? DenyAtRequest { get; set; }
    internal List<SecurityRequest> Requests { get; } = [];

    public async ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (DenyAtRequest == Requests.Count)
        {
            return new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied."));
        }

        var grant = new SecurityGrant(
            new GrantId(SequenceGuid(++_grantSequence)),
            request.Id,
            request.Scope,
            request.Identity,
            request.Audience,
            request.Kind,
            request.Effect,
            request.Resources,
            request.InputFingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            request.Deadline,
            1);
        await store.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }

    private static Guid SequenceGuid(int value) =>
        Guid.Parse($"10000000-0000-0000-0000-{value:D12}");
}

internal sealed class SequenceSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    private int _value;
    public SecurityRequestId Create() => new(Guid.Parse($"20000000-0000-0000-0000-{++_value:D12}"));
}

internal sealed class SequenceNetworkOperationIdGenerator: IIdentifierGenerator<NetworkOperationId>
{
    private int _value;
    public NetworkOperationId Create() => new(Guid.Parse($"30000000-0000-0000-0000-{++_value:D12}"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal sealed class CallbackUtcNowTimeProvider(Func<DateTimeOffset> callback): TimeProvider
{
    public override DateTimeOffset GetUtcNow() => callback();
}
