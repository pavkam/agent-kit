// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Scripts grant-store consumption results so enforcement can be proven to refuse before any state change.</summary>
internal sealed class IntentReceiptGrantStore: ISecurityGrantStore
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;


    internal bool ReturnExactReceipt { get; set; } = true;

    internal bool Throw { get; set; }

    internal Action? OnConsumption { get; set; }

    internal SecurityEnforcementIntent? LastIntent { get; private set; }

    internal SecurityEnforcementRequest? LastEnforcement { get; private set; }

    internal int ConsumptionCount { get; private set; }

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        _ = grant;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConsumptionCount++;
        LastIntent = intent;
        LastEnforcement = enforcement;
        OnConsumption?.Invoke();
        if (Throw)
        {
            throw new InvalidOperationException("The grant store is down.");
        }

        var receipt = Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled
            ? new SecurityEnforcementIntentReceipt(
                ReturnExactReceipt ? intent.Id : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                grant.Id, grant.RequestId, enforcement, intent.RequiredFence, SecurityEnforcementBinding.Fingerprint(enforcement, intent), _now)
            : null;
        return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied.", receipt));
    }

    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }
}
