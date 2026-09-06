---
name: agentkit-diagnostics
description:
  "Investigate AgentKit runtime, composition, provider, stream, hook, tool,
  security, queue, memory, cancellation, and concurrency failures. Use to locate
  the owning boundary and regression seam before changing behavior."
---

# AgentKit Diagnostics

Follow [Observability](../../../docs/architecture/observability.md), the
[observability and audit contract](../../../docs/concepts/observability-and-audit.md),
and the [error taxonomy](../../../docs/concepts/error-taxonomy.md). When
changing C#, also read the [modern C# rules](../references/modern-csharp.md).

## Decision guide

1. Diagnose before fixing unless the user requested both. Capture the smallest
   reproducible run, expected transition, actual outcome, effective selections,
   capability snapshots, and redacted correlation identities.
2. Reproduce with deterministic collaborators or loopback transport. Use live
   systems only to isolate behavior that the local reproduction cannot explain.
3. Follow one typed correlation chain across component boundaries and find the
   first point where state, ordering, content, identity, authority, budget, or
   side-effect certainty diverges.
4. Inspect both dependency graphs, keyed selections, lifetimes, configuration
   snapshots, ownership, and disposal before blaming business logic.
5. Replay streams at different byte and logical-event boundaries. Distinguish
   malformed protocol, premature end, cancellation, timeout, backpressure, and
   observer failure.
6. For protected effects, keep policy evaluation, approval, grant binding,
   revocation, audit, and low-level enforcement as separate diagnostic seams.
   Never bypass security to make a reproduction pass.
7. Classify the root cause at the component that owns the failed contract. That
   boundary maps its error and preserves safe external diagnostics.
8. If a fix is authorized, add the smallest regression at the lowest public
   boundary, change the owner, then run affected conformance suites.

Consume AgentKit's neutral immutable events, audit records, stable errors, logs,
traces, and metrics. Do not require or prescribe `ActivitySource`, `Meter`, or a
specific exporter; OpenTelemetry and other exporters are leaf integrations.
Content capture is opt-in, classified, bounded, and redacted.
