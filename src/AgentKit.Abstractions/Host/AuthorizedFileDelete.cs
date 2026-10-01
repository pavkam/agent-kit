// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one authorized file deletion to its resolved target, optional target-state precondition, and grant.</summary>
/// <remarks>The expected fingerprint, when present, is a commit precondition: a racing change produces a conflict instead of a deletion of different content. The implementation evaluates it against the bytes it actually observes at commit.</remarks>
public sealed record AuthorizedFileDelete
{
    /// <summary>Initializes authorized file deletion evidence.</summary>
    /// <param name="resolvedTarget">The resolved file target bound by authorization.</param>
    /// <param name="expectedTargetFingerprint">The fingerprint the target must still have, or <see langword="null"/> when any regular file at the target may be removed.</param>
    /// <param name="grant">The bounded grant issued for this exact deletion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedFileDelete(ResolvedFileTarget resolvedTarget, ContentHash? expectedTargetFingerprint, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedTarget = resolvedTarget;
        ExpectedTargetFingerprint = expectedTargetFingerprint;
        Grant = grant;
    }

    /// <summary>Gets the resolved file target bound by authorization.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the fingerprint the target must still have, or <see langword="null"/> when none is required.</summary>
    public ContentHash? ExpectedTargetFingerprint { get; init; }

    /// <summary>Gets the bounded deletion grant.</summary>
    public SecurityGrant Grant { get; init; }
}
