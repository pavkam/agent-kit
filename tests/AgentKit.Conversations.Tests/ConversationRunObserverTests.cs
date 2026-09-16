// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies incremental-to-conversation projection and duplicate suppression.</summary>
public sealed class ConversationRunObserverTests
{
    [Fact]
    public async Task OnEventAsync_WhenTextWasStreamed_DoesNotRepeatCompletedPart()
    {
        var observer = new RecordingConversationEventObserver();
        var sut = CreateObserver(observer);
        var requestId = new ModelRequestId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());

        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(
                turnId,
                new ModelPartDelta(requestId, 1, 0, new TextContentDelta("hello"))),
            TestContext.Current.CancellationToken);
        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(
                turnId,
                new ModelPartCompleted(
                    requestId,
                    2,
                    0,
                    new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty))),
            TestContext.Current.CancellationToken);

        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextDeltaEvent>()
            .Text.ShouldBe("hello");
    }

    [Fact]
    public async Task OnEventAsync_WhenProviderBuffersText_ProjectsCompletedPart()
    {
        var observer = new RecordingConversationEventObserver();
        var sut = CreateObserver(observer);
        var requestId = new ModelRequestId(Guid.NewGuid());

        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(
                new TurnId(Guid.NewGuid()),
                new ModelPartCompleted(
                    requestId,
                    1,
                    0,
                    new TextPart("buffered", TextSemantics.Plain, ExtensionData.Empty))),
            TestContext.Current.CancellationToken);

        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>()
            .Text.ShouldBe("buffered");
    }

    [Fact]
    public async Task OnEventAsync_WhenUsageUpdated_DoesNotRepeatUsageAtResponseCompletion()
    {
        var observer = new RecordingConversationEventObserver();
        var sut = CreateObserver(observer);
        var requestId = new ModelRequestId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var usage = new ModelUsage(ModelUsageReportState.Final, 3, 2, null, null, null, null, ExtensionData.Empty);

        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(turnId, new ModelUsageUpdated(requestId, 1, usage)),
            TestContext.Current.CancellationToken);
        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(
                turnId,
                new ModelResponseCompleted(requestId, 2, Response(requestId, usage))),
            TestContext.Current.CancellationToken);

        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationUsageEvent>().Usage.ShouldBe(usage);
    }

    [Fact]
    public async Task OnEventAsync_WhenInterimUsageThenFinalUsageDiffer_ForwardsTheFinalUsage()
    {
        // ModelUsageUpdated is "current best-known"; a later Final report supersedes an Interim one and must not be dropped.
        var observer = new RecordingConversationEventObserver();
        var sut = CreateObserver(observer);
        var requestId = new ModelRequestId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var interim = new ModelUsage(ModelUsageReportState.Interim, 3, 2, null, null, null, null, ExtensionData.Empty);
        var final = new ModelUsage(ModelUsageReportState.Final, 30, 20, null, null, null, null, ExtensionData.Empty);

        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(turnId, new ModelUsageUpdated(requestId, 1, interim)),
            TestContext.Current.CancellationToken);
        await sut.OnEventAsync(
            new AgentRunModelResponseEvent(turnId, new ModelResponseCompleted(requestId, 2, Response(requestId, final))),
            TestContext.Current.CancellationToken);

        observer.Events.OfType<ConversationUsageEvent>().Last().Usage.ShouldBe(final);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnEventAsync_WhenExactBindingExists_PresentsOriginalCallWithCapturedDescriptor(bool omitWireVersion)
    {
        var observer = new RecordingConversationEventObserver();
        var descriptor = Descriptor();
        var advertised = Advertised(descriptor.Id, "read_alias");
        var presenter = new RecordingToolPresenter();
        var bindings = ImmutableDictionary<ToolId, ConversationToolPresentationBinding>.Empty.Add(
            descriptor.Id,
            new ConversationToolPresentationBinding(descriptor, advertised));
        var sut = CreateObserver(observer, presenter, bindings);
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            omitWireVersion
                ? new ToolReference(new ToolAlias(advertised.Name), null, null)
                : new ToolReference(new ToolAlias(advertised.Name), descriptor.Id, descriptor.Version),
            JsonDocument.Parse("{\"path\":\"a.txt\"}").RootElement,
            null,
            ExtensionData.Empty);

        await sut.OnEventAsync(
            new AgentRunToolCallStarted(new TurnId(Guid.NewGuid()), call),
            TestContext.Current.CancellationToken);

        var request = presenter.Requests.ShouldHaveSingleItem();
        request.Descriptor.ShouldBeSameAs(descriptor);
        request.Source.ShouldBeOfType<ToolCallPresentationSource>().Call.ShouldBeSameAs(call);
        call.Tool.Version.ShouldBe(omitWireVersion ? null : descriptor.Version);
        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationToolCallEvent>()
            .Presentation.ShouldBeSameAs(presenter.Presentation);
    }

    [Fact]
    public async Task OnEventAsync_WhenBindingDoesNotMatchVersion_UsesHonestGenericFallback()
    {
        var observer = new RecordingConversationEventObserver();
        var descriptor = Descriptor();
        var advertised = Advertised(descriptor.Id, "read_alias");
        var presenter = new RecordingToolPresenter();
        var sut = CreateObserver(
            observer,
            presenter,
            ImmutableDictionary<ToolId, ConversationToolPresentationBinding>.Empty.Add(
                descriptor.Id,
                new ConversationToolPresentationBinding(descriptor, advertised)));
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolAlias(advertised.Name), descriptor.Id, new ToolVersion("different")),
            JsonDocument.Parse("{}").RootElement,
            null,
            ExtensionData.Empty);

        await sut.OnEventAsync(
            new AgentRunToolCallStarted(new TurnId(Guid.NewGuid()), call),
            TestContext.Current.CancellationToken);

        presenter.Requests.ShouldHaveSingleItem().Descriptor.ShouldBeNull();
        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationToolCallEvent>()
            .Presentation.ShouldBeSameAs(presenter.Presentation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnEventAsync_WhenToolCompletes_PresentsOriginalLossAwareResultProjection(bool omitWireVersion)
    {
        var observer = new RecordingConversationEventObserver();
        var descriptor = Descriptor();
        var advertised = Advertised(descriptor.Id, "read_alias");
        var presenter = new RecordingToolPresenter();
        var sut = CreateObserver(
            observer,
            presenter,
            ImmutableDictionary<ToolId, ConversationToolPresentationBinding>.Empty.Add(
                descriptor.Id,
                new ConversationToolPresentationBinding(descriptor, advertised)));
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            omitWireVersion
                ? new ToolReference(new ToolAlias(advertised.Name), null, null)
                : new ToolReference(new ToolAlias(advertised.Name), descriptor.Id, descriptor.Version),
            JsonDocument.Parse("{}").RootElement,
            null,
            ExtensionData.Empty);
        var result = FakeMessages.ToolSuccess(call, "contents");

        await sut.OnEventAsync(
            new AgentRunToolCallCompleted(new TurnId(Guid.NewGuid()), result),
            TestContext.Current.CancellationToken);

        var request = presenter.Requests.ShouldHaveSingleItem();
        request.Descriptor.ShouldBeSameAs(descriptor);
        request.Source.ShouldBeOfType<ToolResultPresentationSource>().Result.ShouldBeSameAs(result);
        result.Tool.Version.ShouldBe(omitWireVersion ? null : descriptor.Version);
        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationToolResultEvent>()
            .Presentation.ShouldBeSameAs(presenter.Presentation);
    }

    private static ConversationRunObserver CreateObserver(
        IConversationEventObserver observer,
        IToolPresenter? presenter = null,
        ImmutableDictionary<ToolId, ConversationToolPresentationBinding>? bindings = null) =>
        new(
            observer,
            static result => result.Outcome.FailureReason ?? string.Empty,
            presenter,
            bindings ?? []);

    private static ModelResponse Response(ModelRequestId requestId, ModelUsage usage) =>
        new(
            requestId,
            new ProviderResponseIdentity(
                new ProviderId("test"),
                null,
                new ApiFamilyId("test"),
                new ModelId("test"),
                new ModelId("test"),
                null,
                null,
                null),
            [],
            NormalizedStopReason.Completed,
            usage,
            ExtensionData.Empty);

    private static ToolDescriptor Descriptor()
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new ToolDescriptor(
            new ToolId(Guid.NewGuid().ToString()),
            new ToolVersion("v1"),
            "read",
            "Reads a file.",
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
        return new LlmToolDefinition(id, name, "Reads a file.", document.RootElement);
    }

    private sealed class RecordingToolPresenter: IToolPresenter
    {
        internal ToolPresentation Presentation { get; } = new([], ToolPresentationDisposition.Fallback);

        internal List<ToolPresentationRequest> Requests { get; } = [];

        public ValueTask<ToolPresentation> PresentAsync(
            ToolPresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(Presentation);
        }
    }
}
