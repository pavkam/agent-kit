// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests process-local ownership for one exact durably accepted session-lane operation.</summary>
/// <remarks>The request carries immutable canonical state evidence. Acquisition revalidates that evidence through the protected session coordinator after obtaining the local lane slot.</remarks>
public sealed record SessionRunLeaseRequest
{
    /// <summary>Initializes an exact lane-ownership request.</summary>
    /// <param name="context">The lane-bound in-run context of the accepted operation.</param>
    /// <param name="expectedStateRevision">The positive total-state revision observed by the caller.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="context"/> is not lane-bound and in-run.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedStateRevision"/> is default.</exception>
    public SessionRunLeaseRequest(SessionOperationContext context, OperationStateRevision expectedStateRevision)
    {
        ArgumentException.ThrowIfSessionContextNotInRun(context);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStateRevision, default);
        Context = context;
        ExpectedStateRevision = expectedStateRevision;
    }

    /// <summary>Gets the exact protected operation context.</summary><value>A lane-bound in-run context retained by accepted state.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the expected total-state revision.</summary><value>A positive revision revalidated before lease acquisition completes.</value>
    public OperationStateRevision ExpectedStateRevision { get; }
    /// <summary>Gets the owning agent.</summary><value>The agent from <see cref="Context"/>.</value>
    public AgentId AgentId => Context.AgentId;
    /// <summary>Gets the addressed session.</summary><value>The session from <see cref="Context"/>.</value>
    public SessionId SessionId => Context.SessionId;
    /// <summary>Gets the exact execution lane.</summary><value>The non-default lane from <see cref="Context"/>.</value>
    public ExecutionLaneId ExecutionLaneId => Context.ExecutionLaneId!.Value;
    /// <summary>Gets the accepted operation.</summary><value>The operation from the in-run correlation.</value>
    public OperationId OperationId => ((InRunOperationCorrelation) Context.Correlation).OperationId;
    /// <summary>Gets the accepted run.</summary><value>The run from the in-run correlation.</value>
    public RunId RunId => ((InRunOperationCorrelation) Context.Correlation).RunId;
}
