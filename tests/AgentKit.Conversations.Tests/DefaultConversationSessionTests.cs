// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Verifies DefaultConversationSession behavior and contracts.</summary>
public sealed class DefaultConversationSessionTests
{
    [Fact]
    public async Task UnsupportedContextAssembler_WhenInvokedDirectly_ThrowsBecauseTheScriptedLoopNeverReachesContextAssembly()
    {
        var assembler = new UnsupportedContextAssembler();

        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await assembler.AssembleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnsupportedRunContinuationPolicy_WhenInvokedDirectly_ThrowsBecauseTheScriptedLoopNeverReachesContinuationEvaluation()
    {
        var policy = new UnsupportedRunContinuationPolicy();

        _ = await Should.ThrowAsync<NotSupportedException>(
            async () => await policy.DecideAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_WhenSnapshotVersionDiffersFromUpperSequence_UsesEachExactCoordinate()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = request => new SessionPage(
                [],
                request.FromSequenceExclusive,
                hasMore: false,
                new SessionReadSnapshot(request.Context.ToAddress(), request.BranchId, new SessionVersion(1), new SessionSequence(2))),
        };
        var session = CreateSession(coordinator);

        _ = await session.SendAsync("hello", TestContext.Current.CancellationToken);

        var append = coordinator.LastAppendRequest.ShouldNotBeNull();
        append.ExpectedVersion.ShouldBe(new SessionVersion(1));
        append.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(3));
    }

    [Fact]
    public async Task SendAsync_WhenFirstCalled_LogsSessionCreationTurnStartAndSettlement()
    {
        var logger = new RecordingLogger<DefaultConversationSession>();
        using var session = CreateSession(logger: logger);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var entries = logger.Snapshot();
        entries.ShouldContain(static entry => entry.EventId.Id == 24005);
        var started = entries.Single(static entry => entry.EventId.Id == 24000);
        started.State["AgentId"].ShouldBe(ConversationSessionOptionsFactory.AgentId);
        var settled = entries.Single(static entry => entry.EventId.Id == 24001);
        settled.State["EventCount"].ShouldBe(result.Events.Length);
        entries.SelectMany(static entry => entry.State.Values.Select(static value => value?.ToString()).Append(entry.Message))
            .ShouldNotContain("hi");
    }

    [Fact]
    public async Task SendAsync_WhenAppendDoesNotSucceed_LogsTurnAdmissionFailed()
    {
        var logger = new RecordingLogger<DefaultConversationSession>();
        var coordinator = new FakeSessionCoordinator { AppendResult = new SessionAppendFailed("no capacity") };
        using var session = CreateSession(coordinator: coordinator, logger: logger);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 24002);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.State["AgentId"].ShouldBe(ConversationSessionOptionsFactory.AgentId);
    }

    [Fact]
    public async Task SendAsync_WhenCancellationTokenIsAlreadyCancelled_LogsTurnCancelled()
    {
        var logger = new RecordingLogger<DefaultConversationSession>();
        using var session = CreateSession(logger: logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.SendAsync("hi", cts.Token));

        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 24003);
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task SendAsync_WhenTheLoopThrowsANonCancellationException_LogsTurnFaulted()
    {
        var logger = new RecordingLogger<DefaultConversationSession>();
        var fault = new InvalidOperationException("loop fault");
        var loop = new FakeAgentLoop { ResultFactory = _ => throw fault };
        using var session = CreateSession(loop: loop, logger: logger);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));

        exception.ShouldBeSameAs(fault);
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 24004);
        entry.Level.ShouldBe(LogLevel.Error);
        entry.State["ErrorType"].ShouldBe(fault.GetType().FullName);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorThrows_LogsHistoryReadFaulted()
    {
        var logger = new RecordingLogger<DefaultConversationSession>();
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => throw new InvalidOperationException("stored conversation secret"),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator, logger: logger);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        _ = await session.ReadHistoryAsync(new SessionSequence(0), 10, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 24006);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void Constructor_WhenSessionCoordinatorIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            null!, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler, args.ToolInvoker,
            args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("sessionCoordinator");
    }

    [Fact]
    public void Constructor_WhenSecurityProfileSelectorIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, null!, args.LoopScopeFactory, args.ContextAssembler, args.ToolInvoker,
            args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("securityProfileSelector");
    }

    [Fact]
    public void Constructor_WhenLoopScopeFactoryIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, null!, args.ContextAssembler, args.ToolInvoker,
            args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("loopScopeFactory");
    }

    [Fact]
    public void Constructor_WhenContextAssemblerIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, null!, args.ToolInvoker,
            args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("contextAssembler");
    }

    [Fact]
    public void Constructor_WhenToolInvokerIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler, null!,
            args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("toolInvoker");
    }

    [Fact]
    public void Constructor_WhenModelCatalogIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, null!, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("modelCatalog");
    }

    [Fact]
    public void Constructor_WhenModelSelectorIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, null!, args.LlmModelResolver, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("modelSelector");
    }

    [Fact]
    public void Constructor_WhenLlmModelResolverIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, null!, args.ContinuationPolicy, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("llmModelResolver");
    }

    [Fact]
    public void Constructor_WhenContinuationPolicyIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, null!, args.RunIds,
            args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("continuationPolicy");
    }

    [Fact]
    public void Constructor_WhenRunIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            null!, args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("runIds");
    }

    [Fact]
    public void Constructor_WhenOperationIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, null!, args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("operationIds");
    }

    [Fact]
    public void Constructor_WhenMessageIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, args.OperationIds, null!, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("messageIds");
    }

    [Fact]
    public void Constructor_WhenSessionEntryIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, args.OperationIds, args.MessageIds, null!, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("sessionEntryIds");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, args.OperationIds, args.MessageIds, args.SessionEntryIds, null!, args.Options));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider, null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenOptionsValueIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.LoopScopeFactory, args.ContextAssembler,
            args.ToolInvoker, args.ModelCatalog, args.ModelSelector, args.LlmModelResolver, args.ContinuationPolicy,
            args.RunIds, args.OperationIds, args.MessageIds, args.SessionEntryIds, args.TimeProvider,
            new NullValueOptions()));

        exception.ParamName.ShouldBe("optionValues");
    }

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AgentId = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.Identity = null));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenSecurityProfileKeyIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.SecurityProfileKey = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenConfigurationVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.ConfigurationVersion = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.SessionProfile = null));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenModelSelectionPolicyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.ModelSelectionPolicy = null));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAgentIdDiffersFromTheOptionsAgentId_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        options.AgentId = new AgentId(Guid.NewGuid());

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAgentRevisionDiffersFromTheOptionsRevision_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        options.AgentDefinitionRevision = new AgentDefinitionRevision(99);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAgentSecurityProfileDiffersFromTheOptionsSecurityProfileKey_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        options.SecurityProfileKey = new SecurityProfileKey("a-different-security-profile");

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAgentSessionProfileDiffersFromTheOptionsSessionProfileKey_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        var original = options.SessionProfile!;
        options.SessionProfile = new SessionProfileSnapshot(
            new SessionProfileReference(new SessionProfileKey("a-different-session-profile"), original.Reference.Version),
            original.CoordinatorKey,
            original.RunCoordinatorKey,
            original.DefaultStoreKey,
            original.RequiredStoreCapabilities,
            original.RequiresDurableStore,
            original.RequiresDistributedFencing,
            original.RetentionProfile,
            original.BusyBehavior,
            original.MaximumAppendEntries,
            original.MaximumPageSize,
            original.VerifySnapshotHashes,
            original.DeleteOnDispose,
            original.ConfigurationFingerprint);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenConfigurationVersionDiffersFromTheOptionsConfigurationVersion_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        options.ConfigurationVersion = new ConfigurationVersion(99);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenConfigurationFingerprintDiffersFromTheSessionProfileFingerprint_ThrowsArgumentException()
    {
        var options = ConversationSessionOptionsFactory.ValidWithExactEvidence();
        options.Configuration = new EffectiveConfigurationSnapshot(
            options.ConfigurationVersion, new ContentHash("sha256:a-different-fingerprint"), [], []);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task SendAsync_WhenExactAgentAndConfigurationEvidenceIsSupplied_UsesThemToBuildTheRunRequest()
    {
        var loop = new FakeAgentLoop();
        using var session = CreateSession(loop: loop, options: ConversationSessionOptionsFactory.ValidWithExactEvidence());

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var request = loop.LastRequest.ShouldNotBeNull();
        _ = request.Agent.ShouldNotBeNull();
        _ = request.Configuration.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.MaxTurns = maxTurns));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AttemptTimeout = TimeSpan.Zero));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AttemptTimeout = TimeSpan.FromSeconds(-1)));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenToolPresentationBindingIsValid_CapturesItWithoutThrowing()
    {
        var advertised = Advertised(new ToolId("read"), "read");
        var descriptor = Descriptor(advertised.Id);
        using var session = CreateSession(configureOptions: options =>
        {
            options.Tools.Add(advertised);
            options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptor, advertised));
        });

        _ = session.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WhenToolPresentationBindingReferencesAToolNotAdvertised_ThrowsArgumentException()
    {
        var advertised = Advertised(new ToolId("read"), "read");
        var descriptor = Descriptor(advertised.Id);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(configureOptions: options =>
            // "advertised" itself is never added to options.Tools, so the binding references an unadvertised tool.
            options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptor, advertised))));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenTwoToolPresentationBindingsShareTheSameAdvertisedAlias_ThrowsArgumentException()
    {
        var firstAdvertised = Advertised(new ToolId("read"), "shared-name");
        var firstDescriptor = Descriptor(firstAdvertised.Id);
        var secondAdvertised = Advertised(new ToolId("write"), "shared-name");
        var secondDescriptor = Descriptor(secondAdvertised.Id);

        var exception = Should.Throw<ArgumentException>(() => CreateSession(configureOptions: options =>
        {
            options.Tools.Add(firstAdvertised);
            options.Tools.Add(secondAdvertised);
            options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(firstDescriptor, firstAdvertised));
            options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(secondDescriptor, secondAdvertised));
        }));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenUserTextIsBlank_ThrowsArgumentException(string? userText)
    {
        using var session = CreateSession();

        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await session.SendAsync(userText!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("userText");
    }

    [Fact]
    public async Task SendAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var session = CreateSession();
        session.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var session = CreateSession();

        session.Dispose();
        Should.NotThrow(session.Dispose);
    }

    [Fact]
    public async Task SendAsync_WhenFirstCalled_CreatesSessionAppendsMessageAndProjectsLoopEvents()
    {
        var coordinator = new FakeSessionCoordinator();
        var loop = new FakeAgentLoop();
        var call1 = FakeMessages.ToolCall("search", /*lang=json,strict*/ "{\"q\":\"test\"}");
        var call2 = FakeMessages.ToolCall("write", /*lang=json,strict*/ "{\"path\":\"a\"}");
        loop.ResultFactory = request => new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new AgentRunCompleted(FakeMessages.Assistant(
                request,
                [
                    new TextPart("   ", TextSemantics.Plain, ExtensionData.Empty),
                    new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty),
                    call1,
                ])),
            [
                FakeMessages.Assistant(
                    request,
                    [
                        new TextPart("   ", TextSemantics.Plain, ExtensionData.Empty),
                        new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty),
                        call1,
                    ]),
                FakeMessages.Tool(request, [
                    FakeMessages.ToolSuccess(call1, "42"),
                    FakeMessages.ToolFailure(call2, "boom", "stderr details"),
                ]),
            ],
            new SessionVersion(2));
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(1);
        loop.CallCount.ShouldBe(1);

        var committedEntry = coordinator.LastAppendRequest.ShouldNotBeNull().Entries[0].ShouldBeOfType<MessageSessionEntry>();
        var committedMessage = committedEntry.Message.ShouldBeOfType<UserMessage>();
        committedMessage.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("hi");

        result.Events.Length.ShouldBe(4);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldBe("Hello there");
        var toolCallEvent = result.Events[1].ShouldBeOfType<ConversationToolCallEvent>();
        toolCallEvent.ToolName.ShouldBe("search");
        toolCallEvent.CallId.ShouldBe(call1.CallId);
        toolCallEvent.ArgumentsJson.ShouldBe(/*lang=json,strict*/ "{\"q\":\"test\"}");
        var successEvent = result.Events[2].ShouldBeOfType<ConversationToolResultEvent>();
        successEvent.ToolName.ShouldBe("search");
        successEvent.CallId.ShouldBe(call1.CallId);
        successEvent.Succeeded.ShouldBeTrue();
        successEvent.Summary.ShouldBe("42");
        var failureEvent = result.Events[3].ShouldBeOfType<ConversationToolResultEvent>();
        failureEvent.ToolName.ShouldBe("write");
        failureEvent.CallId.ShouldBe(call2.CallId);
        failureEvent.Succeeded.ShouldBeFalse();
        failureEvent.Summary.ShouldBe("boom\nstderr details");
    }

    [Fact]
    public async Task SendAsync_WhenCalled_DerivesIdempotencyKeysFromInjectedIdentitiesInsteadOfAmbientRandomness()
    {
        // Both session creation and the user-message append used new IdempotencyKey(Guid.NewGuid().ToString()),
        // bypassing the class's own injected identifier generators. Deterministic creation must use an injected
        // generator rather than ambient Guid.NewGuid, and a stable key derived from the same generated identity
        // is required so a test (or a lower-layer retry of the exact same request) can observe a deterministic,
        // reproducible key instead of fresh, unobservable randomness on every construction.
        var coordinator = new FakeSessionCoordinator();
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId,
                new AgentRunCompleted(FakeMessages.Assistant(request, [])), [], new SessionVersion(1)),
        };
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        _ = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        var createKey = coordinator.LastCreateRequest.ShouldNotBeNull().IdempotencyKey.Value;
        var appendKey = coordinator.LastAppendRequest.ShouldNotBeNull().IdempotencyKey.Value;
        Guid.TryParse(createKey, out _).ShouldBeFalse();
        Guid.TryParse(appendKey, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenTheScopedLoopIsOnlyAsyncDisposable_DoesNotThrowAfterTheTurnAlreadyCommitted()
    {
        // IAgentLoop is registered scoped and is an explicitly replaceable extension point. Disposing the loop
        // scope synchronously (a plain `using`) makes Microsoft DI's ServiceProviderEngineScope.Dispose() throw
        // InvalidOperationException when the scope holds a service that implements only IAsyncDisposable, and
        // that exception fires after RunAsync already returned - turning a fully committed, successful turn into
        // a reported fault and losing its ConversationTurnResult.
        var loop = new FakeAgentLoop();
        var services = new ServiceCollection();
        _ = services.AddKeyedScoped<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, (_, _) => loop);
        var loopScopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator, loopScopeFactory: loopScopeFactory);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        loop.DisposeAsyncCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_WhenAssistantResponseReportsUsage_ProjectsAUsageEventAfterItsContent()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);
        var loop = new FakeAgentLoop
        {
            ResultFactory = request =>
            {
                var assistant = FakeMessages.Assistant(
                    request,
                    [new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty)],
                    usage);
                return new AgentLoopResult(
                    request.AgentId,
                    request.SessionId,
                    request.BranchId,
                    request.RunId,
                    new AgentRunCompleted(assistant),
                    [assistant],
                    new SessionVersion(1));
            },
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.Length.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldBe("Hello there");
        result.Events[1].ShouldBeOfType<ConversationUsageEvent>().Usage.ShouldBe(usage);
    }

    [Fact]
    public async Task SendAsync_WhenAssistantResponseDoesNotReportUsage_ProjectsNoUsageEvent()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request =>
            {
                var assistant = FakeMessages.Assistant(request, "Hello there");
                return new AgentLoopResult(
                    request.AgentId,
                    request.SessionId,
                    request.BranchId,
                    request.RunId,
                    new AgentRunCompleted(assistant),
                    [assistant],
                    new SessionVersion(1));
            },
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Events.ShouldNotContain(conversationEvent => conversationEvent is ConversationUsageEvent);
    }

    [Fact]
    public async Task SendAsync_WhenLoopOutcomeIsNotCompleted_ReturnsUnsuccessfulResultDescribingWhy()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId,
                request.SessionId,
                request.BranchId,
                request.RunId,
                new AgentRunTurnLimitReached(request.MaxTurns),
                [],
                new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var description = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>();
        description.Text.ShouldContain("turn limit");
    }

    [Fact]
    public async Task SendAsync_WhenLoopOutcomeIsNotCompleted_LogsTurnRunNotCompletedInsteadOfTurnAdmissionFailed()
    {
        // The message was successfully admitted (session load, read, and append all succeeded); only the run
        // itself did not settle with a completed outcome. This must not be indistinguishable from a rejected
        // append: it needs its own log event and outcome token, not TurnAdmissionFailed/"admission_failed".
        var logger = new RecordingLogger<DefaultConversationSession>();
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId,
                request.SessionId,
                request.BranchId,
                request.RunId,
                new AgentRunTurnLimitReached(request.MaxTurns),
                [],
                new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop, logger: logger);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        logger.Snapshot().ShouldNotContain(static entry => entry.EventId.Id == 24002);
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 24007);
        entry.Level.ShouldBe(LogLevel.Information);
        entry.State["AgentId"].ShouldBe(ConversationSessionOptionsFactory.AgentId);
        entry.State["OutcomeType"].ShouldBe(nameof(AgentRunTurnLimitReached));
    }

    [Fact]
    public async Task SendAsync_WhenLoopFailsWithProviderFailureCarryingDiagnosticCause_DoesNotExposeItInEvents()
    {
        // ProviderFailure.DiagnosticCause "may carry sensitive transport detail and is intended for logs and diagnostics only".
        const string secret = "Bearer sk-live-SECRET-TOKEN";
        var failure = new ProviderFailure(
            ProviderFailureKind.Unavailable,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: 503,
            providerCode: null,
            retryAfter: null,
            "The provider is unavailable.",
            new HttpRequestException($"connection refused while sending {secret}"),
            ExtensionData.Empty);
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId, new AgentRunFailed(failure), [], new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        foreach (var text in result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text))
        {
            text.ShouldNotContain(secret);
            text.ShouldNotContain("HttpRequestException");
            text.ShouldNotContain("DiagnosticCause");
        }
    }

    [Fact]
    public async Task SendAsync_WhenAssistantResponseIncludesReasoning_ProjectsAReasoningEvent()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request =>
            {
                var assistant = FakeMessages.Assistant(
                    request,
                    [new ReasoningPart(
                        new ReasoningContent("thinking it through", ReasoningVisibility.Visible, null, ExtensionData.Empty),
                        ExtensionData.Empty)]);
                return new AgentLoopResult(
                    request.AgentId, request.SessionId, request.BranchId, request.RunId,
                    new AgentRunCompleted(assistant), [assistant], new SessionVersion(1));
            },
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationReasoningEvent>()
            .Text.ShouldBe("thinking it through");
    }

    [Theory]
    [MemberData(nameof(IncompleteOutcomes))]
    public async Task SendAsync_WhenLoopOutcomeIsIncomplete_DescribesEveryTypedOutcomeSafely(
        AgentRunOutcome outcome, string expectedFragment)
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId, outcome, [], new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var description = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>();
        description.Text.ShouldContain(expectedFragment);
    }

    public static TheoryData<AgentRunOutcome, string> IncompleteOutcomes()
    {
        var data = new TheoryData<AgentRunOutcome, string>
        {
            { new AgentRunSessionOperationFailed("store fault"), "session operation failed" },
            { new AgentRunModelSelectionFailed("no usable model", []), "no usable model could be selected" },
            {
                new AgentRunContextPreparationFailed(new ContextPreparationFailure(
                    ContextPreparationFailureKind.EmptyHistory, "history is empty", ExtensionData.Empty)),
                "context preparation failed"
            },
            { new AgentRunCancelled("the caller cancelled"), "the caller cancelled" },
            { new AgentRunIdle(), "ended idle" },
            { new AgentRunInvalidState("inconsistent evidence"), "inconsistent evidence" },
            {
                new AgentRunOutputRejected(new OutputRejected(new OutputValidationFailure(
                    OutputValidationFailureKind.ValidatorFailed, "output failed validation", []))),
                "output was rejected"
            },
            {
                new AgentRunOutputRejected(new OutputConfigurationRejected(new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.MalformedSchema, "schema is malformed", []))),
                "output definition could not be applied"
            },
        };
        return data;
    }

    [Fact]
    public async Task SendAsync_WhenAppendConflicts_DescribesTheConflictWithBothVersions()
    {
        var coordinator = new FakeSessionCoordinator
        {
            AppendResult = new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2)),
        };
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>()
            .Text.ShouldContain("changed concurrently");
    }

    [Fact]
    public async Task SendAsync_WhenAppendReportsSessionNotFound_DescribesItSafely()
    {
        var coordinator = new FakeSessionCoordinator
        {
            AppendResult = new SessionAppendNotFound(new SessionAddress(ConversationSessionOptionsFactory.AgentId, new SessionId(Guid.NewGuid()))),
        };
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>()
            .Text.ShouldContain("not found");
    }

    [Fact]
    public async Task Dispose_WhenCalledDuringAnInFlightTurn_DoesNotReplaceTheTurnResultWithObjectDisposedException()
    {
        var loop = new FakeAgentLoop { Gate = new TaskCompletionSource(), EnteredSignal = new TaskCompletionSource() };
        var session = CreateSession(loop: loop);
        var pending = session.SendAsync("hi", TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;

        session.Dispose();
        loop.Gate.SetResult();

        // The loop ran and committed; the caller must receive that result (or a typed failure), never a disposal fault.
        var exception = await Record.ExceptionAsync(async () => await pending);
        exception.ShouldNotBeOfType<ObjectDisposedException>();
    }

    [Fact]
    public async Task SendAsync_WhenCalledASecondTime_ReusesTheExistingSessionAndBranch()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);

        _ = await session.SendAsync("first", TestContext.Current.CancellationToken);
        _ = await session.SendAsync("second", TestContext.Current.CancellationToken);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(2);
        coordinator.LoadCallCount.ShouldBe(2);
        var secondAppend = coordinator.LastAppendRequest.ShouldNotBeNull();
        secondAppend.Context.SessionId.ShouldBe(coordinator.SessionId);
        secondAppend.BranchId.ShouldBe(coordinator.BranchId);
    }

    [Fact]
    public async Task SendAsync_WhenAppendDoesNotSucceed_ReturnsFailureWithoutRunningTheLoop()
    {
        var coordinator = new FakeSessionCoordinator { AppendResult = new SessionAppendFailed("no capacity") };
        var loop = new FakeAgentLoop();
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.Length.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldContain("Could not record the message");
        loop.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenSessionCreationDoesNotSucceed_ThrowsInvalidOperationException()
    {
        var coordinator = new FakeSessionCoordinator { CreateResult = new SessionCreateFailed("test failure") };
        using var session = CreateSession(coordinator: coordinator);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_WhenAuthorizationCaptureDoesNotSucceed_ThrowsInvalidOperationException()
    {
        var selector = new FakeSecurityProfileSelector { Result = new SecurityAuthorizationCaptureUnavailable("unavailable") };
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator, selector: selector);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));

        coordinator.CreateCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenCancellationTokenIsAlreadyCancelled_PropagatesOperationCanceledException()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.SendAsync("hi", cts.Token));

        coordinator.CreateCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenCalledWhileAPriorTurnIsInFlight_SerializesRatherThanInterleaving()
    {
        var coordinator = new FakeSessionCoordinator();
        var loop = new FakeAgentLoop { Gate = new TaskCompletionSource(), EnteredSignal = new TaskCompletionSource() };
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var firstTurn = session.SendAsync("first", TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;
        var secondTurn = session.SendAsync("second", TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(1);
        loop.CallCount.ShouldBe(1);

        loop.Gate.SetResult();
        var results = await Task.WhenAll(firstTurn, secondTurn);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(2);
        loop.CallCount.ShouldBe(2);
        results[0].Succeeded.ShouldBeTrue();
        results[1].Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenObserved_ForwardsIncrementalTextBeforeCompletionAndOneTerminalEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = new FakeAgentLoop
        {
            Gate = new TaskCompletionSource(),
            EnteredSignal = new TaskCompletionSource(),
            ProgressEvent = new AgentRunModelResponseEvent(
                new TurnId(Guid.NewGuid()),
                new ModelPartDelta(requestId, 1, 0, new TextContentDelta("hello "))),
        };
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession(loop: loop);

        var pending = session.SendAsync("hi", observer, TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;

        pending.IsCompleted.ShouldBeFalse();
        observer.Events.Count.ShouldBe(2);
        _ = observer.Events[0].ShouldBeOfType<ConversationSessionBoundEvent>();
        observer.Events[1].ShouldBeOfType<ConversationAssistantTextDeltaEvent>().Text.ShouldBe("hello ");

        loop.Gate.SetResult();
        var result = await pending;

        result.Succeeded.ShouldBeTrue();
        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeTrue();
        observer.Events[^1].ShouldBeSameAs(terminal);
    }

    [Fact]
    public async Task SendAsync_WhenObservedTurnIsCancelled_EmitsOneCancelledTerminalEventAndPropagatesCancellation()
    {
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.SendAsync("hi", observer, cts.Token));

        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeFalse();
        terminal.Outcome.ShouldBe("cancelled");
        _ = observer.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_WhenTheObserverThrowsDeliveringTheTerminalEvent_StillReturnsTheTurnResult()
    {
        var observer = new RecordingConversationEventObserver { ThrowAfterRecording = true };
        using var session = CreateSession();

        var result = await session.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        _ = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_WhenSessionCannotBeLoadedDuringTheTurn_ReturnsFailureWithoutRunningTheLoop()
    {
        var coordinator = new FakeSessionCoordinator { LoadResult = new SessionLoadFailed("store unavailable") };
        var loop = new FakeAgentLoop();
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>()
            .Text.ShouldContain("could not be loaded");
        loop.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenTheHistorySnapshotCannotBeCaptured_ReturnsFailureWithoutRunningTheLoop()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => new SessionReadFailed("snapshot unavailable"),
        };
        var loop = new FakeAgentLoop();
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>()
            .Text.ShouldContain("snapshot could not be captured");
        loop.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCancellationIsObservedAfterTheLockIsHeld_PropagatesOperationCanceledException()
    {
        // Unlike an already-cancelled token (which SemaphoreSlim.WaitAsync rejects before the try block even
        // starts), this cancels only once the lock is held and a collaborator call is already in flight, so it
        // exercises ReadHistoryAsync's own inner catch rather than the semaphore's own cancellation check.
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var selector = new FakeSecurityProfileSelector();
        using var session = CreateSession(coordinator: coordinator, selector: selector);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        selector.CancelBeforeThrow = cts;

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.ReadHistoryAsync(new SessionSequence(0), 10, cts.Token));

        coordinator.ReadCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task OpenAsync_WhenSessionIsVisible_ReusesAuthoritativeSessionAndBranch()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);

        var opened = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        _ = await session.SendAsync("continue", TestContext.Current.CancellationToken);

        var success = opened.ShouldBeOfType<ConversationSessionOpened>();
        success.SessionId.ShouldBe(sessionId);
        success.BranchId.ShouldBe(coordinator.BranchId);
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.LastAppendRequest.ShouldNotBeNull().Context.SessionId.ShouldBe(sessionId);
    }

    [Fact]
    public async Task OpenAsync_WhenConversationAlreadyRan_RejectsWithoutRebinding()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.SendAsync("first", TestContext.Current.CancellationToken);

        var result = await session.OpenAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ConversationSessionOpenRejected>();
        coordinator.LoadCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task OpenAsync_WhenLoadedOwnerDiffers_RejectsWithoutBindingSession()
    {
        var coordinator = new FakeSessionCoordinator();
        var requested = new SessionId(Guid.NewGuid());
        coordinator.LoadResult = new SessionLoaded(new SessionDescriptor(
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, requested),
            null,
            ConversationSessionOptionsFactory.Identity.TenantId,
            new PrincipalId("another-owner"),
            new SessionStoreKey("test-store"),
            coordinator.BranchId,
            new SessionVersion(1),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty));
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.OpenAsync(requested, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ConversationSessionOpenRejected>();
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.AppendCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenTheRunCompletesWithOutput_SurfacesItAsAnEventAndOnTheResult()
    {
        var output = new ValidatedOutput(OutputMode.Prompted, text: null, json: null, value: new Answer("yes"));
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId,
                new AgentRunCompleted(FakeMessages.Assistant(request, /*lang=json,strict*/ "{\"ok\":\"yes\"}")) { Output = output },
                [FakeMessages.Assistant(request, /*lang=json,strict*/ "{\"ok\":\"yes\"}")],
                new SessionVersion(1)),
        };
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession(loop: loop, configureOptions: o => o.Output = TestOutputDefinition());

        var result = await session.SendAsync("question", observer, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Output.ShouldBeSameAs(output);
        result.Events[^1].ShouldBeOfType<ConversationOutputEvent>().Output.ShouldBeSameAs(output);
        _ = observer.Events.OfType<ConversationOutputEvent>().ShouldHaveSingleItem();
        observer.Events.IndexOf(observer.Events.OfType<ConversationOutputEvent>().Single())
            .ShouldBeLessThan(observer.Events.IndexOf(observer.Events.OfType<ConversationTurnCompletedEvent>().Single()));
    }

    [Fact]
    public async Task SendAsync_WhenAnOutputDefinitionIsConfigured_PassesItToTheLoopWithTheScopedProcessor()
    {
        var loop = new FakeAgentLoop();
        var processor = new NullOutputProcessor();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, loop);
        _ = services.AddScoped<IOutputProcessor>(_ => processor);
        var definition = TestOutputDefinition();
        using var session = CreateSession(
            loopScopeFactory: services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            configureOptions: o => o.Output = definition);

        _ = await session.SendAsync("question", TestContext.Current.CancellationToken);

        loop.LastRequest.ShouldNotBeNull().Output.ShouldBeSameAs(definition);
        loop.LastServices.ShouldNotBeNull().Output.ShouldBeSameAs(processor);
    }

    [Fact]
    public async Task SendAsync_WhenBudgetLimitsAreConfigured_PassesThemToTheLoop()
    {
        var loop = new FakeAgentLoop();
        var limit = new BudgetLimit(BudgetDimensions.Cost, 0.5m, new BudgetUnit("usd"), BudgetLimitKind.Hard);
        using var session = CreateSession(loop: loop, configureOptions: o => o.BudgetLimits.Add(limit));

        _ = await session.SendAsync("question", TestContext.Current.CancellationToken);

        loop.LastRequest.ShouldNotBeNull().BudgetLimits.ShouldBe([limit]);
    }

    [Fact]
    public async Task SendAsync_WhenTheRunSettlesBudgetExhausted_DescribesTheDimensionSafely()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId,
                new AgentRunBudgetExhausted(BudgetDimensions.Cost, "cost cap reached"), [], new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("question", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.OfType<ConversationAssistantTextEvent>().Single().Text.ShouldContain("agentkit.cost");
    }

    [Fact]
    public async Task SendAsync_WhenNoOutputDefinitionIsConfigured_LeavesTheRequestAndServicesOutputNull()
    {
        var loop = new FakeAgentLoop();
        using var session = CreateSession(loop: loop);

        _ = await session.SendAsync("question", TestContext.Current.CancellationToken);

        loop.LastRequest.ShouldNotBeNull().Output.ShouldBeNull();
        loop.LastServices.ShouldNotBeNull().Output.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WhenTheRunCompletesWithoutOutput_LeavesTheResultOutputNull()
    {
        using var session = CreateSession();

        var result = await session.SendAsync("question", TestContext.Current.CancellationToken);

        result.Output.ShouldBeNull();
        result.Events.OfType<ConversationOutputEvent>().ShouldBeEmpty();
    }

    private static OutputDefinition TestOutputDefinition() => new(
        new OutputDefinitionId("answer"), new OutputDefinitionVersion("1"), "answer", OutputMode.Prompted,
        new JsonSchemaDocument("answer", new SchemaVersion("1"), JsonDocument.Parse("""{"type":"object"}""").RootElement),
        typeof(Answer), alternatives: [], validators: [],
        OutputValidationPolicy.RejectOnFirstFailure, new OutputRetryPolicy(1), OutputEndStrategy.Graceful);

    private sealed record Answer(string Ok);

    private sealed class NullOutputProcessor: IOutputProcessor
    {
        public ValueTask<OutputProcessingResult> ProcessAsync(OutputProcessingRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void SessionId_BeforeAnyTurnOrOpen_IsNull()
    {
        using var session = CreateSession();

        session.SessionId.ShouldBeNull();
        session.BranchId.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WhenFirstCalled_LeadsWithTheBindingAndExposesTheCreatedSession()
    {
        var coordinator = new FakeSessionCoordinator();
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.SendAsync("hello", observer, TestContext.Current.CancellationToken);

        var bound = observer.Events[0].ShouldBeOfType<ConversationSessionBoundEvent>();
        bound.SessionId.ShouldBe(coordinator.SessionId);
        bound.BranchId.ShouldBe(coordinator.BranchId);
        result.Events.OfType<ConversationSessionBoundEvent>().ShouldBeEmpty();
        result.SessionId.ShouldBe(coordinator.SessionId);
        _ = result.RunId.ShouldNotBeNull();
        session.SessionId.ShouldBe(coordinator.SessionId);
        session.BranchId.ShouldBe(coordinator.BranchId);
    }

    [Fact]
    public async Task SendAsync_WhenCalledAgain_DoesNotRepeatTheBindingAndAllocatesANewRun()
    {
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession();

        var first = await session.SendAsync("first", observer, TestContext.Current.CancellationToken);
        var second = await session.SendAsync("second", observer, TestContext.Current.CancellationToken);

        _ = observer.Events.OfType<ConversationSessionBoundEvent>().ShouldHaveSingleItem();
        second.SessionId.ShouldBe(first.SessionId);
        second.RunId.ShouldNotBeNull().ShouldNotBe(first.RunId!.Value);
    }

    [Fact]
    public async Task SendAsync_WhenTheSessionWasOpened_AnnouncesTheOpenedIdentityOnce()
    {
        var coordinator = new FakeSessionCoordinator();
        var requested = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);

        var observer = new RecordingConversationEventObserver();
        var opened = await session.OpenAsync(requested, TestContext.Current.CancellationToken);
        var result = await session.SendAsync("continue", observer, TestContext.Current.CancellationToken);

        _ = opened.ShouldBeOfType<ConversationSessionOpened>();
        session.SessionId.ShouldBe(requested);
        observer.Events[0].ShouldBeOfType<ConversationSessionBoundEvent>().SessionId.ShouldBe(requested);
        result.SessionId.ShouldBe(requested);
    }

    [Fact]
    public async Task SendAsync_WhenAdmissionFailsAfterTheSessionExists_StillReportsTheSessionAndRun()
    {
        var coordinator = new FakeSessionCoordinator { AppendResult = new SessionAppendFailed("no capacity") };
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.SendAsync("hello", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.SessionId.ShouldBe(coordinator.SessionId);
        _ = result.RunId.ShouldNotBeNull();
        session.SessionId.ShouldBe(coordinator.SessionId);
    }

    [Fact]
    public async Task SendAsync_WhenTheFirstTurnIsCancelledAfterBinding_DoesNotAnnounceAgainButKeepsTheSessionReadable()
    {
        var coordinator = new FakeSessionCoordinator();
        var observer = new RecordingConversationEventObserver();
        var loop = new FakeAgentLoop { Gate = new TaskCompletionSource(), EnteredSignal = new TaskCompletionSource() };
        using var cancellation = new CancellationTokenSource();
        using var session = CreateSession(coordinator: coordinator, loop: loop);
        var pending = session.SendAsync("first", observer, cancellation.Token);
        await loop.EnteredSignal.Task;
        await cancellation.CancelAsync();
        loop.Gate.SetResult();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        loop.Gate = null;
        var second = await session.SendAsync("second", observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ConversationSessionBoundEvent>().ShouldHaveSingleItem().SessionId.ShouldBe(coordinator.SessionId);
        second.SessionId.ShouldBe(coordinator.SessionId);
        session.SessionId.ShouldBe(coordinator.SessionId);
    }

    [Fact]
    public async Task SendAsync_WhenTheFirstObserverArrivesOnALaterTurn_AnnouncesTheBindingToItOnce()
    {
        var coordinator = new FakeSessionCoordinator();
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession(coordinator: coordinator);

        _ = await session.SendAsync("first", TestContext.Current.CancellationToken);
        _ = await session.SendAsync("second", observer, TestContext.Current.CancellationToken);
        _ = await session.SendAsync("third", observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ConversationSessionBoundEvent>().ShouldHaveSingleItem().SessionId.ShouldBe(coordinator.SessionId);
        _ = observer.Events[0].ShouldBeOfType<ConversationSessionBoundEvent>();
    }

    [Fact]
    public async Task OpenAsync_WhenRejected_LeavesSessionIdNull()
    {
        var coordinator = new FakeSessionCoordinator { LoadResult = new SessionNotFound(new SessionAddress(ConversationSessionOptionsFactory.AgentId, new SessionId(Guid.NewGuid()))) };
        using var session = CreateSession(coordinator: coordinator);

        _ = (await session.OpenAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ConversationSessionOpenRejected>();

        session.SessionId.ShouldBeNull();
        session.BranchId.ShouldBeNull();
    }

    [Fact]
    public async Task OpenAsync_WhenTheCoordinatorCannotLoadTheSession_RejectsWithoutBindingSession()
    {
        var coordinator = new FakeSessionCoordinator { LoadResult = new SessionLoadFailed("store unavailable") };
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.OpenAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ConversationSessionOpenRejected>();
        coordinator.CreateCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorReturnsNotFound_ReturnsUnavailable()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = request => new SessionReadNotFound(request.Context.ToAddress()),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(new SessionSequence(0), 10, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("unavailable");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenAPageContainsAMessageOwnedByAnotherSession_ReturnsUnavailable()
    {
        // The entry's own address matches the request (so IsValidPage's pagination check accepts the page); only
        // the message content embedded inside it names a different session, which is the ownership check's target.
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var mismatchedMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            ConversationSessionOptionsFactory.AgentId,
            new SessionId(Guid.NewGuid()),
            null,
            coordinator.BranchId,
            runId,
            turnId,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("not this session", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var entry = new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId),
            coordinator.BranchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            mismatchedMessage);
        coordinator.ReadResultFactory = _ => new SessionPage([entry], new SessionSequence(1), hasMore: false);
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(new SessionSequence(0), 10, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("ownership");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenThePageReturnsMoreEntriesThanRequested_ReturnsUnavailable()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var first = HistoryEntry(sessionId, coordinator.BranchId, 1, "first");
        var second = HistoryEntry(sessionId, coordinator.BranchId, 2, "second");
        coordinator.ReadResultFactory = _ => new SessionPage([first, second], new SessionSequence(2), hasMore: false);
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(new SessionSequence(0), 1, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("pagination");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenAnEntrysSequenceDoesNotAdvancePastTheCursor_ReturnsUnavailable()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        // The entry's sequence (0) does not advance past the requested cursor (also 0), which real stores never do.
        var stale = HistoryEntry(sessionId, coordinator.BranchId, 0, "stale");
        coordinator.ReadResultFactory = _ => new SessionPage([stale], new SessionSequence(1), hasMore: false);
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(new SessionSequence(0), 10, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("pagination");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenSessionIsNotBound_ReturnsUnavailableWithoutCreatingOrReading()
    {
        var coordinator = new FakeSessionCoordinator();
        var selector = new FakeSecurityProfileSelector();
        using var session = CreateSession(coordinator: coordinator, selector: selector);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("Open");
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.ReadCallCount.ShouldBe(0);
        selector.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenPagesRemain_UsesStableExclusiveCursorAndCompleteness()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var firstMessage = HistoryEntry(sessionId, coordinator.BranchId, 1, "first");
        var secondMessage = HistoryEntry(sessionId, coordinator.BranchId, 2, "second");
        coordinator.ReadResultFactory = request => request.FromSequenceExclusive.Value switch
        {
            0 => new SessionPage([firstMessage], new SessionSequence(1), hasMore: true),
            1 => new SessionPage([secondMessage], new SessionSequence(2), hasMore: false),
            _ => new SessionPage([], request.FromSequenceExclusive, hasMore: false),
        };
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var first = (await session.ReadHistoryAsync(
            new SessionSequence(0),
            1,
            TestContext.Current.CancellationToken)).ShouldBeOfType<ConversationHistoryPage>();
        var second = (await session.ReadHistoryAsync(
            first.NextCursor,
            1,
            TestContext.Current.CancellationToken)).ShouldBeOfType<ConversationHistoryPage>();

        first.Messages.ShouldHaveSingleItem().ShouldBeSameAs(firstMessage.Message);
        first.NextCursor.ShouldBe(new SessionSequence(1));
        first.Complete.ShouldBeFalse();
        second.Messages.ShouldHaveSingleItem().ShouldBeSameAs(secondMessage.Message);
        second.NextCursor.ShouldBe(new SessionSequence(2));
        second.Complete.ShouldBeTrue();
        coordinator.ReadCallCount.ShouldBe(2);
        coordinator.LastReadRequest.ShouldNotBeNull().FromSequenceExclusive.ShouldBe(first.NextCursor);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenPageContainsOperationalEntries_ProjectsOnlyMessagesAndAdvancesCursor()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var message = HistoryEntry(sessionId, coordinator.BranchId, 2, "visible");
        var admissionId = new AdmissionId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var operationalEntry = new InputPromotedSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, new TurnId(Guid.NewGuid())),
            coordinator.BranchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new ExecutionLaneId(Guid.NewGuid()),
            admissionId,
            new SessionSequence(1),
            [admissionId]);
        coordinator.ReadResultFactory = _ => new SessionPage(
            [operationalEntry, message],
            new SessionSequence(2),
            hasMore: false);
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            2,
            TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<ConversationHistoryPage>();
        page.Messages.ShouldHaveSingleItem().ShouldBeSameAs(message.Message);
        page.NextCursor.ShouldBe(new SessionSequence(2));
        page.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCancelled_PropagatesWithoutReading()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await session.ReadHistoryAsync(new SessionSequence(0), 10, cancellation.Token));

        coordinator.ReadCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorReadFails_ReturnsContentSafeUnavailableResult()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => new SessionReadFailed("History storage is temporarily unavailable."),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage
            .ShouldBe("History storage is temporarily unavailable.");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorThrows_DoesNotExposeExceptionContent()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => throw new InvalidOperationException("stored conversation secret"),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        var unavailable = result.ShouldBeOfType<ConversationHistoryUnavailable>();
        unavailable.SafeMessage.ShouldBe("Conversation history is temporarily unavailable.");
        unavailable.SafeMessage.ShouldNotContain("secret");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorReturnsStalledPage_ReturnsUnavailableInsteadOfLooping()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = request => new SessionPage([], request.FromSequenceExclusive, hasMore: true),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("pagination");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenSessionWasReopened_ReadsBoundBranchWithCapturedAuthorizationAndProfile()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var retainedMessage = HistoryEntry(sessionId, coordinator.BranchId, 1, "retained after restart");
        coordinator.ReadResultFactory = _ => new SessionPage(
            [retainedMessage],
            new SessionSequence(1),
            hasMore: false);
        using var session = CreateSession(coordinator: coordinator);

        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryPage>().Messages.ShouldHaveSingleItem()
            .ShouldBeSameAs(retainedMessage.Message);
        var request = coordinator.LastReadRequest.ShouldNotBeNull();
        request.Context.SessionId.ShouldBe(sessionId);
        request.BranchId.ShouldBe(coordinator.BranchId);
        request.Context.Identity.ShouldBe(ConversationSessionOptionsFactory.Identity);
        request.Context.Authorization.Scope.SessionId.ShouldBe(sessionId);
        request.Context.Authorization.Scope.Correlation.ShouldBe(request.Context.Correlation);
        coordinator.LastReadProfile.ShouldBe(TestSecurityEvidence.SessionProfile());
    }

    [Fact]
    public async Task ListAsync_WhenDirectoryReturnsRoutes_ProjectsBoundedSummaries()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        coordinator.ListResult = new SessionDirectoryPage(
            [new SessionLocation(
                new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
                ConversationSessionOptionsFactory.Identity.TenantId,
                new SessionStoreKey("sqlite"),
                new SessionDirectoryRevision(1),
                DateTimeOffset.UnixEpoch,
                new SchemaVersion("1"))],
            sessionId);
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.ListAsync(null, 10, TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<ConversationSessionPage>();
        page.Sessions.ShouldHaveSingleItem().SessionId.ShouldBe(sessionId);
        page.NextCursor.ShouldBe(sessionId);
    }

    [Fact]
    public async Task PresentToolAsync_WhenNoPresenterIsConfigured_ReturnsNull()
    {
        using var session = CreateSession();
        var call = FakeMessages.ToolCall("search", "{}");

        var presentation = await session.PresentToolAsync(call, TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task PresentToolAsync_WhenThePresenterSucceeds_ReturnsItsPresentation()
    {
        var expected = new ToolPresentation([], ToolPresentationDisposition.Fallback);
        var presenter = new ScriptedToolPresenter(_ => ValueTask.FromResult(expected));
        using var session = CreateSession(toolPresenter: presenter);
        var call = FakeMessages.ToolCall("search", "{}");

        var presentation = await session.PresentToolAsync(call, TestContext.Current.CancellationToken);

        presentation.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task PresentToolAsync_WhenThePresenterThrowsCancellation_Propagates()
    {
        var presenter = new ScriptedToolPresenter(static _ => throw new OperationCanceledException());
        using var session = CreateSession(toolPresenter: presenter);
        var call = FakeMessages.ToolCall("search", "{}");

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.PresentToolAsync(call, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PresentToolAsync_WhenThePresenterFaults_ReturnsNullInsteadOfPropagating()
    {
        var presenter = new ScriptedToolPresenter(static _ => throw new InvalidOperationException("boom"));
        using var session = CreateSession(toolPresenter: presenter);
        var call = FakeMessages.ToolCall("search", "{}");

        var presentation = await session.PresentToolAsync(call, TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    private sealed class ScriptedToolPresenter(Func<ToolPresentationRequest, ValueTask<ToolPresentation>> present): IToolPresenter
    {
        public ValueTask<ToolPresentation> PresentAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default) =>
            present(request);
    }

    private static MessageSessionEntry HistoryEntry(
        SessionId sessionId,
        BranchId branchId,
        long sequence,
        string text)
    {
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var message = new UserMessage(
            new MessageId(Guid.NewGuid()),
            ConversationSessionOptionsFactory.AgentId,
            sessionId,
            null,
            branchId,
            runId,
            turnId,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            message);
    }

    private static DefaultConversationSession CreateSession(
        ISessionCoordinator? coordinator = null,
        ISecurityProfileSelector? selector = null,
        IAgentLoop? loop = null,
        IServiceScopeFactory? loopScopeFactory = null,
        Action<ConversationSessionOptions>? configureOptions = null,
        TimeProvider? timeProvider = null,
        ConversationSessionOptions? options = null,
        IToolPresenter? toolPresenter = null,
        ILogger<DefaultConversationSession>? logger = null) =>
        new(
            coordinator ?? new FakeSessionCoordinator(),
            selector ?? new FakeSecurityProfileSelector(),
            loopScopeFactory ?? LoopScopeFactory(loop ?? new FakeAgentLoop()),
            new UnsupportedContextAssembler(),
            new CaptureTestToolInvoker(),
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])),
            ScriptedModelSelector.Selecting(FakeModelDescriptor()),
            new AliasLlmModelResolver(),
            new UnsupportedRunContinuationPolicy(),
            new GuidIdentifierGenerator<RunId>(static guid => new RunId(guid)),
            new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)),
            new GuidIdentifierGenerator<MessageId>(static guid => new MessageId(guid)),
            new GuidIdentifierGenerator<SessionEntryId>(static guid => new SessionEntryId(guid)),
            timeProvider ?? new FakeTimeProvider(),
            Options.Create(options ?? ConversationSessionOptionsFactory.Valid(configureOptions)),
            logger: logger,
            toolPresenter: toolPresenter);

    /// <summary>
    /// Builds a real <see cref="IServiceScopeFactory"/> whose scopes resolve <paramref name="loop"/> as the keyed
    /// <see cref="IAgentLoop"/> <see cref="DefaultConversationSession"/> looks up per turn, so scripted fakes that
    /// record calls (for example <see cref="FakeAgentLoop"/>) keep observing every invocation on the same instance.
    /// </summary>
    private static IServiceScopeFactory LoopScopeFactory(IAgentLoop loop)
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(AgentLoopComponentDefaults.LoopKeyValue, loop);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static (
        ISessionCoordinator SessionCoordinator,
        ISecurityProfileSelector SecurityProfileSelector,
        IServiceScopeFactory LoopScopeFactory,
        IContextAssembler ContextAssembler,
        IToolInvoker ToolInvoker,
        IModelCatalog ModelCatalog,
        IModelSelector ModelSelector,
        ILlmModelResolver LlmModelResolver,
        IRunContinuationPolicy ContinuationPolicy,
        IIdentifierGenerator<RunId> RunIds,
        IIdentifierGenerator<OperationId> OperationIds,
        IIdentifierGenerator<MessageId> MessageIds,
        IIdentifierGenerator<SessionEntryId> SessionEntryIds,
        TimeProvider TimeProvider,
        IOptions<ConversationSessionOptions> Options) ValidArgs() => (
            new FakeSessionCoordinator(),
            new FakeSecurityProfileSelector(),
            LoopScopeFactory(new FakeAgentLoop()),
            new UnsupportedContextAssembler(),
            new CaptureTestToolInvoker(),
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])),
            ScriptedModelSelector.Selecting(FakeModelDescriptor()),
            new AliasLlmModelResolver(),
            new UnsupportedRunContinuationPolicy(),
            new GuidIdentifierGenerator<RunId>(static guid => new RunId(guid)),
            new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)),
            new GuidIdentifierGenerator<MessageId>(static guid => new MessageId(guid)),
            new GuidIdentifierGenerator<SessionEntryId>(static guid => new SessionEntryId(guid)),
            new FakeTimeProvider(),
            Options.Create(ConversationSessionOptionsFactory.Valid()));

    private static ModelDescriptor FakeModelDescriptor()
    {
        var capabilities = new ModelCapabilities(
            supportsSystemInstructions: true,
            supportsStreaming: true,
            supportsToolCalls: true,
            supportsParallelToolCalls: true,
            supportsStructuredOutput: true,
            supportsReasoning: true,
            supportsVisionInput: true,
            ExtensionData.Empty);

        return new ModelDescriptor(
            new ModelAlias("chat"),
            new ProviderId("test-provider"),
            new ApiFamilyId("test-api"),
            new ModelId("test-model"),
            deploymentId: null,
            capabilities,
            new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024),
            pricing: null,
            ExtensionData.Empty);
    }

    private static ToolDescriptor Descriptor(ToolId id)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new ToolDescriptor(
            id,
            new ToolVersion("v1"),
            "display name",
            "Describes a tool.",
            new JsonSchema(
                new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"),
                document.RootElement),
            null,
            new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, []),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("tests"),
            ExtensionData.Empty);
    }

    private static LlmToolDefinition Advertised(ToolId id, string name)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new LlmToolDefinition(id, name, "Describes a tool.", document.RootElement);
    }

    private sealed class NullValueOptions: IOptions<ConversationSessionOptions>
    {
        public ConversationSessionOptions Value => null!;
    }
}
