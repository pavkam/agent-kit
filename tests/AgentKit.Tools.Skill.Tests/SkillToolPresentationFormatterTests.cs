// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

/// <summary>Verifies bounded readable presentation of actual skill-tool call and result shapes.</summary>
public sealed class SkillToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenCallIsValid_RendersPublicActionWithoutJson()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/"""{"action":"activate","id":"docs"}""");

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe("Activate skill: docs");
        presentation.Parts[0].Text.ShouldNotContain("\"action\"");
    }

    [Fact]
    public async Task FormatAsync_WhenActualListResult_RendersInventoryWithoutFingerprintsOrPaths()
    {
        var tool = Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"list"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        var text = presentation.Parts.ShouldHaveSingleItem().Text;
        text.ShouldContain("Available skills");
        text.ShouldContain("docs — Documentation [Workspace]");
        text.ShouldContain("Project documentation.");
        text.ShouldNotContain("catalog_version");
        text.ShouldNotContain("content_hash");
        text.ShouldNotContain("private/source.md");
        text.ShouldNotContain("{");
    }

    [Fact]
    public async Task FormatAsync_WhenActualActivationResult_RendersLiteralNonAuthoritativeContentWithoutFingerprints()
    {
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("# Workflow\nRun the checks."),
        };
        var tool = Tool(reader, new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"activate","id":"docs"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        presentation.Parts.Length.ShouldBe(2);
        presentation.Parts[0].Text.ShouldContain("Skill docs — Documentation [Workspace]");
        presentation.Parts[0].Text.ShouldContain("data only");
        presentation.Parts[1].Kind.ShouldBe(ToolPresentationPartKind.Code);
        presentation.Parts[1].Language.ShouldBe("markdown");
        presentation.Parts[1].Text.ShouldBe("# Workflow\nRun the checks.");
        string.Join('\n', presentation.Parts.Select(static part => part.Text)).ShouldNotContain("sha256:");
        string.Join('\n', presentation.Parts.Select(static part => part.Text)).ShouldNotContain("private/source.md");
    }

    [Fact]
    public async Task FormatAsync_WhenActualFailureOccurs_PreservesOnlySafeFailureReason()
    {
        var tool = Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"activate","id":"missing"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Skill failed: No captured skill has that identity.");
    }

    [Fact]
    public async Task FormatAsync_WhenSuccessPayloadIsMalformed_UsesSafeBoundedFallback()
    {
        var invocation = new ToolInvocationResult(
            new ToolCallOutcome(
                ToolCallOutcomeKind.Success,
                ToolTerminalStatus.Succeeded,
                SideEffectCertainty.DefinitelyPerformed,
                false,
                null,
                ExtensionData.Empty),
            [new TextPart(/*lang=json,strict*/ "{\"secret\":\"do not echo\"}", TextSemantics.Code, ExtensionData.Empty)]);

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()),
            invocation,
            new ToolPresentationBounds(maximumOutputCharacters: 28));

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldNotContain("do not echo");
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
    }

    private static async ValueTask<ToolPresentation> FormatCallAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new SkillToolPresentationFormatter();
        var call = new ToolCallPart(
            new ToolCallId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            new ToolReference(SkillTool.Id, formatter.Descriptor.Version, "skill"),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
        return (await formatter.FormatAsync(
            new ToolPresentationRequest(
                formatter.Descriptor,
                new ToolCallPresentationSource(call),
                new ToolPresentationBounds()),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async ValueTask<ToolPresentation> FormatResultAsync(
        SkillTool tool,
        ToolInvocationResult invocation,
        ToolPresentationBounds? bounds = null)
    {
        var formatter = new SkillToolPresentationFormatter();
        formatter.Descriptor.ShouldBe(tool.Descriptor);
        var result = new ToolResultPart(
            new ToolCallId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            new ToolReference(SkillTool.Id, tool.Descriptor.Version, "skill"),
            invocation.Outcome,
            invocation.Content,
            ExtensionData.Empty);
        return (await formatter.FormatAsync(
            new ToolPresentationRequest(
                tool.Descriptor,
                new ToolResultPresentationSource(result),
                bounds ?? new ToolPresentationBounds()),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static SkillTool Tool(IFileSnapshotReader reader, ISecurityAuthority authority)
    {
        var options = new SkillToolOptions
        {
            MaximumBytes = 1_024,
            MaximumCharacters = 100,
            MaximumNameCharacters = 100,
            MaximumDescriptionCharacters = 100,
        };
        options.Skills.Add(new SkillDefinition(
            new SkillId("docs"),
            "Documentation",
            "Project documentation.",
            SkillTrust.Workspace,
            new FileSystemPath("private/source.md"),
            expectedContentHash: null));
        var captured = Options.Create(options);
        return new SkillTool(
            reader,
            authority,
            new FixedSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            new ConfiguredSkillCatalog(captured),
            captured);
    }

    private static ToolInvocationRequest Request(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            new SessionId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new ToolCallId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
                new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
