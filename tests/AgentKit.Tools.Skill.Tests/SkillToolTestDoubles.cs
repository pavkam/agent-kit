// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

internal sealed class RecordingSnapshotReader: IFileSnapshotReader
{
    internal List<FileSnapshotRequest> Requests { get; } = [];
    internal FileSnapshotResult Result { get; set; } = Success("content");

    public ComponentId SecurityAudience { get; } = new("test.skill.reader");

    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        var matches = request.Grant.Audience == SecurityAudience
            && request.Grant.Kind == SecurityOperationKind.FileRead
            && request.Grant.Effect == SecurityEffect.Observe
            && request.Grant.Resources.SequenceEqual([FileSecurityBinding.Resource(request.Path)])
            && request.Grant.InputFingerprint == FileSecurityBinding.SnapshotFingerprint(request.Path, request.MaximumBytes);
        return ValueTask.FromResult(matches
            ? Result
            : new FileSnapshotResult(FileSnapshotStatus.Denied, [], null, "Grant mismatch."));
    }

    internal static FileSnapshotResult Success(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content).ToImmutableArray();
        return new FileSnapshotResult(
            FileSnapshotStatus.Success,
            bytes,
            FileSecurityBinding.ContentFingerprint(bytes.AsSpan()),
            null);
    }
}

internal sealed class RecordingSecurityAuthority: ISecurityAuthority
{
    internal bool Allow { get; set; } = true;
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (!Allow)
        {
            return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")));
        }

        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
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
            DateTimeOffset.UnixEpoch,
            request.Deadline,
            1);
        return ValueTask.FromResult<SecurityDecision>(
            new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant));
    }
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
