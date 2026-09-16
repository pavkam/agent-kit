// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

/// <summary>Verifies that command previews retain failure, stream identity, and incomplete output evidence.</summary>
public sealed class CommandToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenBothStreamsExist_LabelsThemSeparatelyFromTheirLiteralContent()
    {
        var presentation = await FormatResultAsync(JsonSerializer.Serialize(new
        {
            exit_code = 7,
            stdout = "out\n<b>literal</b>",
            stderr = "err",
            stdout_truncated = true,
        }));

        var parts = presentation.ShouldNotBeNull().Parts;
        parts[0].Text.ShouldBe("Outcome: InvocationFailed\ncommand failed\nExit code: 7");
        parts[1].Text.ShouldBe("stdout (tail; earlier bytes omitted)");
        parts[2].Text.ShouldBe("out\n<b>literal</b>");
        parts[2].Kind.ShouldBe(ToolPresentationPartKind.Code);
        parts[2].Language.ShouldBeNull();
        parts[3].Text.ShouldBe("stderr");
        parts[4].Text.ShouldBe("err");
    }

    [Fact]
    public async Task FormatAsync_WhenOutputIsBinary_PreservesEncodingAndDistinguishesEmptyStream()
    {
        var presentation = await FormatResultAsync(/*lang=json,strict*/ "{\"exit_code\":null,\"stdout\":null,\"stdout_base64\":\"/w==\",\"stderr\":\"\"}");

        var parts = presentation.ShouldNotBeNull().Parts;
        parts[0].Text.ShouldContain("Exit code: unavailable");
        parts[1].Text.ShouldBe("stdout (base64; binary output)");
        parts[2].Text.ShouldBe("/w==");
        parts[3].Text.ShouldBe("stderr: empty");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    public async Task FormatAsync_WhenResultIsMalformed_DeclinesThePreview(string json)
    {
        var presentation = await FormatResultAsync(json);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenSourceIsUnrecognized_ReturnsNull()
    {
        var formatter = new CommandToolPresentationFormatter();

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new UnsupportedPresentationSource(), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"working_directory\":1}")]
    public async Task FormatAsync_WhenCallIsMalformed_ReturnsNull(string json)
    {
        var formatter = new CommandToolPresentationFormatter();
        using var document = JsonDocument.Parse(json);
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("command"), null, null),
            document.RootElement.Clone(), null, ExtensionData.Empty);

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsEmpty_RendersOnlySummary()
    {
        var formatter = new CommandToolPresentationFormatter();
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("command"), null, null),
            new ToolCallOutcome(ToolCallOutcomeKind.Rejected, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed, false, "Denied.", ExtensionData.Empty),
            [], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Outcome: Denied\nDenied.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsNotOneTextPart_ReturnsNull()
    {
        var formatter = new CommandToolPresentationFormatter();
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("command"), null, null),
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart("{}", TextSemantics.Code, ExtensionData.Empty), new TextPart("{}", TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenStreamHasNeitherTextNorBase64_LabelsItUnavailable()
    {
        var presentation = await FormatResultAsync(JsonSerializer.Serialize(new
        {
            exit_code = 0,
        }));

        var parts = presentation.ShouldNotBeNull().Parts;
        parts[1].Text.ShouldBe("stdout: unavailable");
        parts[2].Text.ShouldBe("stderr: unavailable");
    }

    [Fact]
    public async Task FormatAsync_WhenCallHasWorkingDirectory_PreservesDirectoryAndShellText()
    {
        var formatter = new CommandToolPresentationFormatter();
        using var document = JsonDocument.Parse("{\"command\":\"printf '<b>literal</b>'\",\"working_directory\":\"src/project\"}");
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("command"), null, null),
            document.RootElement, null, ExtensionData.Empty);

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        var parts = presentation.ShouldNotBeNull().Parts;
        parts[0].Text.ShouldBe("Working directory: src/project");
        parts[1].Text.ShouldBe("printf '<b>literal</b>'");
        parts[1].Language.ShouldBe("shell");
    }

    private static ValueTask<ToolPresentation?> FormatResultAsync(string json)
    {
        var formatter = new CommandToolPresentationFormatter();
        var result = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("command"), null, null), new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyPerformed, false, "command failed", ExtensionData.Empty), [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);
    }
}
