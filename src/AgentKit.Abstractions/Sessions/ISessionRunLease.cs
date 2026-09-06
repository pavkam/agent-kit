// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An acquired, exclusive right to perform the single active mutating run
/// for one session. Disposing the lease releases it.
/// </summary>
/// <remarks>
/// A local implementation provides process-local coordination only; it is
/// never a claim of cluster-wide safety. Cross-process ownership requires a
/// durable execution component's fenced, distributed leases. The lease is
/// owned by the caller that acquired it and must be disposed exactly once,
/// normally in a <see langword="using"/> block spanning the run.
/// </remarks>
public interface ISessionRunLease: IAsyncDisposable
{
    /// <summary>Gets the stable identity of this lease.</summary>
    public SessionLeaseId LeaseId { get; }

    /// <summary>Gets the agent that owns the leased session.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session this lease grants exclusive access to.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the run holding this lease.</summary>
    public RunId RunId { get; }
}
