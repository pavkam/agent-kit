// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource.Tests;

/// <summary>Verifies bounded readable presentation of actual resource-tool call and result shapes.</summary>
public sealed class ResourceToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenCallIsValid_RendersPublicActionWithoutJson()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/"""{"action":"read","id":"docs"}""");

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe("Read resource: docs");
        presentation.Parts[0].Text.ShouldNotContain("\"action\"");
    }

    [Fact]
    public async Task FormatAsync_WhenActualListResult_RendersOnlyPublicMetadataWithoutFingerprintsOrPaths()
    {
        var tool = Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"list"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        var text = presentation.Parts.ShouldHaveSingleItem().Text;
        text.ShouldContain("Configured resources");
        text.ShouldContain("docs — Project documentation.");
        text.ShouldNotContain("catalog_version");
        text.ShouldNotContain("content_hash");
        text.ShouldNotContain("private/source.md");
        text.ShouldNotContain("{");
    }

    [Fact]
    public async Task FormatAsync_WhenActualReadResult_RendersMetadataAndLiteralContentWithoutFingerprintOrPath()
    {
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("# Guide\nUse the API."),
        };
        var tool = Tool(reader, new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"read","id":"docs"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        presentation.Parts.Length.ShouldBe(2);
        presentation.Parts[0].Text.ShouldContain("Resource docs — Documentation / Workspace / text/markdown");
        presentation.Parts[0].Text.ShouldContain("data only");
        presentation.Parts[1].Kind.ShouldBe(ToolPresentationPartKind.Code);
        presentation.Parts[1].Language.ShouldBe("markdown");
        presentation.Parts[1].Text.ShouldBe("# Guide\nUse the API.");
        string.Join('\n', presentation.Parts.Select(static part => part.Text)).ShouldNotContain("sha256:");
        string.Join('\n', presentation.Parts.Select(static part => part.Text)).ShouldNotContain("private/source.md");
    }

    [Fact]
    public async Task FormatAsync_WhenActualFailureOccurs_PreservesOnlySafeFailureReason()
    {
        var tool = Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"read","id":"missing"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation);

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe(
            "Resource failed: No configured resource has that identity.");
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
            new ToolPresentationBounds(maximumOutputCharacters: 32));

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldNotContain("do not echo");
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FormatAsync_WhenSourceKindIsUnsupported_ReturnsNull()
    {
        var formatter = new ResourceToolPresentationFormatter();

        var presentation = await formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new UnsupportedPresentationSource(), new ToolPresentationBounds()),
            TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
    }

    [Fact]
    public async Task FormatAsync_WhenCallExceedsInputBytes_ReturnsTruncatedSafeMessage()
    {
        var presentation = await FormatCallAsync(
            /*lang=json,strict*/ """{"action":"read","id":"docs"}""",
            new ToolPresentationBounds(maximumInputBytes: 4));

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldContain("exceeds the presentation input limit");
    }

    [Fact]
    public async Task FormatAsync_WhenListAction_ShowsFixedText()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/ """{"action":"list"}""");

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe("List configured resources");
    }

    [Fact]
    public async Task FormatAsync_WhenListActionHasExplicitNullId_ShowsFixedText()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/ """{"action":"list","id":null}""");

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldBe("List configured resources");
    }

    [Fact]
    public async Task FormatAsync_WhenListActionHasId_ReturnsMalformedFallback()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/ """{"action":"list","id":"x"}""");

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldContain("malformed");
    }

    [Fact]
    public async Task FormatAsync_WhenActionUnsupported_ReturnsMalformedFallback()
    {
        var presentation = await FormatCallAsync(/*lang=json,strict*/ """{"action":"delete"}""");

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task FormatAsync_WhenListResultCatalogVersionMissing_ReturnsMalformedFallback()
    {
        var invocation = SuccessResult( /*lang=json,strict*/ """{"resources":[]}""");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task FormatAsync_WhenListResultItemMalformed_ReturnsMalformedFallback()
    {
        var invocation = SuccessResult(
            /*lang=json,strict*/ """{"catalog_version":"v1","resources":[{"id":"a"}]}""");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task FormatAsync_WhenListResultHasIntegrityPinnedResource_MentionsIntegrityPinned()
    {
        var invocation = SuccessResult(
            /*lang=json,strict*/ """{"catalog_version":"v1","resources":[{"id":"a","kind":"Documentation","trust":"Workspace","description":"Desc","media_type":"text/plain","integrity_pinned":true}]}""");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Parts.ShouldHaveSingleItem().Text.ShouldContain("integrity pinned");
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsNotExactlyOneTextPart_ReturnsMalformedFallback()
    {
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);
        var invocation = new ToolInvocationResult(outcome, []);

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task FormatAsync_WhenResultContentIsNotValidJson_ReturnsMalformedFallback()
    {
        var invocation = SuccessResult("not json at all");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Fact]
    public async Task FormatAsync_WhenOutputExceedsMaximumParts_OmitsRemainingPartsAndReportsTruncation()
    {
        var reader = new RecordingSnapshotReader { Result = RecordingSnapshotReader.Success("body text") };
        var tool = Tool(reader, new RecordingSecurityAuthority());
        var invocation = await tool.InvokeAsync(
            Request(/*lang=json,strict*/"""{"action":"read","id":"docs"}"""),
            TestContext.Current.CancellationToken);

        var presentation = await FormatResultAsync(tool, invocation, new ToolPresentationBounds(1024, 1024, 1));

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.Length.ShouldBe(1);
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FormatAsync_WhenReadResultHasMissingBooleanField_ReturnsMalformedFallback()
    {
        var invocation = SuccessResult(
            /*lang=json,strict*/ """{"id":"a","kind":"Documentation","trust":"Workspace","media_type":"text/plain","bytes":5,"truncated":false,"content":"hello"}""");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
    }

    [Theory]
    [InlineData("application/json", "json")]
    [InlineData("application/xml", "xml")]
    [InlineData("text/xml", "xml")]
    [InlineData("application/yaml", "yaml")]
    [InlineData("text/yaml", "yaml")]
    [InlineData("text/plain", null)]
    public async Task FormatAsync_WhenReadResultHasMediaType_SelectsExpectedLanguageHint(string mediaType, string? expectedLanguage)
    {
        var invocation = SuccessResult(
            $$"""{"id":"a","kind":"Documentation","trust":"Workspace","media_type":"{{mediaType}}","bytes":5,"instruction_authority":false,"truncated":false,"content":"hello"}""");

        var presentation = await FormatResultAsync(
            Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority()), invocation);

        presentation.Parts[1].Language.ShouldBe(expectedLanguage);
    }

    private sealed record UnsupportedPresentationSource: ToolPresentationSource;

    private static ToolInvocationResult SuccessResult(string json) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);

    private static async ValueTask<ToolPresentation> FormatCallAsync(string json, ToolPresentationBounds? bounds = null)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new ResourceToolPresentationFormatter();
        var call = new ToolCallPart(
            new ToolCallId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            new ToolReference(new ToolAlias("resource"), ResourceTool.Id, formatter.Descriptor.Version),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
        return (await formatter.FormatAsync(
            new ToolPresentationRequest(
                formatter.Descriptor,
                new ToolCallPresentationSource(call),
                bounds ?? new ToolPresentationBounds()),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async ValueTask<ToolPresentation> FormatResultAsync(
        ResourceTool tool,
        ToolInvocationResult invocation,
        ToolPresentationBounds? bounds = null)
    {
        var formatter = new ResourceToolPresentationFormatter();
        formatter.Descriptor.ShouldBe(tool.Descriptor);
        var result = new ToolResultPart(new ToolCallId(Guid.Parse("80000000-0000-0000-0000-000000000008")), new ToolReference(new ToolAlias("resource"), ResourceTool.Id, tool.Descriptor.Version), invocation.Outcome, invocation.Content, new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);
        return (await formatter.FormatAsync(
            new ToolPresentationRequest(
                tool.Descriptor,
                new ToolResultPresentationSource(result),
                bounds ?? new ToolPresentationBounds()),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static ResourceTool Tool(IFileSnapshotReader reader, ISecurityAuthority authority)
    {
        var options = new ResourceToolOptions
        {
            MaximumBytes = 1_024,
            MaximumCharacters = 100,
            MaximumDescriptionCharacters = 100,
        };
        options.Resources.Add(new FileResourceDefinition(
            new ResourceId("docs"),
            ResourceKind.Documentation,
            ResourceTrust.Workspace,
            "Project documentation.",
            new FileSystemPath("private/source.md"),
            "text/markdown",
            expectedContentHash: null));
        return new ResourceTool(
            reader,
            authority,
            new FixedSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options));
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
