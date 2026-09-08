// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Consumes exact publication authority before forwarding a grant-free prompt to the selected application channel.</summary>
public sealed class DefaultHumanQuestionBroker: IHumanQuestionBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly IHumanQuestionChannel _channel;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;

    /// <summary>Initializes the broker with a default source of fresh local enforcement-intent identities.</summary>
    /// <param name="grantStore">The non-null store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The non-null application-owned question presentation and resolution channel.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(ISecurityGrantStore grantStore, IHumanQuestionChannel channel)
        : this(grantStore, channel, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the broker with a replaceable source of fresh atomic permission-to-start identities.</summary>
    /// <param name="grantStore">The non-null store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The non-null application-owned question presentation and resolution channel.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(
        ISecurityGrantStore grantStore,
        IHumanQuestionChannel channel,
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
    public ComponentId SecurityAudience { get; } = new("agentkit.io.human-question");

    /// <inheritdoc/>
    public async ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!HumanQuestionEnforcementReceipt.HasCompatibleCapturedAuthorization(request))
        {
            return new HumanQuestionUnavailable(
                request.Id,
                "The captured authorization does not match the question publication.");
        }

        var enforcement = HumanQuestionEnforcementReceipt.Create(request, SecurityAudience);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return !HumanQuestionEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent)
            ? new HumanQuestionUnavailable(
                request.Id,
                HumanQuestionEnforcementReceipt.DenialMessage(consumption))
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
