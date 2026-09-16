// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

/// <summary>Verifies that write previews expose the actual content and target disposition.</summary>
public sealed class WriteFileToolPresentationFormatterTests
{
    [Theory]
    [InlineData("overwrite", "Create or replace the file")]
    [InlineData("create_or_replace", "Create or replace the file")]
    [InlineData("create_new", "Create a new file; fail if it exists")]
    [InlineData("create_only", "Create a new file; fail if it exists")]
    [InlineData("replace_existing", "Replace the existing file; fail if it is missing")]
    [InlineData("append", "Append to the file")]
    public async Task FormatAsync_WhenWriteIsValid_PreservesLiteralContentAndDisposition(string mode, string expectedEffect)
    {
        const string content = "def mean(values):\n    return sum(values) / len(values)\n# <b>literal</b> 😘";
        var arguments = new Dictionary<string, string> { ["path"] = "src/mean.py", ["content"] = content };
        arguments.Add("mode", mode);

        var presentation = await FormatAsync(JsonSerializer.Serialize(arguments));

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Formatted);
        presentation.Parts.Length.ShouldBe(2);
        presentation.Parts[0].Kind.ShouldBe(ToolPresentationPartKind.Text);
        presentation.Parts[0].Text.ShouldBe($"Write: src/mean.py\n{expectedEffect}");
        presentation.Parts[1].Kind.ShouldBe(ToolPresentationPartKind.Code);
        presentation.Parts[1].Text.ShouldBe(content);
        presentation.Parts[1].Language.ShouldBeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.txt\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.txt\",\"content\":\"hello\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.txt\",\"content\":42}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.txt\",\"content\":\"hello\",\"mode\":\"unknown\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.txt\",\"content\":\"hello\",\"mode\":null}")]
    public async Task FormatAsync_WhenArgumentsAreMalformed_DeclinesThePreview(string json)
    {
        var presentation = await FormatAsync(json);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenContentIsEmpty_RetainsAnEmptyContentPart()
    {
        var presentation = await FormatAsync(/*lang=json,strict*/ "{\"path\":\"a.txt\",\"content\":\"\",\"mode\":\"create_or_replace\"}");

        presentation.ShouldNotBeNull().Parts[1].Text.ShouldBeEmpty();
    }

    [Fact]
    public async Task FormatAsync_WhenResultHasPlainTextPart_RendersItAsText()
    {
        var result = ResultWith(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart("Wrote 5 byte(s) to 'a.txt'.", TextSemantics.Plain, ExtensionData.Empty)]);

        var presentation = await FormatResultAsync(result);

        var part = presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem();
        part.Kind.ShouldBe(ToolPresentationPartKind.Text);
        part.Text.ShouldBe("Wrote 5 byte(s) to 'a.txt'.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultHasCodeTextPart_RendersItAsCode()
    {
        var result = ResultWith(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart(/*lang=json,strict*/ "{\"a\":1}", TextSemantics.Code, ExtensionData.Empty)]);

        var presentation = await FormatResultAsync(result);

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Kind.ShouldBe(ToolPresentationPartKind.Code);
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsEmpty_FallsBackToFailureReason()
    {
        var result = ResultWith(
            new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed, false, "Write failed.", ExtensionData.Empty),
            []);

        var presentation = await FormatResultAsync(result);

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Write failed.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsEmptyWithoutFailureReason_FallsBackToSourceStatus()
    {
        var result = ResultWith(
            new ToolCallOutcome(ToolCallOutcomeKind.Rejected, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed, false, null, ExtensionData.Empty),
            []);

        var presentation = await FormatResultAsync(result);

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Denied");
    }

    [Fact]
    public async Task FormatAsync_WhenSourceIsUnrecognized_ReturnsNull()
    {
        var formatter = new WriteFileToolPresentationFormatter();

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new UnsupportedPresentationSource(), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    private static ToolResultPart ResultWith(ToolCallOutcome outcome, ImmutableArray<ContentPart> content) => new(
        new ToolCallId(Guid.NewGuid()),
        new ToolReference(new ToolAlias("write_file"), null, null),
        outcome,
        content,
        new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0),
        ExtensionData.Empty);

    private static ValueTask<ToolPresentation?> FormatResultAsync(ToolResultPart result)
    {
        var formatter = new WriteFileToolPresentationFormatter();
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);
    }

    private static ValueTask<ToolPresentation?> FormatAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new WriteFileToolPresentationFormatter();
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolAlias("write_file"), null, null),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);
    }
}
