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

    public ConversationSessionOptions LoopOptions { get; set; } = ConversationSessionOptionsFactory.Valid();

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
        var runId = new RunId(Guid.NewGuid());
        await AdmitUserMessageAsync(request, sessionId, runId, cancellationToken).ConfigureAwait(false);
        var runRequest = BuildLoopRequest(request, sessionId, runId);
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
        var runId = new RunId(Guid.NewGuid());
        await AdmitUserMessageAsync(request, sessionId, runId, cancellationToken).ConfigureAwait(false);
        var runRequest = BuildLoopRequest(request, sessionId, runId) with { Observer = request.Observer };
        LastRequest = runRequest;
        return await _loop.RunAsync(runRequest, CreateServices(), cancellationToken).ConfigureAwait(false);
    }

    private async Task AdmitUserMessageAsync(
        ConversationTurnRunRequest request,
        SessionId sessionId,
        RunId runId,
        CancellationToken cancellationToken)
    {
        var profile = TestSecurityEvidence.SessionProfile();
        var readCorrelation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var readAuthorization = TestSecurityEvidence.Authorization(
            request.AgentId,
            sessionId,
            readCorrelation,
            request.Identity);
        var readContext = new SessionOperationContext(
            request.AgentId,
            sessionId,
            null,
            readCorrelation,
            request.Identity,
            readAuthorization);
        var head = await _coordinator.ReadAsync(
            new SessionReadRequest(readContext, _coordinator.BranchId, new SessionSequence(0), pageSize: 1),
            profile,
            cancellationToken).ConfigureAwait(false);
        if (head is SessionReadFailed)
        {
            throw new AgentAdmissionRejectedException(Rejection(request, "The session's current history snapshot could not be captured."));
        }

        if (head is not SessionPage { Snapshot: { } snapshot })
        {
            throw new AgentAdmissionRejectedException(Rejection(request, "The session's current history snapshot could not be captured."));
        }

        SessionEntryId? causalParentId = null;
        if (snapshot.UpperSequence.Value > 0)
        {
            var tail = await _coordinator.ReadAsync(
                new SessionReadRequest(
                    readContext,
                    _coordinator.BranchId,
                    new SessionSequence(snapshot.UpperSequence.Value - 1),
                    pageSize: 1,
                    snapshot),
                profile,
                cancellationToken).ConfigureAwait(false);
            if (tail is SessionPage { Entries.Length: > 0 } tipPage)
            {
                causalParentId = tipPage.Entries[^1].Id;
            }
        }

        var turnId = new TurnId(Guid.NewGuid());
        var appendCorrelation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var appendAuthorization = TestSecurityEvidence.Authorization(
            request.AgentId,
            sessionId,
            appendCorrelation,
            request.Identity);
        var appendContext = new SessionOperationContext(
            request.AgentId,
            sessionId,
            null,
            appendCorrelation,
            request.Identity,
            appendAuthorization);
        var userMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            request.AgentId,
            sessionId,
            conversationId: null,
            _coordinator.BranchId,
            runId,
            turnId,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(request.UserText, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var entry = new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(request.AgentId, sessionId),
            appendCorrelation,
            _coordinator.BranchId,
            new SessionSequence(snapshot.UpperSequence.Value + 1),
            causalParentId,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            userMessage);
        var appendResult = await _coordinator.AppendAsync(
            new SessionAppendRequest(
                appendContext,
                _coordinator.BranchId,
                snapshot.Version,
                new IdempotencyKey($"tests.conversation.append:{runId}"),
                [entry]),
            profile,
            cancellationToken).ConfigureAwait(false);
        if (appendResult is SessionAppended)
        {
            return;
        }

        throw new AgentAdmissionRejectedException(Rejection(request, DescribeAppendFailure(appendResult)));
    }

    private AgentAdmissionRejection Rejection(ConversationTurnRunRequest request, string reason) =>
        new(
            request.AgentId,
            LoopOptions.AgentDefinitionRevision,
            new AgentCatalogVersion(1),
            reason);

    private static string DescribeAppendFailure(SessionAppendResult result) => result switch
    {
        SessionAppendFailed failed => $"Could not record the message: {failed.SafeMessage}",
        SessionAppendConflict conflict =>
            $"The conversation changed concurrently (expected version {conflict.ExpectedVersion.Value}, actual version {conflict.ActualVersion.Value}).",
        SessionAppendNotFound => "The session was not found.",
        _ => "Could not record the message.",
    };

    private AgentLoopRunRequest BuildLoopRequest(ConversationTurnRunRequest request, SessionId sessionId, RunId runId)
    {
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId: null);
        var authorization = TestSecurityEvidence.Authorization(request.AgentId, sessionId, correlation, request.Identity);
        var options = LoopOptions;
        return new AgentLoopRunRequest(
            request.AgentId,
            sessionId,
            _coordinator.BranchId,
            runId,
            request.Identity,
            authorization,
            TestSecurityEvidence.SessionProfile(),
            options.ModelSelectionPolicy!,
            options.ModelRequirements,
            [.. options.Instructions],
            [.. options.Tools],
            options.ToolChoice,
            options.RequestSettings,
            request.MaxTurns,
            request.AttemptTimeout,
            ExtensionData.Empty)
        {
            Output = options.Output,
            BudgetLimits = [.. options.BudgetLimits],
        };
    }

    private static AgentRunServices CreateServices() =>
        new(
            new FakeSessionCoordinator(),
            new FakeSecurityProfileSelector(),
            new UnsupportedContextAssembler(),
            new CaptureTestToolExecutor(),
            toolCatalogCaptures: null,
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
        var output = loopResult.Output is TOutput typed
            ? typed
            : typeof(TOutput) == typeof(string)
                ? (TOutput) (object) string.Concat(
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
