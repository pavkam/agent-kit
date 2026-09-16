// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using System.Text.Json;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="ModelCompactionStrategy"/> behavior and contracts.</summary>
public sealed class ModelCompactionStrategyTests
{
    private const string _customPrompt = "Summarize the covered transcript for a test.";
    private static readonly ModelRequestId _modelRequestId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly MessageId _messageId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Theory]
    [InlineData("modelCatalog")]
    [InlineData("modelSelector")]
    [InlineData("llmModelResolver")]
    [InlineData("estimator")]
    [InlineData("modelRequestIds")]
    [InlineData("messageIds")]
    [InlineData("timeProvider")]
    [InlineData("options")]
    public void Constructor_WhenRequiredDependencyNull_ThrowsArgumentNullException(string parameterName)
    {
        var descriptor = TestFactory.SummaryModel();
        var model = ScriptedModel();
        var exception = Should.Throw<ArgumentNullException>(() => new ModelCompactionStrategy(
            parameterName == "modelCatalog" ? null! : new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [descriptor])),
            parameterName == "modelSelector" ? null! : ScriptedModelSelector.Selecting(descriptor),
            parameterName == "llmModelResolver" ? null! : new AliasLlmModelResolver(model),
            parameterName == "estimator" ? null! : CreateEstimator(),
            parameterName == "modelRequestIds" ? null! : new FixedIdentifierGenerator<ModelRequestId>(_modelRequestId),
            parameterName == "messageIds" ? null! : new FixedIdentifierGenerator<MessageId>(_messageId),
            parameterName == "timeProvider" ? null! : TimeProvider.System,
            parameterName == "options" ? null! : Options.Create(CreateOptions())));

        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Constructor_WhenSummaryModelPolicyNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => Create(options: Options.Create(new CompactionOptions())));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain(nameof(CompactionOptions.SummaryModelPolicy));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSummaryPromptWhitespace_ThrowsArgumentException(string prompt)
    {
        var exception = Should.Throw<ArgumentException>(
            () => Create(options: Options.Create(CreateOptions(o => o.SummaryPrompt = prompt))));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain(nameof(CompactionOptions.SummaryPrompt));
    }

    [Fact]
    public async Task ProduceAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var strategy = Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => strategy.ProduceAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ProduceAsync_WhenTokenAlreadyCancelled_ThrowsBeforeSelectingOrCallingModel()
    {
        var model = ScriptedModel();
        var selector = ScriptedModelSelector.Selecting(TestFactory.SummaryModel());
        var strategy = Create(model: model, selector: selector);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => strategy.ProduceAsync(StrategyRequest("alpha", "beta"), cancellation.Token));

        selector.SelectCount.ShouldBe(0);
        model.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProduceAsync_WhenCutCoversNoEntries_ReturnsUnsupportedWithoutCallingModel()
    {
        var model = ScriptedModel();
        var strategy = Create(model: model);
        var entry = TestFactory.MessageEntry(Address(), _branchId, 1, "alpha");
        var source = Source([entry]);
        var cut = new CompactionCut(new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), []);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionStrategyUnsupported>().Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
        model.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSucceeds_SendsPromptAsSystemMessageAndTranscriptAsSingleUserMessage()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model, configure: o => o.SummaryPrompt = _customPrompt);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha", "beta"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var sent = model.ReceivedRequests.ShouldHaveSingleItem();
        sent.Context.Messages.Length.ShouldBe(2);
        var system = sent.Context.Messages[0].ShouldBeOfType<SystemMessage>();
        ((TextPart) system.Parts.ShouldHaveSingleItem()).Text.ShouldBe(_customPrompt);
        var user = sent.Context.Messages[1].ShouldBeOfType<UserMessage>();
        var transcript = ((TextPart) user.Parts.ShouldHaveSingleItem()).Text;
        transcript.ShouldContain("[user]\nalpha");
        transcript.ShouldContain("[user]\nbeta");
        transcript.IndexOf("alpha", StringComparison.Ordinal).ShouldBeLessThan(transcript.IndexOf("beta", StringComparison.Ordinal));
        sent.Context.Tools.ShouldBeEmpty();
        sent.Context.ToolChoice.ShouldBe(LlmToolChoice.None);
        sent.Context.ModelRequestId.ShouldBe(_modelRequestId);
        sent.Attempt.ShouldBe(1);
    }

    [Fact]
    public async Task ProduceAsync_WhenPromptNotConfigured_SendsEmbeddedDefaultPrompt()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);

        _ = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var system = model.ReceivedRequests.ShouldHaveSingleItem().Context.Messages[0].ShouldBeOfType<SystemMessage>();
        var prompt = ((TextPart) system.Parts[0]).Text;
        prompt.ShouldBe(CompactionPromptResources.DefaultSummaryPrompt);
        prompt.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSucceeds_StampsMessagesWithCompactionIdentitiesAndClock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddMinutes(1));
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model, timeProvider: clock);
        var request = StrategyRequest("alpha");
        var runId = ((InRunOperationCorrelation) request.Request.Context.Correlation).RunId;

        _ = await strategy.ProduceAsync(request, TestContext.Current.CancellationToken);

        foreach (var message in model.ReceivedRequests.Single().Context.Messages)
        {
            message.Id.ShouldBe(_messageId);
            message.AgentId.ShouldBe(_agentId);
            message.SessionId.ShouldBe(_sessionId);
            message.BranchId.ShouldBe(_branchId);
            message.RunId.ShouldBe(runId);
            message.CreatedAt.ShouldBe(clock.GetUtcNow());
            message.State.ShouldBe(MessageState.Complete);
        }
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSucceeds_UsesRequestDeadlineAndSelectsWithinCapturedScope()
    {
        var descriptor = TestFactory.SummaryModel();
        var selector = ScriptedModelSelector.Selecting(descriptor);
        var catalog = new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(7), [descriptor]));
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var policy = new ModelSelectionPolicy([new ModelAlias("summarizer")]);
        var strategy = Create(model: model, selector: selector, catalog: catalog, configure: o => o.SummaryModelPolicy = policy);
        var request = StrategyRequest("alpha");

        _ = await strategy.ProduceAsync(request, TestContext.Current.CancellationToken);

        model.ReceivedRequests.Single().Deadline.ShouldBe(request.Request.Deadline);
        model.ReceivedRequests.Single().Context.Model.ShouldBe(descriptor);
        catalog.ReadCount.ShouldBe(1);
        var selection = selector.LastRequest.ShouldNotBeNull();
        selection.Scope.ShouldBe(request.Request.Context.Authorization.Scope);
        selection.Policy.ShouldBe(policy);
        selection.Requirements.RequiresSystemInstructions.ShouldBeTrue();
        selection.ModelRequestId.ShouldBe(_modelRequestId);
        selection.Catalog.Version.ShouldBe(new ModelCatalogVersion(7));
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSucceeds_ProducesUntrustedPlainTextCheckpointWithModelProvenance()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 30, null, null, null, null, ExtensionData.Empty);
        var attempt = TestFactory.CompletedAttempt(
            _modelRequestId, [new TextPart("the summary", TextSemantics.Markdown, ExtensionData.Empty)], usage: usage, responseId: "resp-1");
        var model = ScriptedModel(attempt);
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha", "beta"), TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var part = produced.Checkpoint.Summary.ShouldHaveSingleItem().ShouldBeOfType<TextPart>();
        part.Text.ShouldBe("the summary");
        part.Semantics.ShouldBe(TextSemantics.Plain);
        produced.Producer.StrategyKey.ShouldBe(ModelCompactionStrategy.StrategyKey);
        produced.Producer.Deterministic.ShouldBeFalse();
        var provenance = produced.Producer.Extensions.Values;
        JsonString(provenance, ModelCompactionProvenanceKeys.ProducerKind).ShouldBe("model-backed");
        JsonString(provenance, ModelCompactionProvenanceKeys.ModelAlias).ShouldBe("summarizer");
        JsonString(provenance, ModelCompactionProvenanceKeys.ProviderId).ShouldBe("test-provider");
        JsonString(provenance, ModelCompactionProvenanceKeys.ModelId).ShouldBe("test-summary-model-2024");
        JsonString(provenance, ModelCompactionProvenanceKeys.ModelRequestId).ShouldBe(_modelRequestId.ToString());
        JsonString(provenance, ModelCompactionProvenanceKeys.ProviderResponseId).ShouldBe("resp-1");
        JsonNumber(provenance, ModelCompactionProvenanceKeys.InputTokens).ShouldBe(120);
        JsonNumber(provenance, ModelCompactionProvenanceKeys.OutputTokens).ShouldBe(30);
        JsonBoolean(provenance, ModelCompactionProvenanceKeys.InputTruncated).ShouldBeFalse();
        JsonBoolean(provenance, ModelCompactionProvenanceKeys.OutputTruncated).ShouldBeFalse();
        JsonNumber(provenance, ModelCompactionProvenanceKeys.InputCharacters)
            .ShouldBe(((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text.Length);
        produced.After.ShouldBe(CreateEstimator().EstimateCheckpoint(produced.Checkpoint));
    }

    [Fact]
    public async Task ProduceAsync_WhenUsageNotReported_OmitsTokenProvenanceRatherThanReportingZero()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var provenance = result.ShouldBeOfType<CompactionCheckpointProduced>().Producer.Extensions.Values;
        provenance.ShouldNotContainKey(ModelCompactionProvenanceKeys.InputTokens);
        provenance.ShouldNotContainKey(ModelCompactionProvenanceKeys.OutputTokens);
        provenance.ShouldNotContainKey(ModelCompactionProvenanceKeys.ProviderResponseId);
    }

    [Fact]
    public async Task ProduceAsync_WhenTranscriptExceedsMaximumSummaryInputCharacters_BoundsItAndRecordsTruncation()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model, configure: o => o.MaximumSummaryInputCharacters = 100);
        var head = new string('h', 300);
        var tail = new string('t', 300);

        var result = await strategy.ProduceAsync(StrategyRequest(head, tail), TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.Length.ShouldBeLessThanOrEqualTo(100);
        transcript.ShouldContain(ModelCompactionStrategy.TruncationMarker);
        transcript.ShouldStartWith("[user]\nhhh");
        transcript.ShouldEndWith("ttt");
        var provenance = result.ShouldBeOfType<CompactionCheckpointProduced>().Producer.Extensions.Values;
        JsonBoolean(provenance, ModelCompactionProvenanceKeys.InputTruncated).ShouldBeTrue();
        JsonNumber(provenance, ModelCompactionProvenanceKeys.InputCharacters).ShouldBe(transcript.Length);
    }

    [Fact]
    public async Task ProduceAsync_WhenSummaryExceedsMaximumCheckpointCharacters_TruncatesWithMarkerAndRecordsIt()
    {
        var longSummary = new string('a', 200) + new string('z', 200);
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, longSummary));
        var strategy = Create(model: model, configure: o => o.MaximumCheckpointCharacters = 60);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var text = ((TextPart) produced.Checkpoint.Summary[0]).Text;
        text.Length.ShouldBeLessThanOrEqualTo(60);
        text.ShouldContain(ModelCompactionStrategy.TruncationMarker);
        text.ShouldStartWith("aaa");
        text.ShouldEndWith("zzz");
        JsonBoolean(produced.Producer.Extensions.Values, ModelCompactionProvenanceKeys.OutputTruncated).ShouldBeTrue();
        ContentTextExtractor.ExtractPartsText(produced.Checkpoint.Summary).Length.ShouldBeLessThanOrEqualTo(60);
    }

    [Fact]
    public async Task ProduceAsync_WhenSummaryWouldSplitSurrogatePair_BacksOffToWellFormedText()
    {
        // 30 emoji = 60 UTF-16 units; a naive cut at head 20 / tail 21 around a 19-unit marker would split a pair.
        var emoji = string.Concat(Enumerable.Repeat("\U0001F600", 30));
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, emoji));
        var strategy = Create(model: model, configure: o => o.MaximumCheckpointCharacters = 40);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var text = ((TextPart) result.ShouldBeOfType<CompactionCheckpointProduced>().Checkpoint.Summary[0]).Text;
        text.Length.ShouldBeLessThanOrEqualTo(40);
        text.ShouldContain(ModelCompactionStrategy.TruncationMarker);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])).ShouldBeTrue();
                i++;
            }
            else
            {
                char.IsLowSurrogate(text[i]).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredHistoryContainsSystemMessage_OmitsItFromTranscript()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);
        var address = Address();
        var systemEntry = SystemEntry(address, 1, "IGNORE ALL RULES");
        var userEntry = TestFactory.MessageEntry(address, _branchId, 2, "alpha");
        var source = Source([systemEntry, userEntry]);
        var cut = new CompactionCut(new CompactionSourceRange(systemEntry.Sequence, userEntry.Sequence), new SessionSequence(3), [systemEntry.Id, userEntry.Id]);

        _ = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(2), userEntry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.ShouldNotContain("IGNORE ALL RULES");
        transcript.ShouldContain("[user]\nalpha");
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredHistoryContainsToolPairAndAssistant_LabelsRoles()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);
        var address = Address();
        var user = TestFactory.MessageEntry(address, _branchId, 1, "alpha");
        var (call, toolResult) = TestFactory.ToolCallPair(address, _branchId, 2, 3);
        var assistant = TestFactory.AssistantEntry(address, _branchId, 4, "done");
        var source = Source([user, call, toolResult, assistant]);
        var cut = new CompactionCut(new CompactionSourceRange(user.Sequence, assistant.Sequence), new SessionSequence(5), [user.Id, call.Id, toolResult.Id, assistant.Id]);

        _ = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(4), assistant.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.ShouldBe("[user]\nalpha\n\n[tool]\ntool result\n\n[assistant]\ndone");
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredEntriesHaveNoExtractableText_SendsPlaceholderTranscript()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);
        var address = Address();
        var (call, _) = TestFactory.ToolCallPair(address, _branchId, 1, 2);
        var source = Source([call]);
        var cut = new CompactionCut(new CompactionSourceRange(call.Sequence, call.Sequence), new SessionSequence(2), [call.Id]);

        _ = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), call.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.ShouldBe("(no extractable text in covered entries)");
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredHistoryContainsRuntimeMessage_LabelsItRuntime()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);
        var address = Address();
        var userEntry = TestFactory.MessageEntry(address, _branchId, 1, "alpha");
        var runtimeEntry = RuntimeEntry(address, 2, "interrupted");
        var source = Source([userEntry, runtimeEntry]);
        var cut = new CompactionCut(new CompactionSourceRange(userEntry.Sequence, runtimeEntry.Sequence), new SessionSequence(3), [userEntry.Id, runtimeEntry.Id]);

        _ = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(2), runtimeEntry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.ShouldContain("[runtime]\ninterrupted");
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredHistoryContainsAnEarlierCompactionEntry_LabelsItEarlierSummary()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);
        var address = Address();
        var earlierSummaryEntry = CompactionEntry(address, 1, "previously summarized content");
        var userEntry = TestFactory.MessageEntry(address, _branchId, 2, "alpha");
        var source = Source([earlierSummaryEntry, userEntry]);
        var cut = new CompactionCut(new CompactionSourceRange(earlierSummaryEntry.Sequence, userEntry.Sequence), new SessionSequence(3), [earlierSummaryEntry.Id, userEntry.Id]);

        _ = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(2), userEntry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var transcript = ((TextPart) model.ReceivedRequests.Single().Context.Messages[1].Parts[0]).Text;
        transcript.ShouldContain("[earlier summary]\nprevious", Case.Insensitive);
    }

    [Fact]
    public async Task ProduceAsync_WhenProviderStopReasonIsNoneOfTheHandledValues_ReportsItInTheFailureReason()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "unused", NormalizedStopReason.Error));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionStrategyFailed>();
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("stop reason");
        failed.Failure.SafeMessage.ShouldContain(nameof(NormalizedStopReason.Error));
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSelectionReportsNoCompatibleModel_ReturnsUnsupportedWithoutCallingModel()
    {
        var model = ScriptedModel();
        var selector = new ScriptedModelSelector(new NoCompatibleModel(
            new ModelRequirements { RequiresSystemInstructions = true },
            [new ModelSelectionDiagnostic(new ModelAlias("summarizer"), ModelCandidateOutcome.MissingRequiredCapability, "no system instructions")]));
        var logger = new RecordingLogger<ModelCompactionStrategy>();
        var strategy = Create(model: model, selector: selector, logger: logger);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionStrategyUnsupported>().Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        model.ReceivedRequests.ShouldBeEmpty();
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 9005 && entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ProduceAsync_WhenModelSelectionReportsInvalidPolicy_ReturnsUnsupported()
    {
        var selector = new ScriptedModelSelector(new InvalidModelPolicy("duplicate alias"));
        var strategy = Create(selector: selector);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CompactionStrategyUnsupported>();
        unsupported.Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        unsupported.Rejection.SafeMessage.ShouldContain("duplicate alias");
    }

    [Fact]
    public async Task ProduceAsync_WhenNoAdapterIsRegisteredForSelectedModel_ReturnsUnsupported()
    {
        var strategy = Create(resolver: new AliasLlmModelResolver());

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CompactionStrategyUnsupported>();
        unsupported.Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        unsupported.Rejection.SafeMessage.ShouldContain("summarizer");
    }

    [Theory]
    [InlineData(ProviderFailureKind.Throttling, true)]
    [InlineData(ProviderFailureKind.Unavailable, true)]
    [InlineData(ProviderFailureKind.Timeout, true)]
    [InlineData(ProviderFailureKind.Authentication, false)]
    [InlineData(ProviderFailureKind.InvalidRequest, false)]
    [InlineData(ProviderFailureKind.ProtocolViolation, false)]
    public async Task ProduceAsync_WhenProviderAttemptFails_ReturnsTypedStrategyFailure(ProviderFailureKind kind, bool retryable)
    {
        var logger = new RecordingLogger<ModelCompactionStrategy>();
        var model = ScriptedModel(TestFactory.FailedAttempt(kind));
        var strategy = Create(model: model, logger: logger);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionStrategyFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.StrategyFailure);
        failed.Failure.Retryable.ShouldBe(retryable);
        failed.Failure.SafeMessage.ShouldContain(kind.ToString());
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 9008 && entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ProduceAsync_WhenProviderStoppedForLength_ReturnsNonRetryableFailure()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "partial summ", NormalizedStopReason.Length));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionStrategyFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.StrategyFailure);
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("length");
    }

    [Fact]
    public async Task ProduceAsync_WhenModelRequestsTool_ReturnsNonRetryableFailure()
    {
        var toolCall = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("t"), null, null), default, null, ExtensionData.Empty);
        var model = ScriptedModel(TestFactory.CompletedAttempt(_modelRequestId, [toolCall], NormalizedStopReason.ToolUse));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionStrategyFailed>();
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("tool");
    }

    [Fact]
    public async Task ProduceAsync_WhenModelReturnsNoText_ReturnsNonRetryableFailure()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "   "));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionStrategyFailed>();
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("no summary text");
    }

    [Fact]
    public async Task ProduceAsync_WhenCallerCancelsDuringModelCall_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger<ModelCompactionStrategy>();
        var model = ScriptedModel();
        model.ExecuteOverride = async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("unreachable");
        };
        var strategy = Create(model: model, logger: logger);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => strategy.ProduceAsync(StrategyRequest("alpha"), cancellation.Token));

        _ = model.ReceivedRequests.ShouldHaveSingleItem();
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 9009);
    }

    [Fact]
    public async Task ProduceAsync_WhenAdapterReportsCancelledAndCallerTokenIsCancelled_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var model = ScriptedModel();
        model.ExecuteOverride = async (_, _) =>
        {
            await cancellation.CancelAsync();
            return new ModelAttemptCancelled(
                new ProviderFailure(ProviderFailureKind.Cancellation, new ProviderId("test-provider"), null, null, null, null, "cancelled", null, ExtensionData.Empty),
                [],
                null);
        };
        var strategy = Create(model: model);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => strategy.ProduceAsync(StrategyRequest("alpha"), cancellation.Token));
    }

    [Fact]
    public async Task ProduceAsync_WhenAdapterReportsCancelledWithoutCallerCancellation_ReturnsNonRetryableFailure()
    {
        var model = ScriptedModel(new ModelAttemptCancelled(
            new ProviderFailure(ProviderFailureKind.Cancellation, new ProviderId("test-provider"), null, null, null, null, "deadline", null, ExtensionData.Empty),
            [],
            null));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionStrategyFailed>().Failure.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task ProduceAsync_WhenAdapterThrows_PropagatesException()
    {
        var model = ScriptedModel();
        model.ExecuteOverride = (_, _) => throw new InvalidOperationException("transport bug");
        var strategy = Create(model: model);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("transport bug");
    }

    [Fact]
    public async Task ProduceAsync_WhenObserved_EmitsCorrelatedChatActivityAndLogsWithoutContent()
    {
        const string protectedTranscript = "never-export-transcript-content";
        const string protectedSummary = "never-export-summary-content";
        const string protectedPrompt = "never-export-prompt-content";
        var logger = new RecordingLogger<ModelCompactionStrategy>();
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, protectedSummary));
        var strategy = Create(model: model, logger: logger, configure: o => o.SummaryPrompt = protectedPrompt);
        var request = StrategyRequest(protectedTranscript);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.Chat
                && Equals(activity.GetTagItem(AgentKitTagNames.CompactionId), request.Request.Context.CompactionId.ToString()));

        _ = (await strategy.ProduceAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionCheckpointProduced>();

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ModelRequestId).ShouldBe(_modelRequestId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(_sessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RequestModel).ShouldBe("test-summary-model");
        activity.GetTagItem(AgentKitTagNames.ProviderName).ShouldBe("test-provider");
        var tagValues = activity.Tags.Values.Select(static value => value?.ToString() ?? string.Empty).ToArray();
        tagValues.ShouldNotContain(static value => value.Contains(protectedTranscript, StringComparison.Ordinal));
        tagValues.ShouldNotContain(static value => value.Contains(protectedSummary, StringComparison.Ordinal));
        tagValues.ShouldNotContain(static value => value.Contains(protectedPrompt, StringComparison.Ordinal));

        var entries = logger.Snapshot();
        entries.Select(static entry => entry.EventId.Id).ShouldBe([9006, 9007]);
        entries.ShouldAllBe(static entry => entry.Category == typeof(ModelCompactionStrategy).FullName);
        foreach (var entry in entries)
        {
            entry.Message.ShouldNotContain(protectedTranscript);
            entry.Message.ShouldNotContain(protectedSummary);
            entry.Message.ShouldNotContain(protectedPrompt);
            entry.State.Values.Select(static value => value?.ToString() ?? string.Empty)
                .ShouldNotContain(static value => value.Contains("never-export", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ProduceAsync_WhenProviderFails_EmitsErrorChatActivity()
    {
        var model = ScriptedModel(TestFactory.FailedAttempt(ProviderFailureKind.Unavailable));
        var strategy = Create(model: model);
        var request = StrategyRequest("alpha");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.Chat
                && Equals(activity.GetTagItem(AgentKitTagNames.CompactionId), request.Request.Context.CompactionId.ToString()));

        _ = (await strategy.ProduceAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionStrategyFailed>();

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task ProduceAsync_WhenNoListenerIsAttached_BehavesIdentically()
    {
        var model = ScriptedModel(TestFactory.CompletedTextAttempt(_modelRequestId, "the summary"));
        var strategy = Create(model: model);

        var result = await strategy.ProduceAsync(StrategyRequest("alpha"), TestContext.Current.CancellationToken);

        ((TextPart) result.ShouldBeOfType<CompactionCheckpointProduced>().Checkpoint.Summary[0]).Text.ShouldBe("the summary");
    }

    private SessionAddress Address() => new(_agentId, _sessionId);

    private CompactionSourceSnapshot Source(ImmutableArray<SessionEntry> entries) => new(
        TestFactory.CompactionContext(_agentId, _sessionId),
        _branchId,
        new SessionVersion(entries.Length),
        entries.IsEmpty ? new SessionSequence(0) : entries[^1].Sequence,
        entries);

    /// <summary>Builds a request covering one user entry per text, in order, with nothing retained.</summary>
    private CompactionStrategyRequest StrategyRequest(params string[] texts)
    {
        var address = Address();
        var entries = texts.Select((text, index) => (SessionEntry) TestFactory.MessageEntry(address, _branchId, index + 1, text)).ToImmutableArray();
        var source = Source(entries);
        var cut = new CompactionCut(
            new CompactionSourceRange(entries[0].Sequence, entries[^1].Sequence),
            new SessionSequence(entries.Length + 1),
            [.. entries.Select(static e => e.Id)]);
        return new CompactionStrategyRequest(
            TestFactory.Request(source.Context, _branchId, source.Version, entries[^1].Sequence), source, cut);
    }

    private MessageSessionEntry SystemEntry(SessionAddress address, long sequence, string text) => new(
        new SessionEntryId(Guid.NewGuid()),
        address,
        TestFactory.Correlation(),
        _branchId,
        new SessionSequence(sequence),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        new SystemMessage(
            new MessageId(Guid.NewGuid()),
            address.AgentId,
            address.SessionId,
            null,
            _branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty));

    private MessageSessionEntry RuntimeEntry(SessionAddress address, long sequence, string text) => new(
        new SessionEntryId(Guid.NewGuid()),
        address,
        TestFactory.Correlation(),
        _branchId,
        new SessionSequence(sequence),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        new RuntimeMessage(
            new MessageId(Guid.NewGuid()),
            address.AgentId,
            address.SessionId,
            null,
            _branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty));

    private CompactionSessionEntry CompactionEntry(SessionAddress address, long sequence, string summaryText)
    {
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.NewGuid()),
            context,
            _branchId,
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(1), new SessionSequence(1)),
            new SessionSequence(sequence),
            new CompactionProducer(new CompactionStrategyKey("test"), true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(1, 1, 1),
            new CompactionSizeEstimate(1, 1, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var checkpoint = new CompactionCheckpoint(
            [new TextPart(summaryText, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var record = new CompactionRecord(
            context,
            new SessionVersion(1),
            new SessionVersion(sequence),
            CompactionRecordStatus.Active,
            manifest,
            checkpoint,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        return new CompactionSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            TestFactory.Correlation(),
            _branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            record);
    }

    private static ScriptedLlmModel ScriptedModel(params ModelAttemptResult[] results) => new(new ModelAlias("summarizer"), results);

    private static CompactionOptions CreateOptions(Action<CompactionOptions>? configure = null)
    {
        var options = new CompactionOptions { SummaryModelPolicy = new ModelSelectionPolicy([new ModelAlias("summarizer")]) };
        configure?.Invoke(options);
        return options;
    }

    private static CharacterCompactionSizeEstimator CreateEstimator() => new(Options.Create(new CompactionOptions()));

    private static ModelCompactionStrategy Create(
        ScriptedLlmModel? model = null,
        IModelCatalog? catalog = null,
        IModelSelector? selector = null,
        ILlmModelResolver? resolver = null,
        ICompactionSizeEstimator? estimator = null,
        IIdentifierGenerator<ModelRequestId>? modelRequestIds = null,
        IIdentifierGenerator<MessageId>? messageIds = null,
        TimeProvider? timeProvider = null,
        IOptions<CompactionOptions>? options = null,
        ILogger<ModelCompactionStrategy>? logger = null,
        Action<CompactionOptions>? configure = null)
    {
        var descriptor = TestFactory.SummaryModel();
        model ??= ScriptedModel();
        return new ModelCompactionStrategy(
            catalog ?? new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [descriptor])),
            selector ?? ScriptedModelSelector.Selecting(descriptor),
            resolver ?? new AliasLlmModelResolver(model),
            estimator ?? CreateEstimator(),
            modelRequestIds ?? new FixedIdentifierGenerator<ModelRequestId>(_modelRequestId),
            messageIds ?? new FixedIdentifierGenerator<MessageId>(_messageId),
            timeProvider ?? new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddMinutes(1)),
            options ?? Options.Create(CreateOptions(configure)),
            logger);
    }

    private static string JsonString(ImmutableDictionary<string, ExtensionValue> values, string key) =>
        JsonSerializer.Deserialize<string>(values[key].CanonicalJson.AsSpan()).ShouldNotBeNull();

    private static long JsonNumber(ImmutableDictionary<string, ExtensionValue> values, string key) =>
        JsonSerializer.Deserialize<long>(values[key].CanonicalJson.AsSpan());

    private static bool JsonBoolean(ImmutableDictionary<string, ExtensionValue> values, string key) =>
        JsonSerializer.Deserialize<bool>(values[key].CanonicalJson.AsSpan());
}
