// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Consumes exact single-use delegation authority immediately before child dispatch.</summary>
public sealed class DefaultTaskDelegationBroker: ITaskDelegationBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ITaskDelegationChannel _channel;

    /// <summary>Initializes the protected broker over the grant store and application goal channel.</summary>
    /// <param name="grantStore">The atomic grant validator and consumer.</param>
    /// <param name="channel">The goal-aware child dispatcher.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(ISecurityGrantStore grantStore, ITaskDelegationChannel channel)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(channel);
        _grantStore = grantStore;
        _channel = channel;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.goals.delegation");

    /// <inheritdoc/>
    public async ValueTask<TaskDelegationResult> DelegateAsync(
        TaskDelegationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var prompt = request.Prompt;
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            new SecurityEnforcementRequest(
                new SecurityAuthorizationScope(prompt.ParentAgentId, prompt.ParentSessionId, prompt.Correlation),
                prompt.Identity,
                SecurityAudience,
                SecurityOperationKind.Delegation,
                SecurityEffect.Create,
                [TaskDelegationSecurityBinding.Resource(prompt.Id)],
                TaskDelegationSecurityBinding.Fingerprint(prompt),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        return consumption.Status != GrantConsumptionStatus.Consumed
            ? new TaskDelegationRejected(prompt.Id, consumption.SafeMessage)
            : await _channel.DelegateAsync(prompt, cancellationToken).ConfigureAwait(false);
    }
}
