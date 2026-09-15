// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic release of one lane's installed accepted run state, freeing the lane for a later start.</summary>
/// <remarks>
/// Release is scoped by the exact operation and run it names: a store only clears a lane whose installed
/// <see cref="SessionAcceptedRunState.Correlation"/> and <see cref="SessionAcceptedRunState.OperationStateRevision"/>
/// match this request's evidence, so a stale caller — for example a lease left over from a superseded attempt —
/// can never release a different, newer occupant of the same lane. Release appends no session entry by default;
/// it only clears the lane's process-independent durable ownership marker and advances the canonical whole-session
/// version by one.
/// </remarks>
public sealed record SessionRunReleaseRequest
{
    /// <summary>Initializes a lane-release request.</summary>
    /// <param name="context">The lane-bound in-run context of the accepted operation being released.</param>
    /// <param name="expectedStateRevision">The positive total-state revision installed by the acceptance this call releases.</param>
    /// <param name="expectedVersion">The canonical whole-session version the caller last observed.</param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe: a retry with the same key returns the original result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="context"/> is not lane-bound and in-run, or <paramref name="idempotencyKey"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedStateRevision"/> is default.</exception>
    public SessionRunReleaseRequest(
        SessionOperationContext context,
        OperationStateRevision expectedStateRevision,
        SessionVersion expectedVersion,
        IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfSessionContextNotInRun(context);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStateRevision, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        ExpectedStateRevision = expectedStateRevision;
        ExpectedVersion = expectedVersion;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact protected operation context.</summary>
    /// <value>A lane-bound in-run context naming the operation and run this call releases.</value>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the expected installed total-state revision.</summary>
    /// <value>The positive revision the lane's accepted state must currently carry.</value>
    public OperationStateRevision ExpectedStateRevision { get; }

    /// <summary>Gets the canonical whole-session version the caller last observed.</summary>
    /// <value>The store rejects this release with a conflict if the session has since advanced.</value>
    public SessionVersion ExpectedVersion { get; }

    /// <summary>Gets the key that makes repeating this exact request safe.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the owning agent.</summary><value>The agent from <see cref="Context"/>.</value>
    public AgentId AgentId => Context.AgentId;

    /// <summary>Gets the addressed session.</summary><value>The session from <see cref="Context"/>.</value>
    public SessionId SessionId => Context.SessionId;

    /// <summary>Gets the exact execution lane to release.</summary><value>The non-default lane from <see cref="Context"/>.</value>
    public ExecutionLaneId ExecutionLaneId => Context.ExecutionLaneId!.Value;

    /// <summary>Gets the accepted operation being released.</summary><value>The operation from the in-run correlation.</value>
    public OperationId OperationId => ((InRunOperationCorrelation) Context.Correlation).OperationId;

    /// <summary>Gets the accepted run being released.</summary><value>The run from the in-run correlation.</value>
    public RunId RunId => ((InRunOperationCorrelation) Context.Correlation).RunId;
}
