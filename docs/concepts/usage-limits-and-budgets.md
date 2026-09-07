# Usage limits and budgets

**Status:** Normative

**Architecture:** [Budgets and limits](../architecture/budgets.md)

**Depends on:** [Agent loop](agent-loop-state-machine.md),
[model capabilities](model-providers-and-capabilities.md)

## Purpose

The first-party implementation boundary is defined by the
[budgets and limits architecture](../architecture/budgets.md).

Budgets bound cost, time, context, and side effects across the entire run. A
limit is a domain outcome with a measurement source and enforcement boundary.

## Budget dimensions

`BudgetDimension` is an extensible validated value, not a closed enum. AgentKit
reserves the `agentkit.*` namespace; third-party dimensions MUST use their own
stable namespace and register a descriptor declaring aggregation semantics and
legal units. Unknown dimensions, incompatible units, and conflicting descriptors
fail composition rather than becoming counters with guessed semantics.

The first-party catalog MUST support independent limits for:

- model requests, concurrent model requests, and turns/steps;
- input, output, reasoning, cached-read, and cached-write tokens;
- per-request input and requested output tokens;
- cost in a declared currency and pricing revision;
- successful, attempted, and concurrently running tool calls;
- output-validation and tool retry attempts;
- total and concurrently active delegations;
- elapsed run duration and per-operation deadline;
- context bytes/tokens and retained media;
- queued input count, bytes, and age; and
- event, final-result, tool-result, artifact, retrieval item/byte, and
  buffered-stream sizes.

Limits MAY be absent individually. Unbounded defaults for external cost or
memory dimensions SHOULD be deliberate and visible, never an integer maximum
accident.

## Accounting

`RunUsage` MUST distinguish measured, provider-reported, estimated, and unknown
values. It SHOULD retain per-request and aggregate values plus provider/model
identity so [observability](observability-and-audit.md) can report provenance
without reconstructing it. Cost calculation records pricing source/version and
whether it is an estimate.

Cached tokens and reasoning tokens MUST remain separate where providers report
them. A portable `TotalTokens` convenience value MUST document its formula.

Usage ledger order and adjustment versions are monotonic. Corrected numeric
totals may decrease when authoritative evidence replaces an estimate. Each
correction MUST name its original attempt and prior accounting version; it MUST
NOT double-count streaming increments, terminal totals, or repeated delivery.

Durable session usage is an append-only ledger, not a field reconstructed from
the surviving transcript or current operation state. It records every settled
provider attempt—including failed, retried, cancelled, and synthetic
settlements—with provider-native counters, response/entry identity when one
exists, and adjustment provenance. Terminal cleanup and conversation compaction
do not delete billing evidence; recovery never treats a usage row as proof of an
operation transition.

## Enforcement boundaries

Before a [model request](provider-request-pipeline.md), the runtime MUST check
request count, elapsed time, known token/cost consumption, minimum output
reserve, and context estimate. Before a
[tool batch](tool-scheduling-and-concurrency.md), it MUST preflight attempted
and successful call limits so a batch that cannot legally start executes none.

Provider output tokens and cost may only be known after response. Post-response
limits therefore stop subsequent work rather than erase received output. A
provider that supports token counting MAY be queried before send, within its own
request/cost budget.

## Reservation

Concurrent operations require atomic reservations. A budget service MUST reserve
capacity before starting work, mark the reservation started before the effect,
commit actual usage afterward, and release only proven unused capacity.
Reservations carry run/operation IDs. Expiry and disposal release an unstarted
reservation; a started reservation with unknown usage MUST remain unresolved or
be conservatively charged with explicit estimated provenance until reconciled.
Process loss is not evidence that no money or tokens were consumed.

An indivisible batch MUST reserve every required dimension across its shared
ancestors atomically or reserve none. A sequence of independently successful
single-dimension checks is insufficient. Each charged dimension has one owner;
outer preflight capacity is transferred or subdivided rather than charged again
by the lower executor.

Actual overrun MUST be fully recorded even when it exceeds a hard ceiling. It
sets remaining capacity to zero and stops new work; the ledger MUST NOT clamp
reported consumption or reject its truthful accounting.

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

The budget authority, ledger, and immutable profile, policy, and dimension
catalogs are shared thread-safe services for one engine process. Tenant, agent,
session, run, and operation scope are ledger addresses, not separate authority
instances. `IRunBudget`, child scopes, and reservations are short-lived owned
handles created by that authority; they MUST NOT be captured by singleton
consumers. Profiles and policy implementations are keyed and replaceable only
through an explicit same-key replacement registration.

Consumers receive an invocation-only budget capability containing the exact
profile version, authenticated execution identity, operation correlation, and
`IBudgetScope`. They MUST validate that binding, reserve before every attempted
effect, and settle it or transfer unresolved accounting before returning.
Out-of-run embedding, reranking, maintenance, or delegation work receives a
child operation scope; it MUST NOT inject or fabricate an `IRunBudget`.

The capability's address MUST match the authenticated tenant and principal and
the exact operation correlation. An `InRun` binding also matches the active run.
`BeforeRun` and `AfterRun` bindings have no active run in their address.
`AfterRun.CausalRunId` records causality only: subsequent work reserves from a
new operation child scope under an authorized non-run parent. It MUST NOT reopen
the settled run's budget. Late corrections retain the original reservation's
identity and accounting history; they do not authorize new effects. The scope
creator owns its lifetime; consumers borrow the capability for one invocation.

## Acceptance scenarios

- Concurrent request reservations cannot oversubscribe one remaining slot.
- A tool batch over the remaining call limit executes none.
- Provisional and terminal provider usage count exactly once.
- Unknown price is not reported as free.
- Post-response token overflow preserves received output and prevents another
  turn.
- Limit results identify partial side-effect certainty.
- A failed first provider attempt and successful retry produce two attributable
  ledger rows but one correctly aggregated run total.
- Fork usage is copied or reset only through an explicit policy.

- Crash after provider send retains unknown spend and cannot reopen capacity.
- A batch rejected on its last dimension starts no effects and reserves nothing.
- A downward correction changes accounting once without rewinding ledger order.
- Actual overrun is fully recorded and blocks subsequent reservations.
- After-run maintenance cannot obtain fresh capacity from a settled causal run.
- A capability with a different tenant, principal, operation, or active run is
  rejected before a reservation or effect.

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Tool scheduling and concurrency](tool-scheduling-and-concurrency.md)
- [Observability and audit](observability-and-audit.md)
