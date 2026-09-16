// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob.Tests;

/// <summary>Verifies glob previews and native result projections remain literal and outcome-aware.</summary>
public sealed class GlobToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenCallHasScopeAndExclusions_ShowsActualRequest()
    {
        var presentation = await FormatCall(/*lang=json,strict*/ """{"pattern":"**/*.cs","base_path":"src","exclude_patterns":["**/obj/**"]}""");
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Glob **/*.cs under src; excluding **/obj/**");
    }

    [Fact]
    public async Task FormatAsync_WhenSuccessfulNoMatches_DistinguishesEmptyCompleteSearch()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"status":"NoMatches","matches":[],"visited_entries":17,"complete":true}""", success: true);
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("NoMatches: 0 matches; visited 17 entries; complete.");
    }

    [Fact]
    public async Task FormatAsync_WhenFailedWithPartialMatches_PreservesFailureAndIncompleteEvidence()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"status":"LimitExceeded","matches":["src/a.cs"],"visited_entries":20,"complete":false}""", success: false, "Traversal limit reached.");
        presentation.ShouldNotBeNull().Parts[0].Text.ShouldContain("Traversal limit reached.");
        presentation.Parts[0].Text.ShouldContain("incomplete");
        presentation.Parts[1].Kind.ShouldBe(ToolPresentationPartKind.Code);
        presentation.Parts[1].Text.ShouldBe("src/a.cs");
    }

    [Fact]
    public async Task FormatAsync_WhenSuccessfulPayloadMalformed_DeclinesToGenericFallback()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"matches":"wrong"}""", success: true);
        presentation.ShouldBeNull();
    }

    private static async Task<ToolPresentation?> FormatCall(string json)
    {
        using var document = JsonDocument.Parse(json);
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("glob"), null, null), document.RootElement, null, ExtensionData.Empty);
        return await new GlobToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(GlobTool.PresentationDescriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }

    private static async Task<ToolPresentation?> FormatResult(string json, bool success, string? reason = null)
    {
        var outcome = new ToolCallOutcome(success ? ToolCallOutcomeKind.Success : ToolCallOutcomeKind.Failed,
            success ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyPerformed, false, reason, ExtensionData.Empty);
        var result = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("glob"), null, null), outcome, [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);
        return await new GlobToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(GlobTool.PresentationDescriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }
}
