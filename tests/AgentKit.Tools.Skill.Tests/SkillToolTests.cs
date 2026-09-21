// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

using AgentKit.TestSupport;



/// <summary>Verifies SkillTool behavior and contracts.</summary>
public sealed class SkillToolTests
{
    [Theory]
    [InlineData( /*lang=json,strict*/"{}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"unknown\"}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"activate\"}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"list\",\"id\":\"docs\"}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"activate\",\"id\":\"\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrRead(string json)
    {
        var reader = new RecordingSnapshotReader();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(reader, authority).InvokeAsync(Request(json), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenListing_ReturnsOnlyApprovedMetadataWithoutAuthorizationOrPath()
    {
        var reader = new RecordingSnapshotReader();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(reader, authority).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"list"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
        var text = result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text;
        text.ShouldContain("docs");
        text.ShouldContain("catalog_version");
        text.ShouldNotContain("private/source.md");
    }

    [Fact]
    public async Task InvokeAsync_WhenIdentityUnknown_PerformsNoAuthorizationOrRead()
    {
        var reader = new RecordingSnapshotReader();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(reader, authority).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"missing"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_PerformsNoSnapshotRead()
    {
        var reader = new RecordingSnapshotReader();
        var authority = new RecordingSecurityAuthority
        {
            Allow = false
        };
        var result = await Tool(reader, authority).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.FailureReason.ShouldBe("Denied.");
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_UsesExactSnapshotEvidenceAndMarksContentNonAuthoritative()
    {
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("hello")
        };
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(reader, authority).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(reader.SecurityAudience);
        security.Resources.ShouldBe([FileSecurityBinding.Resource(new FileSystemPath("private/source.md"))]);
        security.InputFingerprint.ShouldBe(FileSecurityBinding.SnapshotFingerprint(new FileSystemPath("private/source.md"), 1_024));
        var snapshot = reader.Requests.ShouldHaveSingleItem();
        snapshot.Grant.RequestId.ShouldBe(security.Id);
        using var json = Json(result);
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("content").GetString().ShouldBe("hello");
        json.RootElement.GetProperty("trust").GetString().ShouldBe("Workspace");
    }

    [Fact]
    public async Task InvokeAsync_WhenCharacterBoundaryReached_ReportsExplicitTruncation()
    {
        var options = OptionsForTool();
        options.MaximumCharacters = 3;
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("abcdef")
        };
        var result = await Tool(reader, new RecordingSecurityAuthority(), options).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        using var json = Json(result);
        json.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("content").GetString().ShouldBe("abc");
    }

    [Fact]
    public async Task InvokeAsync_WhenTruncationBoundaryLandsInsideASurrogatePair_BacksOffInsteadOfEmittingALoneSurrogate()
    {
        // content[..maximumCharacters] sliced on UTF-16 code units. "ab\U0001F600" is ['a','b',HighSurrogate,
        // LowSurrogate] (4 code units); a maximum of 3 lands exactly between the high and low surrogate. Cutting
        // there must back off to 2 instead of emitting a lone high surrogate.
        var options = OptionsForTool();
        options.MaximumCharacters = 3;
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("ab\U0001F600cd")
        };

        var result = await Tool(reader, new RecordingSecurityAuthority(), options).InvokeAsync(
            Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);

        using var json = Json(result);
        json.RootElement.GetProperty("content").GetString().ShouldBe("ab");
    }

    [Fact]
    public async Task InvokeAsync_WhenIntegrityPinDiffers_ReturnsFailureWithoutContent()
    {
        var options = OptionsForTool(expectedHash: new ContentHash("sha256:wrong"));
        var reader = new RecordingSnapshotReader
        {
            Result = RecordingSnapshotReader.Success("changed")
        };
        var result = await Tool(reader, new RecordingSecurityAuthority(), options).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("integrity");
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotReadFailsAfterAuthorization_ReturnsTypedFailure()
    {
        var reader = new RecordingSnapshotReader
        {
            Result = new FileSnapshotResult(FileSnapshotStatus.NotFound, [], null, "Not found."),
        };
        var result = await Tool(reader, new RecordingSecurityAuthority()).InvokeAsync(
            Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("Not found.");
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotHasUtf8Bom_StripsBomBeforeProjectingContent()
    {
        byte[] withBom = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("hello")];
        var reader = new RecordingSnapshotReader
        {
            Result = new FileSnapshotResult(
                FileSnapshotStatus.Success,
                [.. withBom],
                FileSecurityBinding.ContentFingerprint(withBom.AsSpan()),
                null),
        };
        var result = await Tool(reader, new RecordingSecurityAuthority()).InvokeAsync(
            Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        using var json = Json(result);
        json.RootElement.GetProperty("content").GetString().ShouldBe("hello");
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotIsInvalidUtf8_ReturnsTypedFailure()
    {
        var reader = new RecordingSnapshotReader
        {
            Result = new FileSnapshotResult(FileSnapshotStatus.Success, [0xff], FileSecurityBinding.ContentFingerprint([0xff]), null),
        };
        var result = await Tool(reader, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"""{"action":"activate","id":"docs"}"""), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("UTF-8");
    }

    [Fact]
    public void Constructor_WhenSkillIdsDuplicate_ThrowsBeforeCatalogPublication()
    {
        var options = OptionsForTool();
        options.Skills.Add(Definition());
        var action = () => Tool(new RecordingSnapshotReader(), new RecordingSecurityAuthority(), options);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("skills");
    }

    private static SkillTool Tool(IFileSnapshotReader reader, ISecurityAuthority authority, SkillToolOptions? options = null)
    {
        var configured = options ?? OptionsForTool();
        var captured = Options.Create(configured);
        return new SkillTool(reader, new FixedSecurityAuthoritySelector(authority), new FixedSecurityRequestIdGenerator(), new FixedTimeProvider(), new ConfiguredSkillCatalog(captured), captured);
    }

    private static SkillToolOptions OptionsForTool(ContentHash? expectedHash = null)
    {
        var options = new SkillToolOptions
        {
            MaximumBytes = 1_024,
            MaximumCharacters = 100,
            MaximumDescriptionCharacters = 100,
        };
        options.Skills.Add(Definition(expectedHash));
        return options;
    }

    private static SkillDefinition Definition(ContentHash? expectedHash = null) => new(new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, new FileSystemPath("private/source.md"), expectedHash);
    private static JsonDocument Json(ToolInvocationResult result) => JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
    private static ToolInvocationRequest Request(string json) => new(TestSupport.TestSecurityEvidence.ToolContext(new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new SessionId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new ToolCallId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new InRunOperationCorrelation(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);
}
