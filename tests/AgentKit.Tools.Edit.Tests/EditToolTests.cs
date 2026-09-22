// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

using AgentKit.TestSupport;

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

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
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
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
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

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[1]")]
    public async Task InvokeAsync_WhenMaximumBytesIsNotANumber_ReturnsInvalidArgumentsWithoutThrowing(string literal)
    {
        // Model-supplied argument values are untrusted input; a wrong JSON kind is InvalidArguments, never an exception.
        var snapshot = new FakeSnapshotReader();
        var replacer = new FakeAtomicFileReplacer();
        var json = $$"""{"path":"src/a.cs","old_text":"old","new_text":"new","maximum_bytes":{{literal}}}""";

        var result = await CreateTool(snapshot, replacer, new SequencedSecurityAuthority()).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        replacer.Requests.ShouldBeEmpty();
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
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
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
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        result.Outcome.FailureReason.ShouldBe("Changed.");
        Status(result).ShouldBe("\"Conflict\"");
    }

    [Fact]
    public void Descriptor_WhenAccessed_MatchesPresentationDescriptor()
    {
        var tool = CreateTool(new FakeSnapshotReader(), new FakeAtomicFileReplacer(), new SequencedSecurityAuthority());

        ((ITool) tool).Descriptor.ShouldBeSameAs(EditTool.PresentationDescriptor);
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotFails_ReturnsInvocationFailedWithoutMutation()
    {
        var snapshot = new FakeSnapshotReader
        {
            Result = new FileSnapshotResult(FileSnapshotStatus.NotFound, [], null, "Not found."),
        };
        var replacer = new FakeAtomicFileReplacer();

        var result = await CreateTool(snapshot, replacer, new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("Not found.");
        replacer.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotDeniedByHost_ReturnsDeniedTerminalStatus()
    {
        var snapshot = new FakeSnapshotReader
        {
            Result = new FileSnapshotResult(FileSnapshotStatus.Denied, [], null, "No symlinks."),
        };

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Outcome.FailureReason.ShouldBe("No symlinks.");
    }

    [Fact]
    public async Task InvokeAsync_WhenSnapshotSucceedsWithoutFingerprint_ReturnsInvocationFailed()
    {
        // A defensive contract check: success without a fingerprint is treated as a failed observation.
        var snapshot = new FakeSnapshotReader
        {
            Result = new FileSnapshotResult(FileSnapshotStatus.Success, [(byte) 'a'], null, null),
        };

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("The file snapshot failed.");
    }

    [Fact]
    public async Task InvokeAsync_WhenContentIsNotStrictUtf8_ReturnsBinaryOrInvalidTextFailure()
    {
        byte[] invalidUtf8 = [0xff, 0xfe, 0x00];
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot(invalidUtf8) };

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("The edit target is not strict UTF-8 text.");
        Status(result).ShouldBe("\"BinaryOrInvalidText\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenContentContainsNulByte_ReturnsBinaryOrInvalidTextFailure()
    {
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("old\0binary") };

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("old", "new")), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("The edit target is binary content.");
        Status(result).ShouldBe("\"BinaryOrInvalidText\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenOldTextNotFound_ReturnsNoMatchFailure()
    {
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("content") };

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(Arguments("missing", "new")), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("The exact old text was not found.");
        Status(result).ShouldBe("\"NoMatch\"");
    }

    [Fact]
    public async Task InvokeAsync_WhenFinalContentExceedsMaximumBytes_ReturnsLimitExceededFailure()
    {
        var snapshot = new FakeSnapshotReader { Result = FakeSnapshotReader.Snapshot("old") };
        var json = JsonSerializer.Serialize(new
        {
            path = "src/a.cs",
            old_text = "old",
            new_text = "much longer replacement text",
            replace_all = false,
            maximum_bytes = 5,
        });

        var result = await CreateTool(snapshot, new FakeAtomicFileReplacer(), new SequencedSecurityAuthority()).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("The final content exceeds the requested complete-file byte bound.");
        Status(result).ShouldBe("\"LimitExceeded\"");
    }

    private static EditTool CreateTool(
        IFileSnapshotReader snapshotReader,
        IAtomicFileReplacer replacer,
        ISecurityAuthority authority) => new(
            snapshotReader,
            replacer, new FixedSecurityAuthoritySelector(authority),
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
        TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
