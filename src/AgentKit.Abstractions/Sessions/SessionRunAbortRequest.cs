// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests an atomic durable abort of one lane's installed accepted run.</summary>
/// <remarks>
/// Abort is scoped by the exact operation, run, operation-state revision, and lane revision it names. A store records
/// the cancel marker only when that evidence matches the lane's installed occupant, so a stale caller cannot abort a
/// different or newer run. The commit prunes pending admissions for that run, advances both revisions, and does not
/// append a session entry. History already committed for the run stays in place.
/// </remarks>
public sealed record SessionRunAbortRequest
{
    /// <summary>Initializes a durable-abort request.</summary>
    /// <param name="context">The lane-bound in-run context of the accepted operation being aborted.</param>
    /// <param name="expectedStateRevision">The positive total-state revision the accepted run must currently carry.</param>
    /// <param name="expectedLaneRevision">The positive lane revision the caller last observed.</param>
    /// <param name="expectedVersion">The canonical whole-session version the caller last observed.</param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="context"/> is not lane-bound and in-run, or <paramref name="idempotencyKey"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="expectedStateRevision"/> or <paramref name="expectedLaneRevision"/> is default.
    /// </exception>
    public SessionRunAbortRequest(
        SessionOperationContext context,
        OperationStateRevision expectedStateRevision,
        SessionLaneRevision expectedLaneRevision,
        SessionVersion expectedVersion,
        IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfSessionContextNotInRun(context);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedStateRevision, default);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedLaneRevision, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Context = context;
        ExpectedStateRevision = expectedStateRevision;
        ExpectedLaneRevision = expectedLaneRevision;
        ExpectedVersion = expectedVersion;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact protected operation context.</summary>
    /// <value>A lane-bound in-run context naming the operation and run this call aborts.</value>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the expected installed total-state revision.</summary>
    /// <value>The positive revision the lane's accepted state must currently carry.</value>
    public OperationStateRevision ExpectedStateRevision { get; }

    /// <summary>Gets the expected lane revision.</summary>
    /// <value>The positive lane revision the caller last observed. A mismatch rejects without mutation.</value>
    public SessionLaneRevision ExpectedLaneRevision { get; }

    /// <summary>Gets the canonical whole-session version the caller last observed.</summary>
    /// <value>The store rejects this abort when the session has since advanced.</value>
    public SessionVersion ExpectedVersion { get; }

    /// <summary>Gets the key that makes repeating this exact request safe.</summary>
    /// <value>A nonblank idempotency identity. An equivalent retry returns the original receipt.</value>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the owning agent.</summary>
    /// <value>The agent from <see cref="Context"/>.</value>
    public AgentId AgentId => Context.AgentId;

    /// <summary>Gets the addressed session.</summary>
    /// <value>The session from <see cref="Context"/>.</value>
    public SessionId SessionId => Context.SessionId;

    /// <summary>Gets the exact execution lane to abort.</summary>
    /// <value>The non-default lane from <see cref="Context"/>.</value>
    public ExecutionLaneId ExecutionLaneId => Context.ExecutionLaneId!.Value;

    /// <summary>Gets the accepted operation being aborted.</summary>
    /// <value>The operation from the in-run correlation.</value>
    public OperationId OperationId => ((InRunOperationCorrelation) Context.Correlation).OperationId;

    /// <summary>Gets the accepted run being aborted.</summary>
    /// <value>The run from the in-run correlation.</value>
    public RunId RunId => ((InRunOperationCorrelation) Context.Correlation).RunId;
}
