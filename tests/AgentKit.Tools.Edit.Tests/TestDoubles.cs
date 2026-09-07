// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

internal sealed class FakeSnapshotReader: IFileSnapshotReader
{
    public ComponentId SecurityAudience { get; } = new("test.snapshot");
    public List<FileSnapshotRequest> Requests { get; } = [];
    public FileSnapshotResult Result { get; set; } = Snapshot("old");

    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Result);
    }

    public static FileSnapshotResult Snapshot(string text) => Snapshot(Encoding.UTF8.GetBytes(text));

    public static FileSnapshotResult Snapshot(byte[] bytes)
    {
        var content = ImmutableArray.CreateRange(bytes);
        return new FileSnapshotResult(
            FileSnapshotStatus.Success,
            content,
            FileSecurityBinding.ContentFingerprint(content.AsSpan()),
            null);
    }
}

internal sealed class FakeAtomicFileReplacer: IAtomicFileReplacer
{
    public ComponentId SecurityAudience { get; } = new("test.replacer");
    public List<AtomicFileReplaceRequest> Requests { get; } = [];
    public AtomicFileReplaceResult? Result { get; set; }

    public ValueTask<AtomicFileReplaceResult> ReplaceAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Result ?? new AtomicFileReplaceResult(
            AtomicFileReplaceStatus.Committed,
            FileSecurityBinding.ContentFingerprint(request.Content.AsSpan()),
            request.Content.Length,
            null));
    }
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

    public SecurityRequestId Create() => new(Guid.Parse($"20000000-0000-0000-0000-{++_value:D12}"));
}

internal sealed class StubMutationIdGenerator: IIdentifierGenerator<WorkspaceMutationId>
{
    public WorkspaceMutationId Create() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
