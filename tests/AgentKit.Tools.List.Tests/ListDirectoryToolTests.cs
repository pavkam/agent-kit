// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using System.Diagnostics;

using AgentKit.Observability;

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
        reader.Operations.ShouldBeEmpty();
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
        reader.Operations.ShouldBeEmpty();
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
    public async Task InvokeAsync_WhenCursorSnapshotIsStale_ReportsSnapshotChangedWithoutEntries()
    {
        var reader = new FakeDirectoryReader { Entries = [("a.cs", false), ("b.cs", false)] };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":"src","cursor":{"snapshot":"sha256:x","next_index":1}}""",
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Content.ShouldBeEmpty();
        ExtensionStatus(result).ShouldBe("\"SnapshotChanged\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenCursorIsIssuedByAnEarlierPage_ResumesAtTheNextEntry()
    {
        var reader = new FakeDirectoryReader { Entries = [("c.cs", false), ("a.cs", false), ("b.cs", true)] };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());
        var first = await InvokeAsync(
            tool, /*lang=json,strict*/ """{"path":"src","maximum_entries":2}""", TestContext.Current.CancellationToken);
        using var firstJson = JsonDocument.Parse(first.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        var continuation = firstJson.RootElement.GetProperty("continuation");

        var second = await InvokeAsync(
            tool,
            $$$"""{"path":"src","maximum_entries":2,"cursor":{"snapshot":"{{{continuation.GetProperty("snapshot").GetString()}}}","next_index":{{{continuation.GetProperty("next_index").GetInt32()}}}}}""",
            TestContext.Current.CancellationToken);

        firstJson.RootElement.GetProperty("entries").EnumerateArray().Select(static e => e.GetString())
            .ShouldBe(["src/a.cs", "src/b.cs"]);
        second.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        using var secondJson = JsonDocument.Parse(second.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        secondJson.RootElement.GetProperty("entries").EnumerateArray().Select(static e => e.GetString())
            .ShouldBe(["src/c.cs"]);
        secondJson.RootElement.GetProperty("continuation").ValueKind.ShouldBe(JsonValueKind.Null);
        secondJson.RootElement.GetProperty("snapshot").GetString()
            .ShouldBe(firstJson.RootElement.GetProperty("snapshot").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenCursorIndexExceedsTheListing_ReportsSnapshotChanged()
    {
        var reader = new FakeDirectoryReader { Entries = [("a.cs", false)] };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());
        var first = await InvokeAsync(
            tool, /*lang=json,strict*/ """{"path":"src","maximum_entries":1}""", TestContext.Current.CancellationToken);
        using var firstJson = JsonDocument.Parse(first.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        var snapshot = firstJson.RootElement.GetProperty("snapshot").GetString();

        var result = await InvokeAsync(
            tool,
            $$$"""{"path":"src","cursor":{"snapshot":"{{{snapshot}}}","next_index":5}}""",
            TestContext.Current.CancellationToken);

        ExtensionStatus(result).ShouldBe("\"SnapshotChanged\"");
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
        reader.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_ProjectsEntriesAndStableContinuation()
    {
        var reader = new FakeDirectoryReader { Entries = [("a.cs", false), ("b.cs", false), ("c.cs", false)] };
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(
            tool,
            /*lang=json,strict*/ """{"path":"src","maximum_entries":2}""",
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var text = result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text;
        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("entries").EnumerateArray().Select(static e => e.GetString())
            .ShouldBe(["src/a.cs", "src/b.cs"]);
        json.RootElement.GetProperty("continuation").GetProperty("next_index").GetInt32().ShouldBe(2);
        json.RootElement.GetProperty("continuation").GetProperty("snapshot").GetString()
            .ShouldBe(json.RootElement.GetProperty("snapshot").GetString());
        var request = authority.Requests.ShouldHaveSingleItem();
        request.Kind.ShouldBe(SecurityOperationKind.DirectoryRead);
        request.Audience.ShouldBe(reader.SecurityAudience);
        request.Resources.ShouldBe([DirectorySecurityBinding.Resource(new FileSystemPath("src"))]);
        request.InputFingerprint.ShouldBe(DirectorySecurityBinding.Fingerprint(new FileSystemPath("src")));
        var operation = reader.Operations.ShouldHaveSingleItem();
        operation.Grant.RequestId.ShouldBe(request.Id);
        operation.ResolvedTarget.RelativePath.Value.ShouldBe("src");
        operation.ResolvedTarget.RootId.ShouldBe(new FileRootId("test"));
        operation.ResolvedTarget.HostTargetPath.ShouldBe(Path.GetFullPath("/tmp/test-root/src"));
    }

    [Fact]
    public async Task InvokeAsync_WhenPathIsOmitted_ListsTheRootWithoutAContinuation()
    {
        var reader = new FakeDirectoryReader { Entries = [("README.md", false), ("src", true)] };
        var authority = new RecordingSecurityAuthority();
        var tool = CreateTool(reader, authority);

        var result = await InvokeAsync(tool, /*lang=json,strict*/ "{}", TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("entries").EnumerateArray().Select(static e => e.GetString())
            .ShouldBe(["README.md", "src"]);
        json.RootElement.GetProperty("continuation").ValueKind.ShouldBe(JsonValueKind.Null);
        authority.Requests.ShouldHaveSingleItem().Resources.ShouldBe([DirectorySecurityBinding.Resource(null)]);
        reader.Operations.ShouldHaveSingleItem().ResolvedTarget.RelativePath.Value.ShouldBe(".");
    }

    [Theory]
    [MemberData(nameof(ReaderFailures))]
    public async Task InvokeAsync_WhenReaderFails_PreservesTypedStatusInOutcome(
        Exception failure, string expectedStatus, ToolCallOutcomeKind expectedKind, SideEffectCertainty expectedCertainty)
    {
        var reader = new FakeDirectoryReader { Failure = failure };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());

        var result = await InvokeAsync(tool, /*lang=json,strict*/ """{"path":"src"}""", TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(expectedKind);
        result.Outcome.SideEffectCertainty.ShouldBe(expectedCertainty);
        result.Outcome.FailureReason.ShouldBe(failure.Message);
        ExtensionStatus(result).ShouldBe($"\"{expectedStatus}\"");
    }

    public static TheoryData<Exception, string, ToolCallOutcomeKind, SideEffectCertainty> ReaderFailures() => new()
    {
        { new UnauthorizedAccessException("Boundary."), "Denied", ToolCallOutcomeKind.Rejected, SideEffectCertainty.DefinitelyNotPerformed },
        { new DirectoryNotFoundException("Missing."), "NotFound", ToolCallOutcomeKind.Failed, SideEffectCertainty.DefinitelyNotPerformed },
        { new IOException("Too many entries."), "Failed", ToolCallOutcomeKind.Failed, SideEffectCertainty.Unknown },
    };

    [Fact]
    public async Task InvokeAsync_WhenCallerCancels_PropagatesCancellationFromTheReader()
    {
        var reader = new FakeDirectoryReader { Entries = [("a.cs", false)] };
        var tool = CreateTool(reader, new RecordingSecurityAuthority());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => InvokeAsync(tool, /*lang=json,strict*/ """{"path":"src"}""", cancellation.Token));
    }

    private static string ExtensionStatus(ToolInvocationResult result) =>
        System.Text.Encoding.UTF8.GetString(result.Outcome.Extensions.Values["agentkit.directory.status"].CanonicalJson.AsSpan());

    private static ListDirectoryTool CreateTool(
            IDirectoryReader reader,
            ISecurityAuthority authority) =>
            TestListComposition.CreateTool(reader, authority);

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

    [Fact]
    public async Task InvokeAsync_WhenObserved_ReportsTheOutcomeWithoutArgumentContent()
    {
        var logger = new RecordingLogger<ListDirectoryTool>();
        var tool = TestListComposition.CreateTool(new FakeDirectoryReader(), new RecordingSecurityAuthority(), logger: logger);
        const string json = /*lang=json,strict*/ """{"path":"../classified-argument-9137"}""";
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.ExecuteTool
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolId), ListDirectoryTool.Id.ToString()));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolLeafOperationCount);

        var result = await InvokeAsync(tool, json, TestContext.Current.CancellationToken);

        var outcome = result.Outcome.Kind == ToolCallOutcomeKind.Success ? "succeeded" : "rejected";
        activities.Snapshot().ShouldContain(observation =>
            observation.Status == ActivityStatusCode.Ok && Equals(observation.GetTagItem(AgentKitTagNames.Outcome), outcome));
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(33400);
        entry.Level.ShouldBe(LogLevel.Debug);
        metrics.Snapshot().ShouldContain(measurement => Equals(measurement.Tags[AgentKitTagNames.Outcome], outcome));
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), metrics.Snapshot(), "classified-argument-9137");
    }
}
