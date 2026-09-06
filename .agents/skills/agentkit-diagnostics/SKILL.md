---
name: agentkit-diagnostics
description:
  "Investigate AgentKit runtime, dependency-injection, provider, streaming,
  tool, permission, queue, memory, cancellation, and concurrency failures. Use
  for diagnosis and regression isolation before changing behavior."
---

# AgentKit Diagnostics

Read [AGENTS.md](../../../AGENTS.md). Diagnose first; do not turn a request for
an explanation into an implementation unless the user also asks for a fix.

1. Capture the smallest reproducible run: configuration shape with secrets
   removed, selected implementations and capabilities, run/call IDs, expected
   state transition, actual transition, and terminal outcome.
2. Reproduce with deterministic fakes or a loopback transport before using a
   live provider. If only live traffic fails, isolate authentication, endpoint,
   deployment/model selection, HTTP framing, throttling, or provider capability
   drift. Identify the concrete provider package and its tested compatibility
   profile; `OpenAICompatible` alone is not enough to identify behavior.
3. Inspect the DI graph: missing/duplicate registrations, keyed/name mismatch,
   scope leaks, disposable ownership, options validation, decorators, and
   accidental construction of a second container.
4. Trace one correlation chain across queue item → run → provider request →
   streamed item → tool call → permission decision → tool result → stored
   message. Find the first boundary where identity, ordering, or content
   changes. Treat `AgentKit.IO`, `AgentKit.Loop`, `AgentKit.Providers`, and the
   concrete provider package as separate diagnostic boundaries.
5. For streams, retain event type/index and parser state; test the same payload
   split at different byte and logical-event boundaries. Distinguish malformed
   protocol, premature EOF, consumer cancellation, timeout, and observer
   failure.
6. For tool failures, prove whether discovery, schema validation, resolution,
   policy, approval, invocation, or result correlation failed. Never bypass
   permissions to make a test pass.
7. For queue/storage failures, inspect lease/version tokens, idempotency keys,
   redelivery, concurrency, consistency, pagination, and time assumptions using
   a controllable `TimeProvider`.
8. Classify the root cause at the owning layer. Wrap errors only at abstraction
   boundaries and preserve the original provider/status/request ID as redacted
   diagnostic context.
9. Add a regression test at the lowest public boundary that reproduces the
   failure. Fix the owner, then run its focused tests plus affected conformance
   suites.

Instrumentation should use structured logs, `ActivitySource`, and `Meter`-style
dimensions with bounded cardinality. Raw prompts, tool arguments/results,
retrieved documents, tokens, credentials, and user content are opt-in sensitive
payloads, not default telemetry.
