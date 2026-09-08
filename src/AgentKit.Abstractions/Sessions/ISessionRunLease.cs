// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An acquired right to drive one exact accepted operation in one tenant-partitioned execution lane.
/// </summary>
/// <remarks>
/// The lease excludes competing drivers only for its exact tenant, session
/// address, and lane. Other lanes may drive concurrently, while durable writes
/// still pass through the session coordinator's separate mutation line. This
/// value carries no authorization or security grant. A local lease provides no
/// cluster-wide safety; cross-process ownership requires an enforced fence. The
/// acquiring caller owns disposal, normally through <see langword="await using"/>,
/// and stale or repeated disposal must not release a successor.
/// </remarks>
public interface ISessionRunLease: IAsyncDisposable
{
    /// <summary>Gets the stable identity of this ownership attempt.</summary>
    /// <value>A non-default identity used for exact release linearization.</value>
    public SessionLeaseId LeaseId { get; }

    /// <summary>Gets the tenant partition that owns the leased session address.</summary>
    /// <value>The immutable, nonempty trusted-ingress tenant identity revalidated during acquisition.</value>
    public TenantId TenantId { get; }

    /// <summary>Gets the agent that owns the leased session.</summary>
    /// <value>The non-default agent component of the accepted session address.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the session containing the leased lane.</summary>
    /// <value>The non-default session component of the accepted address; it is not globally unique without tenant and agent.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the execution lane exclusively driven by this lease.</summary>
    /// <value>The non-default lane identity whose other drivers are excluded.</value>
    public ExecutionLaneId ExecutionLaneId { get; }

    /// <summary>Gets the accepted operation holding this lease.</summary>
    /// <value>The non-default operation identity revalidated before acquisition.</value>
    public OperationId OperationId { get; }

    /// <summary>Gets the open run holding this lease.</summary>
    /// <value>The non-default run identity retained from accepted state; a new drive does not create a new run.</value>
    public RunId RunId { get; }

    /// <summary>Gets the accepted total-state revision revalidated during acquisition.</summary>
    /// <value>The positive revision against which the first drive transition must compare-and-swap.</value>
    public OperationStateRevision StateRevision { get; }

    /// <summary>Gets an optional distributed fence supplied by a replacing coordinator.</summary><value><see langword="null"/> for the process-local implementation.</value>
    public FencingToken? Fence { get; }
}
