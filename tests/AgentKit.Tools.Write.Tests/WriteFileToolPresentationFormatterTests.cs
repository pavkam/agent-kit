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

    private static ValueTask<ToolPresentation?> FormatAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new WriteFileToolPresentationFormatter();
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(WriteFileTool.Id, null, "write_file"),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);
    }
}
