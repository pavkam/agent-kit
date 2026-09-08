// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Consumes exact single-use delegation authority immediately before child dispatch.</summary>
public sealed class DefaultTaskDelegationBroker: ITaskDelegationBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ITaskDelegationChannel _channel;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;

    /// <summary>Initializes the protected broker with a default source of fresh local enforcement-intent identities.</summary>
    /// <param name="grantStore">The non-null atomic validator and consumer of delegation grants.</param>
    /// <param name="channel">The non-null goal-aware child dispatcher.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(ISecurityGrantStore grantStore, ITaskDelegationChannel channel)
        : this(grantStore, channel, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the protected broker with a replaceable source of fresh atomic permission-to-start identities.</summary>
    /// <param name="grantStore">The non-null atomic validator and consumer of delegation grants.</param>
    /// <param name="channel">The non-null goal-aware child dispatcher.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(
        ISecurityGrantStore grantStore,
        ITaskDelegationChannel channel,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(intentIds);
        _grantStore = grantStore;
        _channel = channel;
        _intentIds = intentIds;
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
        if (!TaskDelegationEnforcementReceipt.HasCompatibleCapturedAuthorization(request))
        {
            return new TaskDelegationRejected(
                request.Prompt.Id,
                "The captured authorization does not match the task delegation.");
        }

        var enforcement = TaskDelegationEnforcementReceipt.Create(request, SecurityAudience);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return !TaskDelegationEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent)
            ? new TaskDelegationRejected(
                request.Prompt.Id,
                TaskDelegationEnforcementReceipt.DenialMessage(consumption))
            : await _channel.DelegateAsync(request.Prompt, cancellationToken).ConfigureAwait(false);
    }
}
