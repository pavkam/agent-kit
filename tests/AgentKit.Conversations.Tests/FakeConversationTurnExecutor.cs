// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Deterministic stand-in for <see cref="IConversationTurnExecutor"/> in unit tests.</summary>
internal sealed class FakeConversationTurnExecutor(FakeAgentLoop loop, FakeSessionCoordinator coordinator): IConversationTurnExecutor
{
    private readonly FakeAgentLoop _loop = loop ?? throw new ArgumentNullException(nameof(loop));
    private readonly FakeSessionCoordinator _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public AgentLoopRunRequest? LastRequest { get; private set; }

    public int RunCallCount { get; private set; }

    public async Task<SessionId> EnsureSessionAsync(
        AgentId agentId,
        ExecutionIdentity identity,
        SessionId? existingSessionId,
        CancellationToken cancellationToken = default)
    {
        if (existingSessionId is { } existing)
        {
            return existing;
        }

        if (_coordinator.CreateCallCount == 0)
        {
            var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
            var authorization = TestSecurityEvidence.Authorization(agentId, null, correlation, identity);
            var created = await _coordinator.CreateAsync(
                new SessionCreateRequest(
                    agentId,
                    identity,
                    authorization,
                    null,
                    new IdempotencyKey($"tests.conversation:{Guid.NewGuid()}"),
                    ExtensionData.Empty),
                TestSecurityEvidence.SessionProfile(),
                cancellationToken).ConfigureAwait(false);
            if (created is not SessionCreated)
            {
                throw new InvalidOperationException("The fake coordinator did not create a session.");
            }
        }

        return _coordinator.SessionId;
    }

    public async Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RunCallCount++;
        var sessionId = await EnsureSessionAsync(request.AgentId, request.Identity, request.SessionId, cancellationToken)
            .ConfigureAwait(false);
        var runRequest = BuildLoopRequest(request, sessionId);
        LastRequest = runRequest;
        var loopResult = await _loop.RunAsync(runRequest, CreateServices(), cancellationToken).ConfigureAwait(false);
        return ToFinished<TOutput>(loopResult);
    }

    public async Task<AgentLoopResult> SendObservedAsync(
        ConversationTurnRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RunCallCount++;
        var sessionId = await EnsureSessionAsync(request.AgentId, request.Identity, request.SessionId, cancellationToken)
            .ConfigureAwait(false);
        var runRequest = BuildLoopRequest(request, sessionId) with { Observer = request.Observer };
        LastRequest = runRequest;
        return await _loop.RunAsync(runRequest, CreateServices(), cancellationToken).ConfigureAwait(false);
    }

    private AgentLoopRunRequest BuildLoopRequest(ConversationTurnRunRequest request, SessionId sessionId)
    {
        var runId = new RunId(Guid.NewGuid());
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var authorization = TestSecurityEvidence.Authorization(request.AgentId, sessionId, correlation, request.Identity);
        return new AgentLoopRunRequest(
            request.AgentId,
            sessionId,
            _coordinator.BranchId,
            runId,
            request.Identity,
            authorization,
            TestSecurityEvidence.SessionProfile(),
            request.MaxTurns,
            request.AttemptTimeout,
            ExtensionData.Empty);
    }

    private static AgentRunServices CreateServices() =>
        new(
            new FakeSessionCoordinator(),
            new FakeSecurityProfileSelector(),
            new UnsupportedContextAssembler(),
            new CaptureTestToolInvoker(),
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])),
            ScriptedModelSelector.Selecting(new ModelDescriptor(
                new ModelAlias("chat"),
                new ProviderId("test-provider"),
                new ApiFamilyId("test-api"),
                new ModelId("test-model"),
                deploymentId: null,
                new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty),
                new ModelLimits(4096, 1024),
                pricing: null,
                ExtensionData.Empty)),
            new AliasLlmModelResolver(),
            new UnsupportedRunContinuationPolicy());

    private static AgentRunFinished<TOutput> ToFinished<TOutput>(AgentLoopResult loopResult)
    {
        var cursor = new MessageCursor(
            loopResult.AgentId,
            loopResult.SessionId,
            conversationId: null,
            loopResult.BranchId,
            loopResult.FinalVersion ?? new SessionVersion(1),
            new SessionSequence(0));
        TOutput? output = loopResult.Output is TOutput typed
            ? typed
            : typeof(TOutput) == typeof(string)
                ? (TOutput)(object)string.Concat(
                    loopResult.NewMessages.OfType<AssistantMessage>()
                        .SelectMany(static message => message.Parts.OfType<TextPart>().Select(static part => part.Text)))
                : default;
        return new AgentRunFinished<TOutput>(
            loopResult.AgentId,
            loopResult.SessionId,
            conversationId: null,
            loopResult.RunId,
            loopResult.Outcome,
            loopResult.Settlement,
            output,
            cursor,
            loopResult.NewMessages,
            loopResult.Usage,
            [],
            ExtensionData.Empty);
    }
}
