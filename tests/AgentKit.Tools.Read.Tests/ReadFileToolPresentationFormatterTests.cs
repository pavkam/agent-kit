// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>Verifies literal file-content presentation without inferring JSON from code semantics.</summary>
public sealed class ReadFileToolPresentationFormatterTests
{
    [Theory]
    [InlineData(TextSemantics.Plain)]
    [InlineData(TextSemantics.Code)]
    [InlineData(TextSemantics.Markdown)]
    public async Task FormatAsync_WhenFileContentsAreReturned_PreservesLiteralLinesWithoutAssumingJson(TextSemantics semantics)
    {
        const string contents = "# heading\n\n    <b>literal</b>\nreturn value;\n";
        var formatter = new ReadFileToolPresentationFormatter();
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(ReadFileTool.Id, null, "read_file"),
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded,
                SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart(contents, semantics, ExtensionData.Empty)],
            ExtensionData.Empty);

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        var part = presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem();
        part.Kind.ShouldBe(ToolPresentationPartKind.Code);
        part.Text.ShouldBe(contents);
        part.Language.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenCallProjectionIsValid_ProducesFeatureAuthoredPreview()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { path = "src/a.cs" }));
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(ReadFileTool.Id, new ToolVersion("1.0"), "read_file"),
            document.RootElement,
            null,
            ExtensionData.Empty);

        var presentation = await new ReadFileToolPresentationFormatter().FormatAsync(
            new ToolPresentationRequest(ReadFileTool.PresentationDescriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Read: src/a.cs");
    }
}
