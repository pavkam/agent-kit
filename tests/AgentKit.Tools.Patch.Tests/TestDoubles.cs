// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;

internal sealed class FakeSnapshotReader: IFileSnapshotReader
{
    public ComponentId SecurityAudience { get; } = new("test.snapshot");
    public List<FileSnapshotRequest> Requests { get; } = [];
    public Dictionary<string, FileSnapshotResult> Results { get; } = new(StringComparer.Ordinal);

    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Results.TryGetValue(request.Path.Value, out var result)
            ? result
            : Missing());
    }

    public static FileSnapshotResult Snapshot(string text) => Snapshot(Encoding.UTF8.GetBytes(text));

    public static FileSnapshotResult Snapshot(byte[] bytes)
    {
        ImmutableArray<byte> content = [.. bytes];
        return new FileSnapshotResult(
            FileSnapshotStatus.Success,
            content,
            FileSecurityBinding.ContentFingerprint(content.AsSpan()),
            null);
    }

    public static FileSnapshotResult Missing() => new(
        FileSnapshotStatus.NotFound, [], null, "Missing.");
}

internal sealed class FakePatchApplier: IWorkspacePatchApplier
{
    public ComponentId SecurityAudience { get; } = new("test.patch");
    public List<WorkspacePatchRequest> Requests { get; } = [];
    public WorkspacePatchResult? Result { get; set; }

    public ValueTask<WorkspacePatchResult> ApplyPatchAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Result is not null)
        {
            return ValueTask.FromResult(Result);
        }

        var entries = request.Entries.Select(static (entry, index) => new WorkspacePatchEntryResult(
            index,
            entry.Kind,
            WorkspacePatchEntryStatus.Committed,
            Source(entry),
            entry is WorkspacePatchMove move ? move.DestinationPath : null,
            Fingerprint(entry),
            null)).ToImmutableArray();
        return ValueTask.FromResult(new WorkspacePatchResult(
            entries.Length == 1
                ? WorkspacePatchStatus.AtomicCommitted
                : WorkspacePatchStatus.CommittedWithNonAtomicVisibility,
            entries,
            null));
    }

    private static FileSystemPath Source(WorkspacePatchEntry entry) => entry switch
    {
        WorkspacePatchCreate create => create.Path,
        WorkspacePatchReplace replace => replace.Path,
        WorkspacePatchDelete delete => delete.Path,
        WorkspacePatchMove move => move.SourcePath,
        _ => throw new InvalidOperationException(),
    };

    private static ContentHash? Fingerprint(WorkspacePatchEntry entry) => entry switch
    {
        WorkspacePatchCreate create => FileSecurityBinding.ContentFingerprint(create.Content.AsSpan()),
        WorkspacePatchReplace replace => FileSecurityBinding.ContentFingerprint(replace.Content.AsSpan()),
        WorkspacePatchMove move => move.ExpectedContentFingerprint,
        _ => null,
    };
}

internal sealed class SequencedSecurityAuthority(int? denyAt = null): ISecurityAuthority
{
    public List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Requests.Count == denyAt)
        {
            return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")));
        }

        var now = DateTimeOffset.UnixEpoch;
        return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
            request.Id,
            new SecurityPolicyVersion(1),
            new SecurityGrant(
                new GrantId(Guid.NewGuid()),
                request.Id,
                request.Scope,
                request.Identity,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                new SecurityPolicyVersion(1),
                new SecurityRevocationVersion(1),
                now,
                now.AddMinutes(5),
                1)));
    }
}

internal sealed class SequenceSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    private int _value;

    public SecurityRequestId Create() => new(Guid.Parse($"21000000-0000-0000-0000-{++_value:D12}"));
}

internal sealed class SequenceMutationIdGenerator: IIdentifierGenerator<WorkspaceMutationId>
{
    private int _value;

    public WorkspaceMutationId Create() => new(Guid.Parse($"31000000-0000-0000-0000-{++_value:D12}"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
