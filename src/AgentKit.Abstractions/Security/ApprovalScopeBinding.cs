// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures every grant-relevant field approved for one normalized security request.</summary>
public sealed record ApprovalScopeBinding
{
    /// <summary>Initializes an exact approval binding.</summary>
    /// <param name="request">The normalized request whose immutable fields are captured.</param>
    /// <param name="policyVersion">The evaluated policy version.</param>
    /// <param name="revocationVersion">The captured revocation epoch.</param>
    /// <param name="notBefore">The first permitted grant instant.</param>
    /// <param name="expiresAt">The exclusive approval and grant expiry.</param>
    /// <param name="allowedUses">The positive approved use bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="allowedUses"/> is not positive or exceeds the request, or <paramref name="expiresAt"/> exceeds the request deadline.</exception>
    /// <exception cref="ArgumentException"><paramref name="expiresAt"/> is not later than <paramref name="notBefore"/>.</exception>
    public ApprovalScopeBinding(SecurityRequest request, SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion, DateTimeOffset notBefore, DateTimeOffset expiresAt,
        int allowedUses)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allowedUses);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(allowedUses, request.RequestedUses);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, notBefore);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expiresAt, request.Deadline);
        Request = request;
        PolicyVersion = policyVersion;
        RevocationVersion = revocationVersion;
        NotBefore = notBefore;
        ExpiresAt = expiresAt;
        AllowedUses = allowedUses;
    }

    /// <summary>Gets the complete normalized request evidence.</summary>
    public SecurityRequest Request { get; }
    /// <summary>Gets the evaluated policy version.</summary>
    public SecurityPolicyVersion PolicyVersion { get; }
    /// <summary>Gets the captured revocation epoch.</summary>
    public SecurityRevocationVersion RevocationVersion { get; }
    /// <summary>Gets the first valid instant.</summary>
    public DateTimeOffset NotBefore { get; }
    /// <summary>Gets the exclusive expiry.</summary>
    public DateTimeOffset ExpiresAt { get; }
    /// <summary>Gets the approved use bound.</summary>
    public int AllowedUses { get; }

    /// <summary>Compares complete request evidence structurally, including ordered canonical resources.</summary>
    /// <param name="other">The binding to compare.</param>
    /// <returns><see langword="true"/> only when every authority-bearing field matches.</returns>
    public bool Equals(ApprovalScopeBinding? other) =>
        other is not null
        && RequestsEqual(Request, other.Request)
        && PolicyVersion == other.PolicyVersion
        && RevocationVersion == other.RevocationVersion
        && NotBefore == other.NotBefore
        && ExpiresAt == other.ExpiresAt
        && AllowedUses == other.AllowedUses;

    /// <summary>Returns a structural hash consistent with exact binding equality.</summary>
    /// <returns>A hash derived from every authority-bearing field and ordered resource.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Request.Id);
        hash.Add(Request.Scope);
        hash.Add(Request.ToolCallId);
        hash.Add(Request.Identity);
        hash.Add(Request.Authorization);
        hash.Add(Request.Audience);
        hash.Add(Request.Kind);
        hash.Add(Request.Effect);
        foreach (var resource in Request.Resources)
        {
            hash.Add(resource);
        }
        hash.Add(Request.InputFingerprint);
        hash.Add(Request.Deadline);
        hash.Add(Request.RequestedUses);
        hash.Add(PolicyVersion);
        hash.Add(RevocationVersion);
        hash.Add(NotBefore);
        hash.Add(ExpiresAt);
        hash.Add(AllowedUses);
        return hash.ToHashCode();
    }

    private static bool RequestsEqual(SecurityRequest left, SecurityRequest right) =>
        left.Id == right.Id
        && left.Scope == right.Scope
        && left.ToolCallId == right.ToolCallId
        && left.Identity == right.Identity
        && left.Authorization == right.Authorization
        && left.Audience == right.Audience
        && left.Kind == right.Kind
        && left.Effect == right.Effect
        && left.Resources.AsSpan().SequenceEqual(right.Resources.AsSpan())
        && left.InputFingerprint == right.InputFingerprint
        && left.Deadline == right.Deadline
        && left.RequestedUses == right.RequestedUses;
}
