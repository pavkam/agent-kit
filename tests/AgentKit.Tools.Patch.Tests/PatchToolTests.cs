// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;

using AgentKit.TestSupport;

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

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
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

    [Theory]
    [InlineData(WorkspacePatchEntryStatus.Unchanged, SideEffectCertainty.PartiallyPerformed)]
    [InlineData(WorkspacePatchEntryStatus.Uncertain, SideEffectCertainty.Unknown)]
    public async Task InvokeAsync_WhenHostReportsPartial_ReturnsFailureWithPerEntrySettlementContent(WorkspacePatchEntryStatus secondStatus, SideEffectCertainty certainty)
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
                        secondStatus,
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
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(certainty);
        result.Outcome.Retryable.ShouldBeFalse();
        Status(result).ShouldBe("\"Partial\"");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("entries")[0].GetProperty("status").GetString().ShouldBe("Committed");
        json.RootElement.GetProperty("entries")[1].GetProperty("status").GetString().ShouldBe(secondStatus.ToString());
    }

    [Fact]
    public void Descriptor_WhenRead_ExposesStableIdentity()
    {
        var tool = CreateTool(new FakeSnapshotReader(), new FakePatchApplier(), new SequencedSecurityAuthority());

        tool.Descriptor.Id.ShouldBe(PatchTool.Id);
        tool.Descriptor.Effects.Effect.ShouldBe(ToolEffect.Mutating);
    }

    [Fact]
    public async Task InvokeAsync_WhenPatchExceedsByteBound_RejectsWithLimitExceeded()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Add File: new.txt
            +some added content that is long enough
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority, new PatchToolOptions { MaximumPatchBytes = 5 }).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"LimitExceeded\"");
        authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenObservationAuthorizationIsDenied_ReturnsFailureWithoutPlanningFurther()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority(denyAt: 1);
        var patch = """
            *** Begin Patch
            *** Add File: new.txt
            +content
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAddTargetAlreadyExists_RejectsWithConflict()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["exists.txt"] = FakeSnapshotReader.Snapshot("already here");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Add File: exists.txt
            +content
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"Conflict\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAddedFileExceedsFileByteBound_RejectsWithLimitExceeded()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Add File: new.txt
            +this content is longer than the configured bound
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority, new PatchToolOptions { MaximumFileBytes = 5 }).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"LimitExceeded\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenUpdateTargetIsMissing_RejectsWithSnapshotStatusReason()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: missing.txt
            @@
            -old
            +new
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"NotFound\"");
        result.Outcome.FailureReason.ShouldBe("Missing.");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenUpdatedFileExceedsFileByteBound_RejectsWithLimitExceeded()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["a.txt"] = FakeSnapshotReader.Snapshot("ab\n");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: a.txt
            @@
            -ab
            +abcdefgh
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority, new PatchToolOptions { MaximumFileBytes = 5 }).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"LimitExceeded\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenUpdateHunkProducesNoByteChange_RejectsWithNoChange()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["a.txt"] = FakeSnapshotReader.Snapshot("same\n");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: a.txt
            @@
            -same
            +same
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"NoChange\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenDeleteTargetIsMissing_RejectsWithSnapshotStatusReason()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Delete File: missing.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"NotFound\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenDeleteIsValid_AuthorizesAndAppliesDeleteEntry()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["old.txt"] = FakeSnapshotReader.Snapshot("old");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Delete File: old.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var delete = applier.Requests.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem()
            .ShouldBeOfType<WorkspacePatchDelete>();
        authority.Requests[1].Effect.ShouldBe(SecurityEffect.Delete);
        authority.Requests[1].Resources.ShouldBe(WorkspacePatchSecurityBinding.DeleteResources(delete.Path));
    }

    [Fact]
    public async Task InvokeAsync_WhenMoveSourceIsMissing_RejectsWithSnapshotStatusReason()
    {
        var snapshot = new FakeSnapshotReader();
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority();
        var patch = """
            *** Begin Patch
            *** Update File: missing.txt
            *** Move to: new.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"NotFound\"");
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMoveDestinationObservationIsDenied_ReturnsFailureWithoutApplying()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["old.txt"] = FakeSnapshotReader.Snapshot("old");
        var applier = new FakePatchApplier();
        var authority = new SequencedSecurityAuthority(denyAt: 2);
        var patch = """
            *** Begin Patch
            *** Update File: old.txt
            *** Move to: new.txt
            *** End Patch
            """;

        var result = await CreateTool(snapshot, applier, authority).InvokeAsync(
            Request(PatchArguments(patch)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        applier.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMoveDestinationAlreadyExists_RejectsWithConflict()
    {
        var snapshot = new FakeSnapshotReader();
        snapshot.Results["old.txt"] = FakeSnapshotReader.Snapshot("old");
        snapshot.Results["new.txt"] = FakeSnapshotReader.Snapshot("already there");
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

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        Status(result).ShouldBe("\"Conflict\"");
        applier.Requests.ShouldBeEmpty();
    }

    private static PatchTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IWorkspacePatchApplier applier,
        ISecurityAuthority authority,
        PatchToolOptions? options = null) => new(
            snapshotReader,
            applier, new FixedSecurityAuthoritySelector(authority),
            new SequenceSecurityRequestIdGenerator(),
            new SequenceMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options ?? new PatchToolOptions()));

    private static string PatchArguments(string patch) => JsonSerializer.Serialize(new { patch });

    private static string Status(ToolInvocationResult result) => Encoding.UTF8.GetString(
        result.Outcome.Extensions.Values["agentkit.patch.status"].CanonicalJson.AsSpan());

    private static ToolInvocationRequest Request(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
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
