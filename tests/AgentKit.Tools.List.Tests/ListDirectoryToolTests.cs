// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

public sealed class ListDirectoryToolTests
{
    [Fact]
    public async Task InvokeAsync_WhenArgumentsInvalid_DoesNotAuthorizeOrObserve()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"path":"../escape"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        authority.Requests.ShouldBeEmpty();
        reader.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSecurityDenies_DoesNotObserveDirectory()
    {
        var reader = new FakeDirectoryReader();
        var authority = new RecordingSecurityAuthority(allow: false);
        var tool = CreateTool(reader, authority);

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"path":"src"}"""), TestContext.Current.CancellationToken);

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

        var result = await tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"path":"src","maximum_entries":2}"""), TestContext.Current.CancellationToken);

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

        var result = await tool.InvokeAsync(Request("{}"), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        var status = result.Outcome.Extensions.Values["agentkit.directory.status"];
        System.Text.Encoding.UTF8.GetString(status.CanonicalJson.AsSpan()).ShouldBe("\"SnapshotChanged\"");
    }

    private static ListDirectoryTool CreateTool(
        IDirectoryReader reader,
        ISecurityAuthority authority) => new(
            reader,
            authority,
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new ListDirectoryToolOptions()));

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
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
