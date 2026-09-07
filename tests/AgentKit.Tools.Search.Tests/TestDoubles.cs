// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search.Tests;

internal sealed class FakeFileContentSearcher: IFileContentSearcher
{
    public ComponentId SecurityAudience { get; } = new("test.searcher");
    public List<FileSearchRequest> Requests { get; } = [];
    public FileSearchResult Result { get; set; } = new(FileSearchStatus.NoMatches, [], 0, 0, true, null);

    public ValueTask<FileSearchResult> SearchAsync(
        FileSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Result);
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    public List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (!allow)
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
                now,
                now.AddMinutes(5),
                1)));
    }
}

internal sealed class StubSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
