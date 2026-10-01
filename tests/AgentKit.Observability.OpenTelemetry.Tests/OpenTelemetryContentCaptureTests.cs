// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>Verifies content extraction and fail-closed redaction policy enforcement.</summary>
public sealed class OpenTelemetryContentCaptureTests
{
    [Fact]
    public void Extract_WhenTheEventIsNotAContentDelta_ReturnsNull() =>
        OpenTelemetryContentCapture.Extract(RunEventTestData.Committed(), Snapshot()).ShouldBeNull();

    [Fact]
    public void Extract_WhenTheDeltaIsProviderSpecific_ReturnsNull() =>
        OpenTelemetryContentCapture.Extract(RunEventTestData.Content(new ProviderContentDelta(new ProviderId("vendor"), ExtensionData.Empty)), Snapshot()).ShouldBeNull();

    [Theory]
    [MemberData(nameof(DeltaKinds))]
    public void Extract_WhenTheDeltaCarriesText_MapsItToTheMatchingContentKind(ContentDelta delta, ObservationContentKind expected)
    {
        var content = OpenTelemetryContentCapture.Extract(RunEventTestData.Content(delta), Snapshot()).ShouldNotBeNull();

        content.Kind.ShouldBe(expected);
        content.Classification.ShouldBe(DataClassification.Confidential);
        content.Fingerprint.Value.ShouldStartWith("sha256:");
        content.Value.Length.ShouldBeGreaterThan(0);
    }

    public static TheoryData<ContentDelta, ObservationContentKind> DeltaKinds => new()
    {
        { new TextContentDelta("text"), ObservationContentKind.ModelOutput },
        { new ReasoningContentDelta("thinking", ExtensionData.Empty), ObservationContentKind.Reasoning },
        { new ToolArgumentsContentDelta(new ToolCallId(Guid.NewGuid()), "{}"), ObservationContentKind.ToolArguments },
        { new StructuredDataContentDelta("{}"), ObservationContentKind.ModelOutput },
    };

    [Fact]
    public void Extract_WhenTheTextExceedsTheBound_TruncatesThePayload()
    {
        var content = OpenTelemetryContentCapture.Extract(RunEventTestData.Text(new string('a', 500)), Snapshot(bound: 16)).ShouldNotBeNull();

        content.Value.Length.ShouldBe(16);
    }

    [Fact]
    public void Extract_WhenTheArgumentsAreNull_ThrowsArgumentNullExceptionNamingParameter()
    {
        Should.Throw<ArgumentNullException>(() => OpenTelemetryContentCapture.Extract(null!, Snapshot())).ParamName.ShouldBe("runEvent");
        Should.Throw<ArgumentNullException>(() => OpenTelemetryContentCapture.Extract(RunEventTestData.Committed(), null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task CaptureAsync_WhenTheRedactorReturnsCompliantContent_ReturnsIt()
    {
        var redacted = new ObservationContent(ObservationContentKind.ModelOutput, DataClassification.Public, [1, 2], new ContentFingerprint("f"));
        var redactor = new CallbackObservationRedactor((_, _) => new RedactedContent(redacted));

        var outcome = await OpenTelemetryContentCapture.CaptureAsync(redactor, Snapshot(), Content(), TestContext.Current.CancellationToken);

        outcome.IsOmitted.ShouldBeFalse();
        outcome.Content.ShouldBeSameAs(redacted);
    }

    [Fact]
    public async Task CaptureAsync_WhenTheRedactorChangesTheContentKind_OmitsAsAPolicyViolation()
    {
        var redactor = new CallbackObservationRedactor(static (_, _) => new RedactedContent(
            new ObservationContent(ObservationContentKind.Prompt, DataClassification.Public, [1], new ContentFingerprint("f"))));

        var outcome = await OpenTelemetryContentCapture.CaptureAsync(redactor, Snapshot(), Content(), TestContext.Current.CancellationToken);

        outcome.OmittedReason.ShouldBe("policy_violation");
    }

    [Fact]
    public async Task CaptureAsync_WhenTheCallerCancels_PropagatesInsteadOfOmitting()
    {
        var redactor = new CancellingRedactor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await OpenTelemetryContentCapture.CaptureAsync(redactor, Snapshot(), Content(), cts.Token));
    }

    [Fact]
    public async Task CaptureAsync_WhenTheRedactorCancelsWithoutTheCallerCancelling_OmitsTheContent()
    {
        var outcome = await OpenTelemetryContentCapture.CaptureAsync(
            new CancellingRedactor(), Snapshot(), Content(), TestContext.Current.CancellationToken);

        outcome.OmittedReason.ShouldBe("redaction_failed");
    }

    [Fact]
    public async Task CaptureAsync_WhenAnArgumentIsNull_ThrowsArgumentNullExceptionNamingParameter()
    {
        var redactor = new OmissionOnlyObservationRedactor();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await OpenTelemetryContentCapture.CaptureAsync(null!, Snapshot(), Content(), default))).ParamName.ShouldBe("redactor");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await OpenTelemetryContentCapture.CaptureAsync(redactor, null!, Content(), default))).ParamName.ShouldBe("options");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await OpenTelemetryContentCapture.CaptureAsync(redactor, Snapshot(), null!, default))).ParamName.ShouldBe("content");
    }

    private static ObservationContent Content() =>
        new(ObservationContentKind.ModelOutput, DataClassification.Internal, [9], new ContentFingerprint("sha256:original"));

    private static OpenTelemetryObservationOptionsSnapshot Snapshot(int bound = 1024) =>
        new(
            new ObservationExporterKey("capture-test"),
            new ObservationExporterVersion(1),
            OpenTelemetrySignalSet.Activities,
            new ObservationContentCapturePolicy(true),
            DataClassification.Confidential,
            [DataClassification.Public, DataClassification.Internal, DataClassification.Confidential],
            new ObservationDeliveryPolicy(),
            new ObservationBounds(bound),
            AuditExporterProvidesDurableAcceptance: false);

    private sealed class CancellingRedactor: IObservationRedactor
    {
        public ValueTask<RedactionResult> RedactAsync(ObservationContent content, ObservationPolicy policy, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();
    }
}
