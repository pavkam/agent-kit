// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Pairs one exact file read request with its resolved target and single-use
/// grant.
/// </summary>
/// <remarks>
/// The effecting reader recomputes enforcement evidence immediately before
/// opening the target and atomically consumes the grant.
/// </remarks>
public sealed record AuthorizedFileRead
{
    /// <summary>Initializes a new instance of the <see cref="AuthorizedFileRead"/> record.</summary>
    /// <param name="request">The immutable read request.</param>
    /// <param name="resolvedTarget">The resolved target bound by authorization.</param>
    /// <param name="grant">The bounded grant issued for this exact read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedFileRead(FileReadRequest request, ResolvedFileTarget resolvedTarget, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        Request = request;
        ResolvedTarget = resolvedTarget;
        Grant = grant;
    }

    /// <summary>Gets the immutable read request.</summary>
    public FileReadRequest Request { get; init; }

    /// <summary>Gets the resolved target bound by authorization.</summary>
    public ResolvedFileTarget ResolvedTarget { get; init; }

    /// <summary>Gets the bounded grant issued for this exact read.</summary>
    public SecurityGrant Grant { get; init; }
}
