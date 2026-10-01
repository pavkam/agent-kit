// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Issues exact single-use grants that retain the request's captured authorization, or denies on demand.</summary>
/// <remarks>Issued grants are registered with the grant store a real store consumes from, so end-to-end tests exercise the same enforcement a production composition would.</remarks>
internal sealed class ScriptedSecurityAuthority(ISecurityGrantStore? grants = null): ISecurityAuthority
{
    private static long s_sequence;

    /// <summary>Gets or sets whether requests are denied.</summary>
    internal bool Deny { get; set; }

    /// <summary>Gets the requests evaluated, in order.</summary>
    internal List<SecurityRequest> Requests { get; } = [];

    /// <summary>Gets the grants issued, in order.</summary>
    internal List<SecurityGrant> IssuedGrants { get; } = [];

    /// <summary>Gets or sets an action invoked when a request is evaluated, before any decision.</summary>
    internal Func<SecurityRequest, Task>? OnAuthorize { get; set; }

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (OnAuthorize is { } hook)
        {
            await hook(request).ConfigureAwait(false);
        }

        if (Deny)
        {
            return new SecurityDenied(request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied."));
        }

        var grant = new SecurityGrant(
            new GrantId(NextGuid()), request.Id, request.Scope, request.Identity, request.Authorization, request.Audience,
            request.Kind, request.Effect, request.Resources, request.InputFingerprint, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), ArtifactTestData.Now.AddDays(-1), ArtifactTestData.Now.AddYears(1), 1);
        IssuedGrants.Add(grant);
        if (grants is not null)
        {
            await grants.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }

    private Guid NextGuid()
    {
        var sequence = Interlocked.Increment(ref s_sequence);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, sequence);
        bytes[15] = 0x7A;
        return new(bytes);
    }
}
