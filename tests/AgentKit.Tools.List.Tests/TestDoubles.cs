// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

/// <summary>A scripted <see cref="IDirectoryReader"/> that serves one fixed listing, or fails with a configured exception.</summary>
internal sealed class FakeDirectoryReader: IDirectoryReader
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.directory");

    /// <summary>Gets every authorized enumeration this reader was asked to perform, in order.</summary>
    public List<AuthorizedDirectoryEnumeration> Operations { get; } = [];

    /// <summary>Gets or sets the entry names served, in the order served.</summary>
    public List<(string Name, bool IsDirectory)> Entries { get; set; } = [];

    /// <summary>Gets or sets an exception thrown from the enumerator instead of serving entries.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Operations.Add(operation);
        await Task.Yield();
        if (Failure is not null)
        {
            throw Failure;
        }

        foreach (var (name, isDirectory) in Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FileSystemEntry(new NormalizedRelativePath(name), isDirectory);
        }
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
                request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied.")));
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
                request.Authorization,
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
