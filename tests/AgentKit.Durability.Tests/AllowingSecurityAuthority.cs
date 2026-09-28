// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Issues one single-use grant for every request, echoing back the request's own exact binding.</summary>
/// <remarks>
/// The authority mirrors the request instead of applying policy, so a test can assert that the coordinator asked for
/// exactly the audience, kind, effect, resources, and digest the journal will later recompute. Policy evaluation
/// itself belongs to the permissions package and is tested there.
/// </remarks>
internal sealed class AllowingSecurityAuthority: ISecurityAuthority
{
    private readonly List<SecurityRequest> _requests = [];
    private long _nextIdentity;

    /// <summary>Gets every request this authority evaluated, in order.</summary>
    /// <value>A live list of the exact normalized requests presented.</value>
    internal IReadOnlyList<SecurityRequest> Requests => _requests;

    /// <summary>Gets or sets a denial reason that replaces the allow decision.</summary>
    /// <value>Null to allow; content-free text to deny every request.</value>
    internal string? DenyReason { get; set; }

    /// <inheritdoc/>
    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        // A durability request always carries its capture; a request without one is a composition bug, not a denial.
        ArgumentNullException.ThrowIfNull(request.Authorization, nameof(request));
        var policyVersion = request.Authorization.PolicySnapshot.Version;
        if (DenyReason is { } reason)
        {
            return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                policyVersion,
                new SecurityDenial("test.denied", reason)));
        }

        var grant = new SecurityGrant(
            new GrantId(NextGuid()),
            request.Id,
            request.Scope,
            request.Identity,
            request.Authorization,
            request.Audience,
            request.Kind,
            request.Effect,
            request.Resources,
            request.InputFingerprint,
            policyVersion,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            request.Deadline,
            allowedUses: 1);
        return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(request.Id, policyVersion, grant));
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 2;
        return new Guid(bytes);
    }
}
