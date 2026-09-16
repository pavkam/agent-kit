// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

/// <summary>Verifies plan actions and native plan snapshots become readable provider-neutral parts.</summary>
public sealed class PlanToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenReplacingPlan_ShowsTitleCountAndRevision()
    {
        var presentation = await FormatCall(/*lang=json,strict*/ """{"action":"replace","title":"Ship","items":[{"id":"a","text":"Test","status":"pending"}],"expected_revision":3}""");
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Replace plan “Ship” with 1 item; expected revision 3.");
    }

    [Fact]
    public async Task FormatAsync_WhenSettingStatus_ShowsItemStatusAndRevision()
    {
        var presentation = await FormatCall(/*lang=json,strict*/ """{"action":"set_status","item_id":"a","status":"completed","expected_revision":4}""");
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("Set plan item a to completed; expected revision 4.");
    }

    [Fact]
    public async Task FormatAsync_WhenPlanExists_ShowsMetadataAndLiteralItems()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"plan":{"id":"plan-1","revision":5,"title":"Ship","items":[{"id":"a","text":"Run tests","status":"in_progress"}]}}""", true);
        presentation.ShouldNotBeNull().Parts[0].Text.ShouldBe("Plan: Ship (plan-1), revision 5, 1 item.");
        presentation.Parts[1].Kind.ShouldBe(ToolPresentationPartKind.Code);
        presentation.Parts[1].Text.ShouldBe("[in_progress] a — Run tests");
    }

    [Fact]
    public async Task FormatAsync_WhenPlanIsMissing_DistinguishesMissingFromFailure()
    {
        var presentation = await FormatResult(/*lang=json,strict*/ """{"plan":null}""", true);
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("No current plan.");
    }

    [Fact]
    public async Task FormatAsync_WhenFailureHasNoNativePayload_PreservesFailureReason()
    {
        var presentation = await FormatResult("not json", false, "The plan changed; current revision is 7.");
        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe("The plan changed; current revision is 7.");
    }

    private static async Task<ToolPresentation?> FormatCall(string json)
    {
        using var document = JsonDocument.Parse(json);
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("plan"), null, null), document.RootElement, null, ExtensionData.Empty);
        return await new PlanToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(PlanTool.PresentationDescriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }

    private static async Task<ToolPresentation?> FormatResult(string json, bool success, string? reason = null)
    {
        var outcome = new ToolCallOutcome(success ? ToolCallOutcomeKind.Success : ToolCallOutcomeKind.Failed,
            success ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyPerformed, false, reason, ExtensionData.Empty);
        var result = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("plan"), null, null), outcome, [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);
        return await new PlanToolPresentationFormatter().FormatAsync(new ToolPresentationRequest(PlanTool.PresentationDescriptor, new ToolResultPresentationSource(result), new ToolPresentationBounds()), TestContext.Current.CancellationToken);
    }
}
