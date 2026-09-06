# Usage limits and budgets

**Status:** Normative  
**Depends on:** [Agent loop](agent-loop-state-machine.md),
[model capabilities](model-providers-and-capabilities.md)

## Purpose

Budgets bound cost, time, context, and side effects across the entire run. A
limit is a domain outcome with a measurement source and enforcement boundary.

## Budget dimensions

The runtime MUST support independent limits for:

- model requests and turns/steps;
- input, output, reasoning, cached-read, and cached-write tokens;
- per-request input and requested output tokens;
- cost in a declared currency and pricing revision;
- successful, attempted, and concurrently running tool calls;
- output-validation and tool retry attempts;
- wall-clock run duration and per-operation deadline;
- context bytes/tokens and retained media;
- queued input count, bytes, and age; and
- event, tool-result, retrieval, and buffered-stream sizes.

Limits MAY be absent individually. Unbounded defaults for external cost or
memory dimensions SHOULD be deliberate and visible, never an integer maximum
accident.

## Accounting

`RunUsage` MUST distinguish measured, provider-reported, estimated, and unknown
values. It SHOULD retain per-request and aggregate values plus provider/model
identity. Cost calculation records pricing source/version and whether it is an
estimate.

Cached tokens and reasoning tokens MUST remain separate where providers report
them. A portable `TotalTokens` convenience value MUST document its formula.

Usage updates are monotonic for a run. Provider corrections MAY replace one
request's provisional value, but MUST not double-count streaming increments and
terminal totals.

## Enforcement boundaries

Before a model request, the runtime MUST check request count, elapsed time,
known token/cost consumption, minimum output reserve, and context estimate.
Before a tool batch, it MUST preflight attempted/successful call limits so a
batch that cannot legally start executes none.

Provider output tokens and cost may only be known after response. Post-response
limits therefore stop subsequent work rather than erase received output. A
provider that supports token counting MAY be queried before send, within its own
request/cost budget.

## Reservation

Concurrent operations require atomic reservations. A budget service MUST reserve
capacity before starting work, commit actual usage afterward, and release unused
reservation. Reservations carry run/operation IDs and expire or recover after
failure.

The scheduler MUST NOT check `current < limit` independently in several tasks;
that race is adorable until it charges the card four times.

## Cost limitations

Cost is best effort unless the provider supplies authoritative billing data.
Unknown pricing MUST produce `Unknown`, not zero. A hard cost-only safety policy
SHOULD pair with request/token caps because pricing catalogs can be stale or
unavailable.

Currency conversion is outside the core unless a versioned exchange-rate source
is explicitly configured.

## Limit outcomes

Exhaustion MUST identify limit kind, configured value, observed/reserved value,
enforcement boundary, and whether partial output or side effects exist. It
produces `RunLimitReached`, not a generic exception.

A last-step strategy MAY send one final tools-disabled request with a clear
instruction, provided one request and reserved token/cost capacity remain. It
must be observable as a limit-finalization attempt.

## Hierarchical budgets

Budgets MAY apply at host, tenant, principal, agent, session, run, and operation
scope. Effective capacity is the tightest applicable constraint. Consumption and
reservation must be atomic at every enforced shared scope or delegated to a
single authoritative budget service.

## Acceptance scenarios

- Concurrent request reservations cannot oversubscribe one remaining slot.
- A tool batch over the remaining call limit executes none.
- Provisional and terminal provider usage count exactly once.
- Unknown price is not reported as free.
- Post-response token overflow preserves received output and prevents another
  turn.
- Limit results identify partial side-effect certainty.

## Upstream evidence

- Pydantic AI exposes request, token, successful tool-call, per-request input,
  and cost limits in [usage limits](https://ai.pydantic.dev/usage/).
- Pi's normalized token and cost breakdown appears in
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).
- OpenCode's runner applies step limits and a final no-tools instruction in
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Tool scheduling and concurrency](tool-scheduling-and-concurrency.md)
- [Observability and audit](observability-and-audit.md)
