// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

internal sealed class RecordingGrantStore: ISecurityGrantStore
{
    internal List<SecurityEnforcementRequest> Enforcements { get; } = [];
    internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default)
    {
        Enforcements.Add(enforcement);
        var matches = grant.Scope == enforcement.Scope && grant.Identity == enforcement.Identity && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind && grant.Effect == enforcement.Effect && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint && grant.RevocationVersion == enforcement.RevocationVersion;
        var status = matches ? Status : GrantConsumptionStatus.Mismatch;
        return ValueTask.FromResult(new GrantConsumptionResult(status, 0, $"Grant {status}."));
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
