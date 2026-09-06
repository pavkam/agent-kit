# Observability

**Role:** Explain behavior and prove security decisions without controlling the
runtime or leaking protected content.

Observability includes domain events, traces, metrics, structured logs, and a
separate security audit stream. These signals may share exporters, but their
schemas, retention, access, and delivery guarantees remain distinct.

Event and audit contracts live in AgentKit.Abstractions. Runtime components emit
through those contracts without depending on an exporter. A first-party
OpenTelemetry bridge belongs in AgentKit.Observability.OpenTelemetry; other
exporters remain independent leaf packages.

## Domain events

Durable semantic events are sufficient to reconstruct stable session and run
state. Live events describe provisional streaming, progress, and diagnostics.
Only the former are required for replay. Both carry stable domain correlation
instead of relying on formatted text.

Applicable signals correlate tenant, agent, session, conversation, run, turn,
request, message, input, tool call, deferred request, goal, and durable
operation. Provider request and MCP protocol identities are retained as external
correlation, not substituted for AgentKit identities.

## Telemetry

Traces describe causal work across context preparation, model requests, tool
batches, validation, persistence, and settlement. Metrics report bounded
aggregates such as latency, outcome, queue depth, retries, denial, token and
cost usage, context pressure, stream loss, compaction, and recovery delay. Logs
carry structured diagnostic facts and normalized error categories.

Domain errors have stable categories, retry advice, origin, safe message,
external correlation, and side-effect certainty. Provider or implementation
details remain diagnostics; callers and models do not parse raw exception text
to decide behavior.

## Audit

Audit records authorization, permission rules, approvals, tool effects, memory
changes, configuration changes, MCP security context, recovery, branches,
reverts, and deletion. Records are append-oriented and redacted, with stronger
retention and access controls than routine telemetry.

Content capture is disabled by default. When enabled, prompts, output, tool
arguments, retrieval, and reasoning are separately classified, bounded,
policy-controlled, and redacted before export. Credentials and private keys are
never captured. Redaction failure removes the content field rather than the
structural event.

## Delivery and influence

Observers receive immutable data and cannot mutate run state. Best-effort sink
failure is isolated. A host may mark a durable event or audit sink as required;
failure then prevents clean settlement but remains bounded so shutdown cannot
deadlock.

Every interchangeable component is verified by a shared conformance suite.
Diagnostics expose behavior for those tests, but testability does not create
backdoors into private state.

AgentKit.Evaluation consumes public engine results, events, and recorded
manifests. It may export evaluation metrics through observability contracts, but
an evaluator is not an observer with secret access to mutable runtime state.

## Related concept specifications

- [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)
- [Observability and audit](../concepts/observability-and-audit.md)
- [Error taxonomy](../concepts/error-taxonomy.md)
- [Testing and evaluation](../concepts/testing-and-evaluation.md)
- [Testing and evaluation architecture](17-testing-and-evaluation.md)
