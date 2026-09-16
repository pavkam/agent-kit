// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolPresenterTests
{
    [Fact]
    public async Task PresentAsync_WhenNoExactFormatterExists_ReturnsBoundedFallback()
    {
        var request = new ToolPresentationRequest(null, new ToolCallPresentationSource(Call("custom", /*lang=json,strict*/ "{\"value\":\"abcdef\"}")), new ToolPresentationBounds(1024, 8, 1));

        var result = await new ToolPresenter([]).PresentAsync(request, TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        result.Parts.ShouldHaveSingleItem().Text.Length.ShouldBe(8);
        result.OmittedCharacters.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WhenExactFormatterIdentityIsDuplicated_RejectsAmbiguity()
    {
        var descriptor = Descriptor("custom");

        _ = Should.Throw<ArgumentException>(() => new ToolPresenter([new StubFormatter(descriptor), new StubFormatter(descriptor)]));
    }

    [Fact]
    public async Task PresentAsync_WhenCapturedDescriptorDiffersFromFormatter_UsesFallback()
    {
        var registered = Descriptor("custom");
        var changed = Descriptor("custom", "Changed captured contract");
        var presenter = new ToolPresenter([new StubFormatter(registered)]);

        var result = await presenter.PresentAsync(new ToolPresentationRequest(changed, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task PresentAsync_WhenCustomExactFormatterIsCaptured_UsesItsReplacementPresentation()
    {
        var descriptor = Descriptor("custom");
        var presenter = new ToolPresenter([new StubFormatter(descriptor)]);

        var result = await presenter.PresentAsync(
            new ToolPresentationRequest(descriptor, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Formatted);
        result.Parts.ShouldHaveSingleItem().Text.ShouldBe("formatted");
    }

    [Fact]
    public async Task PresentAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = async () => await new ToolPresenter([]).PresentAsync(
            new ToolPresentationRequest(null, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PresentAsync_WhenProjectedArgumentsAreUndefined_ReturnsExplicitFallback()
    {
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("custom"), null, null), default, null, ExtensionData.Empty);

        var result = await new ToolPresenter([]).PresentAsync(
            new ToolPresentationRequest(null, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        result.Parts.ShouldHaveSingleItem().Text.ShouldContain("malformed");
    }

    [Fact]
    public async Task PresentAsync_WhenFormatterDescriptorChangesAfterCapture_UsesFrozenDescriptorWithoutRereadingMetadata()
    {
        var descriptor = Descriptor("custom");
        var formatter = new MutableDescriptorFormatter(descriptor, Descriptor("custom", sourceId: "changed"));
        var presenter = new ToolPresenter([formatter]);

        var result = await presenter.PresentAsync(new ToolPresentationRequest(descriptor, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Formatted);
        formatter.DescriptorReads.ShouldBe(1);
    }

    [Fact]
    public async Task PresentAsync_WhenSameIdentityHasDistinctSources_CapturesBothAndSelectsExactSource()
    {
        var first = Descriptor("custom", sourceId: "first");
        var second = Descriptor("custom", sourceId: "second");
        var presenter = new ToolPresenter([new StubFormatter(first, "first"), new StubFormatter(second, "second")]);

        var result = await presenter.PresentAsync(new ToolPresentationRequest(second, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Parts.ShouldHaveSingleItem().Text.ShouldBe("second");
    }

    [Fact]
    public async Task PresentAsync_WhenSourceDoesNotMatchExactRegistration_UsesFallback()
    {
        var registered = Descriptor("custom", sourceId: "first");
        var requested = Descriptor("custom", sourceId: "second");

        var result = await new ToolPresenter([new StubFormatter(registered)]).PresentAsync(
            new ToolPresentationRequest(requested, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task PresentAsync_WhenResultContainsStructuredAndMediaContent_OmitsTheirPayloadsGenerically()
    {
        using var data = JsonDocument.Parse("{\"secret_like_payload\":\"must-not-render\"}");
        var media = new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1, 2, 3], null, null, ExtensionData.Empty);
        var resultPart = Result([
            new StructuredDataPart(data.RootElement, null, ExtensionData.Empty),
            new MediaReferencePart(media, MediaSemantics.Output, ExtensionData.Empty),
        ]);

        var result = await new ToolPresenter([]).PresentAsync(new ToolPresentationRequest(null, new ToolResultPresentationSource(resultPart), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        var text = result.Parts.ShouldHaveSingleItem().Text;
        text.ShouldContain("structured content omitted");
        text.ShouldContain("media content omitted");
        text.ShouldNotContain("secret_like_payload");
        text.ShouldNotContain("AQID");
    }

    [Fact]
    public async Task PresentAsync_WhenMetadataAndUnicodeExceedOutputBound_KeepsValidUnicodeAndBoundsEveryField()
    {
        var descriptor = Descriptor("custom");
        var formatter = new StubFormatter(descriptor, "😀", "language-too-long");

        var result = await new ToolPresenter([formatter]).PresentAsync(
            new ToolPresentationRequest(descriptor, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds(1024, 2, 1)), TestContext.Current.CancellationToken);

        var part = result.Parts.ShouldHaveSingleItem();
        part.Text.ShouldBe("😀");
        part.Language.ShouldBeNull();
        part.Text.EnumerateRunes().ShouldHaveSingleItem().Value.ShouldBe(0x1F600);
        result.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
    }

    [Fact]
    public async Task PresentAsync_WhenFormatterThrowsArbitraryFault_ReturnsFallback()
    {
        var descriptor = Descriptor("custom");

        var result = await new ToolPresenter([new FaultingFormatter(descriptor)]).PresentAsync(
            new ToolPresentationRequest(descriptor, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task PresentAsync_WhenFormatterAlreadyOmittedContent_RetainsItsOmittedCount()
    {
        var descriptor = Descriptor("custom");
        var formatter = new StubFormatter(descriptor, "ok", omittedCharacters: 9);

        var result = await new ToolPresenter([formatter]).PresentAsync(
            new ToolPresentationRequest(descriptor, new ToolCallPresentationSource(Call("custom", "{}")), new ToolPresentationBounds()), TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        result.OmittedCharacters.ShouldBe(9);
    }

    private static ToolCallPart Call(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        return new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias(id), new ToolId(id), new ToolVersion("1.0")), document.RootElement.Clone(), null, ExtensionData.Empty);
    }

    private static ToolResultPart Result(ImmutableArray<ContentPart> content) => new(
        new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("custom"), new ToolId("custom"), new ToolVersion("1.0")),
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), content,
        new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

    private static ToolDescriptor Descriptor(string id, string description = "Test descriptor", string sourceId = "tests")
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return new ToolDescriptor(new ToolId(id), new ToolVersion("1.0"), id, description, new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement), null,
            new ToolEffects(ToolEffect.ReadOnly, null, null), new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId(sourceId), ExtensionData.Empty);
    }

    private sealed class StubFormatter(ToolDescriptor descriptor, string text = "formatted", string? language = null, long omittedCharacters = 0): IToolPresentationFormatter
    {
        public ToolDescriptor Descriptor { get; } = descriptor;
        public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ToolPresentation?>(new ToolPresentation(
                [new ToolPresentationPart(language is null ? ToolPresentationPartKind.Text : ToolPresentationPartKind.Code, text, language)],
                omittedCharacters > 0 ? ToolPresentationDisposition.Truncated : ToolPresentationDisposition.Formatted,
                omittedCharacters));
    }

    private sealed class MutableDescriptorFormatter(ToolDescriptor first, ToolDescriptor subsequent): IToolPresentationFormatter
    {
        public int DescriptorReads { get; private set; }
        public ToolDescriptor Descriptor => ++DescriptorReads == 1 ? first : subsequent;
        public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ToolPresentation?>(new ToolPresentation([new ToolPresentationPart(ToolPresentationPartKind.Text, "formatted")], ToolPresentationDisposition.Formatted));
    }

    private sealed class FaultingFormatter(ToolDescriptor descriptor): IToolPresentationFormatter
    {
        public ToolDescriptor Descriptor { get; } = descriptor;
        public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidDataException("Formatter fault.");
    }
}
