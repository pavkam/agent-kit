// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search.Tests;

/// <summary>Verifies search previews use the real schema and native results retain partial-match evidence.</summary>
public sealed class SearchToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenCallIsLiteralAndScoped_ShowsPatternScopeAndExclusions()
    {
        var presentation = await FormatCall(/*lang=json,strict*/ """{"pattern":"needle","regex":false,"base_path":"src","path_pattern":"**/*.cs","exclude_patterns":["**/obj/**"]}""");
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Search (literal) for needle under src in **/*.cs; excluding **/obj/**");
    }

    [Fact]
    public async Task FormatAsync_WhenMatchLineWasClipped_MarksLineAndKeepsLocation()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"status":"Success","matches":[{"path":"src/a.cs","line_number":12,"line_text":"var needle =","line_text_truncated":true}],"visited_files":4,"visited_bytes":900,"complete":true}""", true);
        presentation.ShouldNotBeNull().Parts[0].Text.ShouldContain("1 match in 4 files");
        presentation.Parts[1].Text.ShouldBe("src/a.cs:12: var needle = … [line truncated]");
    }

    [Fact]
    public async Task FormatAsync_WhenSuccessfulNoMatches_DistinguishesEmptyCompleteSearch()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"status":"NoMatches","matches":[],"visited_files":4,"visited_bytes":900,"complete":true}""", true);
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("NoMatches: 0 matches in 4 files (900 bytes visited); complete.");
    }

    [Fact]
    public async Task FormatAsync_WhenTimedOutWithPartialMatch_PreservesFailureAndIncompleteEvidence()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"status":"TimedOut","matches":[{"path":"a.txt","line_number":1,"line_text":"x","line_text_truncated":false}],"visited_files":2,"visited_bytes":20,"complete":false}""", false, "Timed out.");
        presentation.ShouldNotBeNull().Parts[0].Text.ShouldContain("Timed out.");
        presentation.Parts[0].Text.ShouldContain("incomplete");
        presentation.Parts[1].Text.ShouldBe("a.txt:1: x");
    }

    [Fact]
    public async Task FormatAsync_WhenSuccessfulPayloadMalformed_DeclinesToGenericFallback() =>
        (await FormatResult(/*lang=json,strict*/ """{"status":"Success","matches":null}""", true)).ShouldBeNull();

    private static async Task<ToolPresentation?> FormatCall(string json)
    {
        using var document = JsonDocument.Parse(json);
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(SearchTool.Id, null, "search"), document.RootElement, null, ExtensionData.Empty);
        return await new SearchToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(SearchTool.PresentationDescriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }

    private static async Task<ToolPresentation?> FormatResult(string json, bool success, string? reason = null)
    {
        var outcome = new ToolCallOutcome(success ? ToolCallOutcomeKind.Success : ToolCallOutcomeKind.Failed,
            success ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.TimedOut, SideEffectCertainty.PartiallyPerformed, false, reason, ExtensionData.Empty);
        var result = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(SearchTool.Id, null, "search"), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)], ExtensionData.Empty);
        return await new SearchToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(SearchTool.PresentationDescriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }
}
