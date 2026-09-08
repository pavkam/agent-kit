// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

public sealed class EditToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"../x\",\"old_text\":\"a\",\"new_text\":\"b\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a\",\"old_text\":\"\",\"new_text\":\"b\"}")]
    [InlineData(/*lang=json,strict*/ "{\"path\":\"a\",\"old_text\":\"a\",\"new_text\":\"b\",\"maximum_bytes\":10485761}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrObservation(string json)
    {
        var snapshot = new FakeSnapshotReader();
        var replacer = new FakeAtomicFileReplacer();
        var authority = new SequencedSecurityAuthority();

        var result = await CreateTool(snapshot, replacer, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        snapshot.Requests.ShouldBeEmpty();
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenReadDenied_PerformsNoObservationOrMutation()
    {
        var snapshot = new FakeSnapshotReader();
        var replacer = new FakeAtomicFileReplacer();

        var result = await CreateTool(snapshot, replacer, new SequencedSecurityAuthority(denyAt: 1)).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        snapshot.Requests.ShouldBeEmpty();
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMatchAmbiguous_RequestsOnlyReadAuthorityAndDoesNotMutate()
    {
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("old old") };
        var replacer = new FakeAtomicFileReplacer();
        var authority = new SequencedSecurityAuthority();

        var result = await CreateTool(snapshot, replacer, authority).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        Status(result).ShouldBe("\"Ambiguous\"");
        authority.Requests.Count.ShouldBe(1);
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenWriteDenied_DoesNotInvokeReplacer()
    {
        var snapshot = new FakeSnapshotReader();
        var replacer = new FakeAtomicFileReplacer();
        var authority = new SequencedSecurityAuthority(denyAt: 2);

        var result = await CreateTool(snapshot, replacer, authority).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        authority.Requests.Count.ShouldBe(2);
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenUnique_PreservesUntouchedBomCrlfAndUnicodeAndBindsFinalBytes()
    {
        byte[] initial = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("α old\r\nlast")];
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot(initial) };
        var replacer = new FakeAtomicFileReplacer();
        var authority = new SequencedSecurityAuthority();

        var result = await CreateTool(snapshot, replacer, authority).InvokeAsync(
            Request(Arguments("old", "🚀")), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var mutation = replacer.Requests.ShouldHaveSingleItem();
        byte[] expected = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("α 🚀\r\nlast")];
        mutation.Content.ShouldBe(expected);
        var readSecurity = authority.Requests[0];
        readSecurity.Kind.ShouldBe(SecurityOperationKind.FileRead);
        readSecurity.Effect.ShouldBe(SecurityEffect.Observe);
        readSecurity.InputFingerprint.ShouldBe(FileSecurityBinding.SnapshotFingerprint(
            new FileSystemPath("src/a.cs"), 1024 * 1024));
        var writeSecurity = authority.Requests[1];
        writeSecurity.Kind.ShouldBe(SecurityOperationKind.FileWrite);
        writeSecurity.Effect.ShouldBe(SecurityEffect.Replace);
        writeSecurity.Resources.ShouldBe(FileSecurityBinding.AtomicReplaceResources(mutation.Id, mutation.Path));
        writeSecurity.InputFingerprint.ShouldBe(FileSecurityBinding.AtomicReplaceFingerprint(
            mutation.Id, mutation.Path, mutation.ExpectedContentFingerprint, mutation.Content));
        mutation.Grant.RequestId.ShouldBe(writeSecurity.Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenReplaceAllRequested_ReplacesEveryNonOverlappingOccurrence()
    {
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("old-old-old") };
        var replacer = new FakeAtomicFileReplacer();

        var result = await CreateTool(snapshot, replacer, new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new", replaceAll: true)), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        Encoding.UTF8.GetString(replacer.Requests.ShouldHaveSingleItem().Content.AsSpan()).ShouldBe("new-new-new");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("replacements").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task InvokeAsync_WhenReplacementIsIdentical_ReturnsNoChangeWithoutWriteAuthority()
    {
        var snapshot = new FakeSnapshotReader();
        var replacer = new FakeAtomicFileReplacer();
        var authority = new SequencedSecurityAuthority();

        var result = await CreateTool(snapshot, replacer, authority).InvokeAsync(
            Request(Arguments("old", "old")), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        Status(result).ShouldBe("\"NoChange\"");
        authority.Requests.Count.ShouldBe(1);
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenHostReportsConflict_PreservesTypedFailure()
    {
        var replacer = new FakeAtomicFileReplacer
        {
            Result = new AtomicFileReplaceResult(
                AtomicFileReplaceStatus.Conflict, null, 0, "Changed."),
        };

        var result = await CreateTool(
            new FakeSnapshotReader(), replacer, new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("Changed.");
        Status(result).ShouldBe("\"Conflict\"");
    }

    private static EditTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IAtomicFileReplacer replacer,
        ISecurityAuthority authority) => new(
            snapshotReader,
            replacer,
            authority,
            new SequenceSecurityRequestIdGenerator(),
            new StubMutationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(new EditToolOptions()));

    private static string Arguments(string oldText, string newText, bool replaceAll = false) =>
        JsonSerializer.Serialize(new
        {
            path = "src/a.cs",
            old_text = oldText,
            new_text = newText,
            replace_all = replaceAll,
        });

    private static string Status(ToolInvocationResult result) => Encoding.UTF8.GetString(
        result.Outcome.Extensions.Values["agentkit.edit.status"].CanonicalJson.AsSpan());

    private static ToolInvocationRequest Request(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
