// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using AgentKit.TestSupport;

public sealed class ListDirectoryToolTests
{
    [Fact]
    public async Task InvokeAsync_WhenArgumentsInvalid_DoesNotAuthorizeOrObserve()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":"../escape"}""",
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"str\"")]
    public async Task InvokeAsync_WhenArgumentsAreNotAnObject_DoesNotAuthorizeOrObserve(string json)
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(tool, json, TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenPathIsWrongType_ReturnsInvalidArguments()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":1}""",
            TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        authority.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"maximum_entries\":\"1\"}")]
    [InlineData(/*lang=json,strict*/ "{\"maximum_entries\":0}")]
    [InlineData(/*lang=json,strict*/ "{\"maximum_entries\":-1}")]
    [InlineData(/*lang=json,strict*/ "{\"maximum_entries\":100000}")]
    public async Task InvokeAsync_WhenMaximumEntriesIsInvalid_ReturnsInvalidArguments(string json)
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(tool, json, TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        authority.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":{\"next_index\":1}}")]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":{\"snapshot\":\"sha256:x\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":{\"snapshot\":\"sha256:x\",\"next_index\":\"1\"}}")]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":{\"snapshot\":\"sha256:x\",\"next_index\":0}}")]
    [InlineData(/*lang=json,strict*/ "{\"cursor\":{\"snapshot\":\"\",\"next_index\":1}}")]
    public async Task InvokeAsync_WhenCursorIsInvalid_ReturnsInvalidArguments(string json)
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(tool, json, TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenCursorIsValid_ForwardsExactCursorToReader()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"cursor":{"snapshot":"sha256:x","next_index":3}}""",
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var request = reader.Requests.ShouldHaveSingleItem();
        request.Continuation.ShouldBe(new DirectoryEnumerationCursor(new ContentHash("sha256:x"), 3));
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityDenies_DoesNotObserveDirectory()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority(allow: false);
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":"src"}""",
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_ProjectsEntriesAndStableContinuation()
    {
        var cursor = new DirectoryEnumerationCursor(new ContentHash("sha256:snapshot"), 2);
        var reader = new FakeDirectoryReader
        {
            Result = new DirectoryEnumerationResult(
                DirectoryEnumerationStatus.Success,
                [new DirectoryEntry(new FileSystemPath("src/a.cs")), new DirectoryEntry(new FileSystemPath("src/b.cs"))],
                cursor.SnapshotFingerprint,
                cursor,
                null),
        };
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":"src","maximum_entries":2}""",
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var text = result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text;
        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("entries")[0].GetString().ShouldBe("src/a.cs");
        json.RootElement.GetProperty("continuation").GetProperty("next_index").GetInt32().ShouldBe(2);
        var request = authority.Requests.ShouldHaveSingleItem();
        request.Kind.ShouldBe(SecurityOperationKind.DirectoryRead);
        request.Resources.ShouldBe([DirectorySecurityBinding.Resource(new FileSystemPath("src"))]);
        reader.Requests.ShouldHaveSingleItem().Grant.RequestId.ShouldBe(request.Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenHostReportsSnapshotChanged_PreservesTypedStatusInOutcome()
    {
        var reader = new FakeDirectoryReader
        {
            Result = new DirectoryEnumerationResult(
                DirectoryEnumerationStatus.SnapshotChanged, [], null, null, "Directory changed."),
        };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());

        var result = await InvokeAsync(tool, "{}", TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        var status = result.Outcome.Extensions.Values["agentkit.directory.status"];
        System.Text.Encoding.UTF8.GetString(status.CanonicalJson.AsSpan()).ShouldBe("\"SnapshotChanged\"");
    }

    private static ListDirectoryTool CreateTool(
        ILegacyDirectoryReader reader,
        ISecurityAuthority authority) => new(
            reader,
            new FixedSecurityAuthoritySelector(authority),
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new ListDirectoryToolOptions()));

    private static Task<ToolInvocationResult> InvokeAsync(
        ListDirectoryTool tool,
        string json,
        CancellationToken cancellationToken) =>
        tool.InvokeAsync(InvocationContext(json), cancellationToken).AsTask();

    private static ToolInvocationContext InvocationContext(string json)
    {
        var descriptor = ListDirectoryTool.Descriptor;
        var agentId = new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var sessionId = new SessionId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var runId = new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007"));
        var turnId = new TurnId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        var operationId = new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006"));
        var callId = new ToolCallId(Guid.Parse("50000000-0000-0000-0000-000000000005"));
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        using var arguments = JsonDocument.Parse(json);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new SecurityRequestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            authorization.Scope,
            identity,
            authorization,
            new ComponentId("tool"),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "tool:list_directory")],
            new InputFingerprint("sha256:input"),
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            1);
        return new ToolInvocationContext(
            agentId,
            sessionId,
            runId,
            turnId,
            operationId,
            callId,
            descriptor,
            descriptor.Version,
            arguments.RootElement.Clone(),
            grant,
            attempt: 1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            ToolCaptureTestData.InvocationContext(descriptor).Progress);
    }
}
