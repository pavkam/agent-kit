// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

/// <summary>Issues exact single-use grants that retain the request's captured authorization and registers them with the grant store the file-system volume consumes from.</summary>
internal sealed class GrantIssuingAuthority(ISecurityGrantStore grants, TimeProvider time): ISecurityAuthority
{
    private static long s_sequence;

    /// <summary>Gets or sets whether requests are denied.</summary>
    internal bool Deny { get; set; }

    /// <summary>Gets the requests evaluated, in order.</summary>
    internal List<SecurityRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Deny)
        {
            return new SecurityDenied(request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied."));
        }

        var now = time.GetUtcNow();
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), request.Id, request.Scope, request.Identity, request.Authorization, request.Audience,
            request.Kind, request.Effect, request.Resources, request.InputFingerprint, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), now.AddDays(-1), now.AddYears(1), 1);
        await grants.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }

    private static Guid NextGuid()
    {
        var sequence = Interlocked.Increment(ref s_sequence);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, sequence);
        bytes[15] = 0x6A;
        return new(bytes);
    }
}
