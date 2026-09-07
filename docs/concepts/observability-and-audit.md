# Observability and audit

**Status:** Normative  
**Depends on:** [Streaming and events](streaming-and-event-protocol.md),
[run lifecycle](run-lifecycle-and-settlement.md)

## Purpose

Observability explains behavior without becoming a hidden control dependency.
Audit proves security-relevant decisions without leaking the data it protects.

## Signals

Signals describe the
[typed streaming and durable event model](streaming-and-event-protocol.md)
without becoming an alternate source of run state.

AgentKit SHOULD emit OpenTelemetry-compatible traces, metrics, and structured
logs plus an independent security audit stream.

Traces describe causal work. Metrics aggregate bounded operational facts. Logs
diagnose exceptional behavior. Audit records permission, approval, tool, memory,
configuration, and administrative decisions with stronger retention and access
controls. One sink MAY carry several signals, but their schemas and guarantees
remain distinct.

## Correlation model

Signals MUST carry applicable tenant, agent, session, conversation, run, turn,
request, message, input, tool-call, deferred-request, goal, and operation IDs.
Trace/span IDs supplement rather than replace domain identities.

Provider request IDs and MCP correlation IDs SHOULD be retained as external
identifiers. They MUST not be mistaken for globally stable AgentKit IDs.

Invocation context is process-local. Concurrent calls on one shared engine,
session, lane, provider catalog, or tool receiver MUST retain independent
telemetry parentage and cancellation. Shared receivers do not discover an
ambient current invocation or serialize trace context into durable business
state.

## Span model

Recommended spans are:

```text
agent.run
  agent.turn
    context.prepare
    model.request
      model.stream
    tool.batch
      tool.call*
    output.validate
  session.commit
  run.settle
```

Queue admission, compaction, retrieval, permission/approval, provider retry, and
durable recovery MAY be linked or nested according to actual causality.

## Required attributes

Stable [error categories](error-taxonomy.md) and
[usage provenance](usage-limits-and-budgets.md) travel as structured attributes
so operators never need to parse message text.

Safe attributes SHOULD include implementation/capability identity, state and
terminal reason, attempt, duration, queue delay, token/cost usage with
provenance, message/tool counts, limit name, provider/model/deployment, retry
decision, error category, and side-effect certainty.

High-cardinality IDs belong on traces/logs and carefully selected metrics, not
every metric label. Raw prompts, model output, tool arguments/results, retrieved
documents, and reasoning are content and disabled by default.

## Redaction and content capture

Content capture MUST be opt-in, policy-controlled, size-bounded, classified, and
redacted before export. Credentials, authorization headers, secrets, and private
keys are never captured. Redaction failure fails closed for the content field
without dropping the structural event.

Reasoning/thinking content may contain sensitive data and provider-restricted
material; it follows a separate capture policy. Hashed fingerprints SHOULD use
keyed or appropriately salted schemes when equality itself is sensitive.

Update checks, installation/update telemetry, product analytics, diagnostics,
security audit, and model/tool content capture are separate consent and
retention domains. Each states its default, destination, stable pseudonymous ID
creation, reset/delete behavior, offline behavior, and exact field allowlist.
Enabling one never opts into another. A coding harness excludes prompts,
workspace paths, session IDs, model payloads, tool inputs/results, reasoning,
and credentials from telemetry by default.

Offline is enforced as network-egress policy across update checks, catalog
refresh, telemetry, remote resources, packages, providers, and network tools. It
is not inferred from one skipped version check. Machine-readable adapters send
diagnostics through protocol frames or stderr and preserve stdout framing.

## Audit records

Audit MUST cover:

- input/session authorization and administrative changes;
- effective policy/configuration version;
- security request, decision, and winning rule;
- approval and grant creation, consumption, expiry, denial, and revocation;
- tool call start/terminal status and effect certainty;
- memory proposal/accept/delete and retrieval authorization;
- MCP/server identity and negotiated security context; and
- recovery, reconciliation, branch, revert, and deletion actions.

Audit records are append-oriented, tamper-evident where required, and use
restricted retention. They contain redacted fingerprints and scopes rather than
unrestricted payloads.

## Observer behavior

Required sink delivery participates in
[run settlement](run-lifecycle-and-settlement.md); best-effort export cannot
mutate the semantic result.

Instrumentation MUST NOT mutate run state. Exporter failure MUST NOT change
semantic results unless the host explicitly marks an audit sink as required.
Required sink failure becomes a settlement failure and must be bounded so it
cannot deadlock shutdown.

Sampling decisions SHOULD preserve failures, permission activity, recovery, and
limit outcomes even when routine successful runs are sampled.

Snapshot/watch publication uses semantic session sequence, not trace order, to
prevent observation gaps. Event-producing commits bind their complete immutable
event batch and emitting invocation context in the continuation that observes
the commit; a later dispatcher MUST NOT rediscover recipients or parentage from
mutable global state.

## Metrics

At minimum, expose run/turn/request duration and outcome, queue depth/age,
provider errors/retries, tool duration/outcome/denial, token/cost usage,
compaction reduction/failure, context size, stream drops/backpressure, and
settlement/recovery delay.

## Acceptance scenarios

- A trace correlates provider and tool work to one run without content capture.
- Enabling safe content capture still redacts seeded secrets.
- Exporter failure cannot corrupt loop state.
- Required audit failure prevents a clean settlement success.
- Metric cardinality tests reject domain IDs as unbounded labels.
- Permission and approval audit can reconstruct the winning decision safely.
- Two concurrent invocations through one receiver retain different trace and
  cancellation lineage.
- Registering a watcher concurrently with a commit yields either the old
  snapshot plus that event batch or the new snapshot, never neither or both.
- Enabling update telemetry leaves product analytics and content capture off.
- Offline policy prevents every configured telemetry destination from sending.

## Related specifications

- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Error taxonomy](error-taxonomy.md)
- [Testing and evaluation](testing-and-evaluation.md)
- [Coding harness execution profile](coding-harness-execution-profile.md)
