// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using System.Diagnostics;

internal sealed class RecordingGrantStore: ISecurityGrantStore
{
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal List<SecurityEnforcementIntent> Intents { get; } = [];
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    internal bool IncludeReceipt { get; set; } = true;
    internal bool ReturnExactReceipt { get; set; } = true;
    internal Action? OnConsume { get; set; }

    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, $"Grant {Status}."));
    }

    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        Intents.Add(intent);
        OnConsume?.Invoke();
        var status = Matches(grant, enforcement) ? Status : GrantConsumptionStatus.Mismatch;
        var receipt = IncludeReceipt && (status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled)
            ? new SecurityEnforcementIntentReceipt(
                ReturnExactReceipt ? intent.Id : new SecurityEnforcementIntentId(
                    Guid.Parse("f0000000-0000-0000-0000-00000000000f")),
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch)
            : null;
        return ValueTask.FromResult(new GrantConsumptionResult(status, 0, $"Grant {status}.", receipt));
    }

    private static bool Matches(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        Debug.Assert(grant is not null, "A grant-store comparison receives the broker's non-null grant.");
        Debug.Assert(enforcement is not null, "A grant-store comparison receives the broker's non-null evidence.");
        return grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Authorization == enforcement.Authorization
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint
            && grant.RevocationVersion == enforcement.RevocationVersion;
    }
}

internal sealed class RecordingDelegationChannel: ITaskDelegationChannel
{
    internal List<TaskDelegationPrompt> Prompts { get; } = [];
    public ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationPrompt prompt, CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);
        return ValueTask.FromResult<TaskDelegationResult>(new TaskDelegationChildResult(
            prompt.Id, new GoalId(Guid.NewGuid()), prompt.TargetAgentId, new SessionId(Guid.NewGuid()), null, null,
            TaskDelegationStatus.Succeeded, "Done.", SideEffectCertainty.DefinitelyPerformed));
    }
}

internal sealed class FixedSecurityEnforcementIntentIdGenerator(SecurityEnforcementIntentId id):
    IIdentifierGenerator<SecurityEnforcementIntentId>
{
    internal int Calls { get; private set; }

    public SecurityEnforcementIntentId Create()
    {
        Calls++;
        return id;
    }
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
