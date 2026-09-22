// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob.Tests;

using AgentKit.TestSupport;

public sealed class GlobToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"../*.cs\"}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"case_sensitive\":\"yes\"}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"exclude_patterns\":[\"../bin/**\"]}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"exclude_patterns\":[42]}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"maximum_results\":10001}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"base_path\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"base_path\":\"../escape\"}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"**/*.cs\",\"exclude_patterns\":\"not-an-array\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_DoesNotAuthorizeOrObserve(string json)
    {
        var globber = new FakeFileGlobber();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(globber, authority);

        var result = await tool.InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        authority.Requests.ShouldBeEmpty();
        globber.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenExcludePatternsExceedsMaximumCount_ReturnsInvalidArguments()
    {
        var globber = new FakeFileGlobber();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(globber, authority);
        var excludePatterns = Enumerable.Range(0, 101).Select(static index => $"pattern-{index}/**");
        var json = JsonSerializer.Serialize(new { pattern = "**/*.cs", exclude_patterns = excludePatterns });

        var result = await tool.InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        authority.Requests.ShouldBeEmpty();
        globber.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Descriptor_WhenAccessed_MatchesPresentationDescriptor()
    {
        var tool = CreateTool(new FakeFileGlobber(), new RecordingSecurityAuthority());

        ((ITool) tool).Descriptor.ShouldBeSameAs(GlobTool.PresentationDescriptor);
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityDenies_DoesNotObserveWorkspace()
    {
        var globber = new FakeFileGlobber();
        var tool = CreateTool(globber, new RecordingSecurityAuthority(allow: false));

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"**/*.cs"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        globber.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_ProjectsMatchesAndExactSecurityBinding()
    {
        var globber = new FakeFileGlobber
        {
            Result = new GlobResult(
                GlobStatus.Success,
                [new FileSystemPath("src/A.cs"), new FileSystemPath("src/B.cs")],
                7,
                true,
                null),
        };
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(globber, authority);

        var result = await tool.InvokeAsync(
            Request(
                /*lang=json,strict*/
                """{"pattern":"**/*.cs","base_path":"src","case_sensitive":false,"include_hidden":true,"exclude_patterns":["**/bin/**","**/obj/**"],"maximum_depth":8,"maximum_visited_entries":30,"maximum_results":4}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("matches")[0].GetString().ShouldBe("src/A.cs");
        json.RootElement.GetProperty("visited_entries").GetInt32().ShouldBe(7);
        json.RootElement.GetProperty("complete").GetBoolean().ShouldBeTrue();

        var securityRequest = authority.Requests.ShouldHaveSingleItem();
        var basePath = new FileSystemPath("src");
        var pattern = new GlobPattern("**/*.cs");
        securityRequest.Kind.ShouldBe(SecurityOperationKind.DirectoryRead);
        securityRequest.Effect.ShouldBe(SecurityEffect.Observe);
        securityRequest.Resources.ShouldBe([GlobSecurityBinding.Resource(basePath)]);
        securityRequest.InputFingerprint.ShouldBe(
            GlobSecurityBinding.Fingerprint(
                basePath,
                pattern,
                false,
                true,
                8,
                30,
                4,
                [new GlobPattern("**/bin/**"), new GlobPattern("**/obj/**")]));

        var hostRequest = globber.Requests.ShouldHaveSingleItem();
        hostRequest.Grant.RequestId.ShouldBe(securityRequest.Id);
        hostRequest.Pattern.ShouldBe(pattern);
        hostRequest.CaseSensitive.ShouldBeFalse();
        hostRequest.IncludeHidden.ShouldBeTrue();
        hostRequest.ExcludedPathPatterns.ShouldBe(
            [new GlobPattern("**/bin/**"), new GlobPattern("**/obj/**")]);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoMatches_ReturnsSuccessfulTypedEmptyResult()
    {
        var tool = CreateTool(new FakeFileGlobber(), new RecordingSecurityAuthority());

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"*.missing"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        ExtensionStatus(result).ShouldBe("\"NoMatches\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenLimitExceeded_PreservesPartialMatchesAndTypedFailure()
    {
        var globber = new FakeFileGlobber
        {
            Result = new GlobResult(
                GlobStatus.LimitExceeded,
                [new FileSystemPath("src/partial.cs")],
                10,
                false,
                "Traversal limit exceeded."),
        };
        var tool = CreateTool(globber, new RecordingSecurityAuthority());

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"**/*.cs"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("Traversal limit exceeded.");
        ExtensionStatus(result).ShouldBe("\"LimitExceeded\"");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("matches")[0].GetString().ShouldBe("src/partial.cs");
        json.RootElement.GetProperty("complete").GetBoolean().ShouldBeFalse();
    }

    private static string ExtensionStatus(ToolInvocationResult result) => Encoding.UTF8.GetString(
        result.Outcome.Extensions.Values["agentkit.glob.status"].CanonicalJson.AsSpan());

    private static GlobTool CreateTool(IFileGlobber globber, ISecurityAuthority authority) => new(
        globber, new FixedSecurityAuthoritySelector(authority),
        new StubSecurityRequestIdGenerator(),
        new FixedTimeProvider(),
        Options.Create(new GlobToolOptions()));

    private static ToolInvocationRequest Request(string json) => new(
        TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            new SessionId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new ToolCallId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
                new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                null),
            TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);

}
