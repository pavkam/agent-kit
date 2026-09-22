// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Starts one authorized process and returns an operation-owned handle.</summary>
public interface IProcessExecutor
{
    /// <summary>Gets the sole component audience permitted to consume process-start grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Revalidates authority and canonical facts, then starts one process when allowed.</summary>
    /// <param name="request">The resolved start request.</param>
    /// <param name="grant">The exact single-use start authority.</param>
    /// <param name="cancellationToken">Cancels start work before creation when possible.</param>
    /// <returns>A closed start outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="grant"/> is null.</exception>
    public ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken = default);
}
