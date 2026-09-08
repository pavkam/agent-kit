// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;

public sealed class PatchToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"patch\":\"no envelope\"}")]
    [InlineData(/*lang=json,strict*/ "{\"patch\":\"*** Begin Patch\\n*** End Patch\"}")]
    [InlineData(/*lang=json,strict*/ "{\"patch\":\"*** Begin Patch\\n*** Add File: ../x\\n+y\\n*** End Patch\"}")]
    public async Task InvokeAsync_WhenPatchIsInvalid_PerformsNoAuthorizationOrHostAccess(string json)
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        snapshot.Requests.ShouldBeEmpty();
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAddIsValid_ObservesAbsenceThenAuthorizesExactCreate()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Add File: src/new.cs
            +line one
            +🚀
            \ No newline at end of file
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var create = applier.Requests.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspacePatchCreate>();
        Encoding.UTF8.GetString(create.Content.AsSpan()).ShouldBe("line one\n🚀");
        authority.Requests.Count.ShouldBe(2);
        authority.Requests[0].Effect.ShouldBe(SecurityEffect.Observe);
        authority.Requests[0].InputFingerprint.ShouldBe(FileSecurityBinding.SnapshotFingerprint(
            create.Path, 10 * 1024 * 1024));
        authority.Requests[1].Effect.ShouldBe(SecurityEffect.Create);
        authority.Requests[1].Resources.ShouldBe(
            WorkspacePatchSecurityBinding.CreateResources(create.Id, create.Path));
        authority.Requests[1].InputFingerprint.ShouldBe(
            WorkspacePatchSecurityBinding.CreateFingerprint(create.Id, create.Path, create.Content));
        create.Grant.RequestId.ShouldBe(authority.Requests[1].Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenUpdateTargetsBomCrlfUnicode_PreservesExactTextPolicyAndBindsBytes()
    {
        byte[] original = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("α old\r\nlast")];
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["src/a.cs"] = FakeSnapshotReader.Snapshot(original);
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: src/a.cs
            @@
            -α old
            +α 🚀
             last
            \ No newline at end of file
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var replace = applier.Requests.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspacePatchReplace>();
        byte[] expected = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("α 🚀\r\nlast")];
        replace.Content.ShouldBe(expected);
        replace.ExpectedContentFingerprint.ShouldBe(FileSecurityBinding.ContentFingerprint(original));
        authority.Requests[1].Effect.ShouldBe(SecurityEffect.Replace);
        authority.Requests[1].InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(
            replace.Id,
            replace.Path,
            replace.ExpectedContentFingerprint,
            replace.Content));
    }

    [Fact]
    public async Task InvokeAsync_WhenHunkContextIsAmbiguous_DoesNotRequestMutationAuthority()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["a.txt"] = FakeSnapshotReader.Snapshot("same\nsame\n");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: a.txt
            @@
            -same
            +changed
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"PatchConflict\"");
        authority.Requests.Count.ShouldBe(1);
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMutationAuthorizationIsDenied_DoesNotInvokeHostApplier()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["old.txt"] = FakeSnapshotReader.Snapshot("old");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority(denyAt: 3);
        var patch = """
            *** Begin Patch
            *** Add File: new.txt
            +new
            *** Delete File: old.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        snapshot.Requests.Count.ShouldBe(2);
        authority.Requests.Count.ShouldBe(3);
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMoveIsValid_BindsBothPathsAndUnchangedSourceVersion()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["old.txt"] = FakeSnapshotReader.Snapshot("old");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: old.txt
            *** Move to: new.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var move = applier.Requests.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspacePatchMove>();
        snapshot.Requests.Select(static request => request.Path.Value).ShouldBe(["old.txt", "new.txt"]);
        authority.Requests[2].Effect.ShouldBe(SecurityEffect.Move);
        authority.Requests[2].Resources.ShouldBe(
            WorkspacePatchSecurityBinding.MoveResources(move.SourcePath, move.DestinationPath));
        authority.Requests[2].InputFingerprint.ShouldBe(WorkspacePatchSecurityBinding.MoveFingerprint(
            move.SourcePath, move.DestinationPath, move.ExpectedContentFingerprint));
    }

    [Fact]
    public async Task InvokeAsync_WhenHostReportsPartial_ReturnsFailureWithPerEntrySettlementContent()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier
        {
            Result = new WorkspacePatchResult(
                WorkspacePatchStatus.Partial,
                [
                    new WorkspacePatchEntryResult(
                        0,
                        WorkspacePatchEntryKind.Create,
                        WorkspacePatchEntryStatus.Committed,
                        new FileSystemPath("one.txt"),
                        null,
                        new ContentHash("sha256:one"),
                        null),
                    new WorkspacePatchEntryResult(
                        1,
                        WorkspacePatchEntryKind.Create,
                        WorkspacePatchEntryStatus.Unchanged,
                        new FileSystemPath("two.txt"),
                        null,
                        null,
                        "Failed."),
                ],
                "Committed prefix only."),
        };
        var patch = """
            *** Begin Patch
            *** Add File: one.txt
            +one
            *** Add File: two.txt
            +two
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, new SequencedSecurityAuthority()).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"Partial\"");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("entries")[0].GetProperty("status").GetString().ShouldBe("Committed");
        json.RootElement.GetProperty("entries")[1].GetProperty("status").GetString().ShouldBe("Unchanged");
    }

    private static PatchTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IWorkspacePatchApplier applier,
        ISecurityAuthority authority) => new(
            snapshotReader,
            applier,
            authority,
            new SequenceSecurityRequestIdGenerator(),
            new SequenceMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new PatchToolOptions()));

    private static string PatchArguments(string patch) => JsonSerializer.Serialize(new { patch });

    private static string Status(ToolInvocationResult result) => Encoding.UTF8.GetString(
        result.Outcome.Extensions.Values["agentkit.patch.status"].CanonicalJson.AsSpan());

    private static ToolInvocationRequest Request(string json) => new(
        new ToolExecutionContext(
            new AgentId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("61000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("71000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("81000000-0000-0000-0000-000000000008")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
