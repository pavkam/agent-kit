---
name: agentkit-observability
description:
  "Design, implement, or debug AgentKit events, traces, metrics, logs,
  redaction, audit delivery, and exporter adapters. Use for observation and
  diagnostics; not runtime control or semantic state."
---

# AgentKit Observability

Read [AGENTS.md](../../../AGENTS.md), the
[observability architecture](../../../docs/architecture/observability.md), and
the
[normative observability specification](../../../docs/concepts/observability-and-audit.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Boundary

- Emit first-party routine telemetry through `Microsoft.Extensions.Logging` and
  `System.Diagnostics.ActivitySource`/`Meter`, using the stable shared names
  from AgentKit.Observability. Use source-generated `LoggerMessage` events with
  stable IDs; packages do not invent private source, meter, tag, or event-name
  dialects.
- Keep immutable event, audit, policy, redaction, and sink contracts in
  AgentKit.Abstractions. Exporters are leaf integrations.
- Distinguish durable semantic events from provisional live events, and routine
  telemetry from the security audit stream. Shared transport does not merge
  their schemas or guarantees.
- Correlate with typed domain identities; provider, MCP, trace, and span IDs are
  supplemental. Do not use high-cardinality domain IDs as ordinary metric tags.
- Content capture is opt-in, classified, bounded, and redacted before export.
  Redaction failure omits content instead of leaking the original value.
- Observability never controls runs, mutates semantic state, or becomes an
  alternate source of truth. Sinks receive immutable records only.
- Best-effort failures are isolated. Required sink delivery may affect
  settlement only through an explicit bounded policy and must not deadlock
  shutdown.
- Preserve stable error categories and side-effect certainty without exposing
  raw prompts, credentials, tool arguments, retrieved content, or reasoning.

Test structured log IDs and fields, activity parentage and terminal status,
bounded metric dimensions, correlation, redaction, sink capability negotiation,
delivery failure, required settlement, cancellation, and DI replacement.
