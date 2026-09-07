// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Consumes exact publication authority before forwarding a grant-free prompt to the selected application channel.</summary>
public sealed class DefaultHumanQuestionBroker: IHumanQuestionBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly IHumanQuestionChannel _channel;

    /// <summary>Initializes the broker over the authoritative grant store and selected application channel.</summary>
    /// <param name="grantStore">The store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The application-owned question presentation and resolution channel.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(ISecurityGrantStore grantStore, IHumanQuestionChannel channel)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(channel);
        _grantStore = grantStore;
        _channel = channel;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.io.human-question");

    /// <inheritdoc/>
    public async ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            new SecurityEnforcementRequest(
                new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation),
                request.Identity,
                SecurityAudience,
                SecurityOperationKind.StateMutation,
                SecurityEffect.Create,
                [HumanQuestionSecurityBinding.Resource(request.Id)],
                HumanQuestionSecurityBinding.Fingerprint(
                    request.Id,
                    request.Prompt,
                    request.Options,
                    request.AllowsFreeText,
                    request.Deadline),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        return consumption.Status != GrantConsumptionStatus.Consumed
            ? new HumanQuestionUnavailable(request.Id, consumption.SafeMessage)
            : await _channel.AskAsync(
                new HumanQuestionPrompt(
                    request.Id,
                    request.AgentId,
                    request.SessionId,
                    request.ToolCallId,
                    request.Correlation,
                    request.Identity,
                    request.Prompt,
                    request.Options,
                    request.AllowsFreeText,
                    request.Deadline),
                cancellationToken).ConfigureAwait(false);
    }
}
