// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

/// <summary>Verifies exact replacement scope and literal before/after content in edit previews.</summary>
public sealed class EditToolPresentationFormatterTests
{
    [Theory]
    [InlineData(false, "Replace one exact match; fail if there are multiple matches")]
    [InlineData(true, "Replace every exact match")]
    public async Task FormatAsync_WhenEditArgumentsAreComplete_PreservesReplacementScopeAndIndentation(bool replaceAll, string scope)
    {
        var arguments = JsonSerializer.Serialize(new
        {
            path = "src/a.cs",
            old_text = "    old\n    second",
            new_text = "    new\n        nested",
            replace_all = replaceAll,
        });

        var presentation = await FormatAsync(arguments);

        var parts = presentation.ShouldNotBeNull().Parts;
        parts.Length.ShouldBe(2);
        parts[0].Text.ShouldBe(scope);
        parts[1].Kind.ShouldBe(ToolPresentationPartKind.Diff);
        parts[1].Path.ShouldBe("src/a.cs");
        parts[1].Text.ShouldBe("-    old\n-    second\n+    new\n+        nested");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.cs\",\"old_text\":\"\",\"new_text\":\"x\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a.cs\",\"old_text\":\"x\",\"new_text\":\"y\",\"replace_all\":\"false\"}")]
    public async Task FormatAsync_WhenArgumentsAreMalformed_DeclinesThePreview(string json)
    {
        var presentation = await FormatAsync(json);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenActualEditResultCommitted_RendersReadableBoundedEvidenceWithoutJson()
    {
        var tool = new EditTool(
            new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("old value") },
            new FakeAtomicFileReplacer(),
            new SequencedSecurityAuthority(),
            new SequenceSecurityRequestIdGenerator(),
            new StubMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new EditToolOptions()));
        var invocation = await tool.InvokeAsync(
            InvocationRequest(JsonSerializer.Serialize(new
            {
                path = "mean.py",
                old_text = "old",
                new_text = "new",
                replace_all = false,
            })),
            TestContext.Current.CancellationToken);
        var projected = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), invocation.Outcome, invocation.Content, new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(projected, new ToolPresentationBounds());

        var text = presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text;
        text.ShouldContain("Edited: mean.py");
        text.ShouldContain("Status: Committed");
        text.ShouldContain("Replacements: 1");
        text.ShouldContain("Atomic target visibility: yes");
        text.ShouldNotContain("{\"status\"");
        text.ShouldNotContain("previous_content_fingerprint");
    }

    [Fact]
    public async Task FormatAsync_WhenCommittedResultExceedsOutputBound_TruncatesWithoutRawJson()
    {
        var json = JsonSerializer.Serialize(new
        {
            status = "Committed",
            path = "src/a-very-long-file-name.cs",
            replacements = 2,
            previous_content_fingerprint = "sha256:previous",
            final_content_fingerprint = "sha256:final",
            bytes = 42,
            atomic_target_visibility = true,
            warning = (string?) null,
        });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome, [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(
            result,
            new ToolPresentationBounds(maximumOutputCharacters: 28));

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.Length.ShouldBe(28);
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
        presentation.Parts[0].Text.ShouldNotContain("{\"");
    }

    [Fact]
    public async Task FormatAsync_WhenResultOutcomeIsNotSuccess_RendersFailureReason()
    {
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Failed,
            ToolTerminalStatus.InvocationFailed,
            SideEffectCertainty.DefinitelyNotPerformed,
            false,
            "The exact old text was not found.",
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome, [],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit failed: The exact old text was not found.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultHasNoTextProjection_RendersFallback()
    {
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome, [],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit completed, but its result projection is unavailable.");
    }

    [Fact]
    public async Task FormatAsync_WhenProjectionExceedsInputBound_ReturnsTruncatedNoticeWithoutInspectingJson()
    {
        var json = JsonSerializer.Serialize(new { status = "Committed", path = "a", replacements = 1, bytes = 1_000_000, atomic_target_visibility = true });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds(maximumInputBytes: 4));

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldContain("exceeds the presentation input limit");
    }

    [Fact]
    public async Task FormatAsync_WhenResultJsonIsMissingRequiredProperties_RendersFallback()
    {
        var json = JsonSerializer.Serialize(new { status = "Committed", path = "a" });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit completed, but its result projection is malformed.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultJsonIsMissingPath_RendersFallback()
    {
        var json = JsonSerializer.Serialize(new { status = "Committed", replacements = 1, bytes = 1, atomic_target_visibility = true });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit completed, but its result projection is malformed.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultProjectionIsNotJson_RendersFallback()
    {
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart("not json", TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit completed, but its result projection is malformed.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultHasNonEmptyStringWarning_AppendsWarningLine()
    {
        var json = JsonSerializer.Serialize(new
        {
            status = "Committed",
            path = "a.cs",
            replacements = 1,
            bytes = 3,
            atomic_target_visibility = true,
            warning = "Consider reviewing surrounding whitespace.",
        });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text.ShouldContain(
            "Warning: Consider reviewing surrounding whitespace.");
    }

    [Fact]
    public async Task FormatAsync_WhenResultWarningHasWrongJsonKind_RendersFallback()
    {
        var json = JsonSerializer.Serialize(new
        {
            status = "Committed",
            path = "a.cs",
            replacements = 1,
            bytes = 3,
            atomic_target_visibility = true,
            warning = 42,
        });
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty);
        var result = new ToolResultPart(
            new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null), outcome,
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)],
            new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var presentation = await FormatResultAsync(result, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Edit completed, but its result projection is malformed.");
    }

    private static ValueTask<ToolPresentation?> FormatAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new EditToolPresentationFormatter();
        var call = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("edit"), null, null),
            document.RootElement.Clone(), null, ExtensionData.Empty);
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);
    }

    private static ValueTask<ToolPresentation?> FormatResultAsync(
        ToolResultPart result,
        ToolPresentationBounds bounds)
    {
        var formatter = new EditToolPresentationFormatter();
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), bounds),
            TestContext.Current.CancellationToken);
    }

    private static ToolInvocationRequest InvocationRequest(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("61000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("71000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("81000000-0000-0000-0000-000000000008")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
