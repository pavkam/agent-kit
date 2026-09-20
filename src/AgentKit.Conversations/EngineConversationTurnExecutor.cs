// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Delegates conversational turns to <see cref="AgentEngine"/> and <see cref="Agent"/>.</summary>
internal sealed class EngineConversationTurnExecutor(AgentEngine engine, IIdentifierGenerator<InputId> inputIds): IConversationTurnExecutor
{
    private readonly AgentEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    private readonly IIdentifierGenerator<InputId> _inputIds = inputIds ?? throw new ArgumentNullException(nameof(inputIds));

    /// <inheritdoc/>
    public Task<SessionId> EnsureSessionAsync(
        AgentId agentId,
        ExecutionIdentity identity,
        SessionId? existingSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return EnsureSessionForAgentAsync(agentId, identity, existingSessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);
        var agent = (await _engine.GetAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false)).RequireResolved();
        var sessionId = await EnsureSessionAsync(agent, request.SessionId, request.Identity, cancellationToken).ConfigureAwait(false);
        var input = new AgentInput(
            _inputIds.Create(),
            InputDelivery.Steer,
            [new TextPart(request.UserText, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var options = new AgentRunOptions(request.MaxTurns, request.AttemptTimeout);
        return await agent.RunAsync<TOutput>(
            sessionId,
            request.Identity,
            input,
            options: options,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AgentLoopResult> SendObservedAsync(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserText);
        var agent = (await _engine.GetAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false)).RequireResolved();
        var sessionId = await EnsureSessionAsync(agent, request.SessionId, request.Identity, cancellationToken).ConfigureAwait(false);
        return await agent.SendAsync(
            new AgentSendRequest(
                request.Identity,
                request.UserText,
                sessionId,
                request.MaxTurns,
                request.AttemptTimeout,
                request.Observer),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SessionId> EnsureSessionForAgentAsync(
        AgentId agentId,
        ExecutionIdentity identity,
        SessionId? sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId is { } existing)
        {
            return existing;
        }

        var agent = (await _engine.GetAgentAsync(agentId, cancellationToken).ConfigureAwait(false)).RequireResolved();
        return await EnsureSessionAsync(agent, sessionId, identity, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SessionId> EnsureSessionAsync(
        Agent agent,
        SessionId? sessionId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        if (sessionId is { } existing)
        {
            return existing;
        }

        var correlation = Guid.NewGuid();
        var result = await agent.CreateSessionAsync(
            identity,
            new IdempotencyKey($"agentkit.conversation:{agent.Id}:create:{correlation}"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return result is AgentSessionCreated created
            ? created.SessionId
            : throw new InvalidOperationException(result is AgentSessionCreationFailed failed
                ? $"Could not create the underlying session: {failed.Failure.SafeMessage}"
                : $"Could not create the underlying session: {result.GetType().Name}");
    }
}
