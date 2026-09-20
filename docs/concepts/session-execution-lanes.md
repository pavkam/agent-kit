# Session execution lanes

**Status:** Normative

**Architecture:** [Sessions](../architecture/sessions.md)

**Depends on:** [Sessions, persistence, and branching](sessions-persistence-and-branching.md),
[Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)

## Purpose

An execution lane is the session's unit of one-active-run ownership. Durable
abort is a session mutation on that lane, not an ambient cancellation token and
not a field of `SessionAcceptedRunState`.

## Durable abort

`AbortRunAsync` names the expected operation, run, operation-state revision, and
lane revision. The store commits only when that evidence matches the lane's
installed accepted run. One commit MUST:

- record a cancel marker for that exact accepted run;
- prune pending admissions that belong to that run;
- advance the lane revision and the operation-state revision together.

A later `LoadRunStateAsync` for that accepted run returns
`SessionRunStateLoaded` with `AbortRequested` true. True means the cancel
marker is committed and that run's pending admissions were pruned in the same
commit. `AbortRequested` defaults to false so recovery of a run that has not
been aborted stays distinguishable. The marker is not added to
`SessionAcceptedRunState`.

Admissions that belong to a different run or lane are not pruned. Already
committed history is not rewritten. An equivalent retry returns
`SessionRunAbortRecorded` and MUST NOT advance the revision again or delete
unrelated admissions.

The store rejects without mutation when the run is not the accepted run, the
expected revision does not match, the session or lane is missing, or
authorization fails the same way as neighboring protected mutations. A missing
or cross-tenant session is masked as not found. Rejection carries
`SessionRunAbortRejectionKind` and a content-free reason; it appends nothing
and prunes nothing.

```csharp
public abstract record SessionRunAbortResult;
public sealed record SessionRunAbortRecorded : SessionRunAbortResult;
public sealed record SessionRunAbortRejected : SessionRunAbortResult;
public enum SessionRunAbortRejectionKind
{
    Unsupported,
    LaneNotFound,
    NoAcceptedRun,
    Fenced,
    SessionVersion,
    Idempotency,
}
```

## Acceptance scenarios

- Aborting the accepted run makes `LoadRunStateAsync` report `AbortRequested`,
  advances the revision, and removes that run's pending admissions.
- Pending admissions for another run or lane remain.
- An equivalent abort retry returns the recorded result and does not advance
  the revision again.
- A stale expected revision or a different run id is `SessionRunAbortRejected`
  and appends or prunes nothing.
- A stale abort cannot cancel a later operation on the same lane.

## Related specifications

- [Sessions, persistence, and branching](sessions-persistence-and-branching.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Input admission and message queues](input-admission-and-message-queues.md)
