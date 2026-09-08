// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search.Tests;

public sealed class SearchToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"(x)\\\\1\"}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"x\",\"maximum_files\":10001}")]
    [InlineData(/*lang=json,strict*/ "{\"pattern\":\"x\",\"base_path\":\"../escape\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_DoesNotAuthorizeOrObserve(string json)
    {
        var searcher = new FakeFileContentSearcher();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(searcher, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        searcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityDenies_DoesNotObserveFiles()
    {
        var searcher = new FakeFileContentSearcher();

        var result = await CreateTool(searcher, new RecordingSecurityAuthority(allow: false)).InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"needle","regex":false}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        searcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_ProjectsMatchAndExactSecurityEvidence()
    {
        var searcher = new FakeFileContentSearcher
        {
            Result = new FileSearchResult(
                FileSearchStatus.Success,
                [new FileSearchMatch(
                    new FileSystemPath("src/a.cs"),
                    new ContentHash("sha256:file"),
                    3,
                    20,
                    2,
                    6,
                    0,
                    "  needle",
                    false)],
                4,
                200,
                true,
                null),
        };
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(searcher, authority);

        var result = await tool.InvokeAsync(
            Request(
                /*lang=json,strict*/
                """{"pattern":"needle","regex":false,"base_path":"src","path_pattern":"**/*.cs","case_sensitive":false,"include_hidden":true,"maximum_depth":5,"maximum_files":8,"maximum_bytes":900,"maximum_matches":7,"maximum_line_bytes":100,"maximum_duration_ms":500}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("matches")[0].GetProperty("path").GetString().ShouldBe("src/a.cs");
        json.RootElement.GetProperty("visited_bytes").GetInt64().ShouldBe(200);

        var security = authority.Requests.ShouldHaveSingleItem();
        var host = searcher.Requests.ShouldHaveSingleItem();
        security.Kind.ShouldBe(SecurityOperationKind.FileSearch);
        security.Effect.ShouldBe(SecurityEffect.Observe);
        security.Resources.ShouldBe([FileSearchSecurityBinding.Resource(host.BasePath)]);
        security.InputFingerprint.ShouldBe(FileSearchSecurityBinding.Fingerprint(
            host.BasePath,
            host.Pattern,
            host.PathPattern,
            host.CaseSensitive,
            host.IncludeHidden,
            host.MaximumDepth,
            host.MaximumFiles,
            host.MaximumBytes,
            host.MaximumMatches,
            host.MaximumLineBytes,
            host.MaximumDuration));
        host.Grant.RequestId.ShouldBe(security.Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoMatches_ReturnsSuccessfulTypedEmptyResult()
    {
        var result = await CreateTool(new FakeFileContentSearcher(), new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"missing","regex":false}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        Status(result).ShouldBe("\"NoMatches\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenHostTimesOut_PreservesTypedPartialFailure()
    {
        var searcher = new FakeFileContentSearcher
        {
            Result = new FileSearchResult(
                FileSearchStatus.TimedOut,
                [],
                3,
                100,
                false,
                "Timed out."),
        };

        var result = await CreateTool(searcher, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"pattern":"x","regex":false}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("Timed out.");
        Status(result).ShouldBe("\"TimedOut\"");
        _ = result.Content.ShouldHaveSingleItem();
    }

    private static string Status(ToolInvocationResult result) => Encoding.UTF8.GetString(
        result.Outcome.Extensions.Values["agentkit.search.status"].CanonicalJson.AsSpan());

    private static SearchTool CreateTool(IFileContentSearcher searcher, ISecurityAuthority authority) => new(
        searcher,
        authority,
        new StubSecurityRequestIdGenerator(),
        new FixedTimeProvider(),
        Options.Create(new SearchToolOptions()));

    private static ToolInvocationRequest Request(string json) => new(
        new ToolExecutionContext(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            new SessionId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new ToolCallId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
                new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
