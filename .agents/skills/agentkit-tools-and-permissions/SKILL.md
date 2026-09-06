---
name: agentkit-tools-and-permissions
description:
  "Design, implement, or debug AgentKit tool descriptions, providers,
  resolution, invocation, permission policies, approvals, and audit. Use when
  tools can read data or cause side effects; use agentkit-mcp as well for MCP
  transport semantics."
---

# AgentKit Tools and Permissions

Read [AGENTS.md](../../../AGENTS.md). Preserve this execution pipeline:

```text
discover → resolve → parse/bound → validate → authorize → invoke → record → return
```

1. Keep `ITool`, tool descriptors, tool providers/catalogs, resolvers,
   executors, permission evaluators, approval brokers, and audit sinks as
   separate contracts. A registry does not execute, and an executor does not
   decide policy.
2. Give every tool a stable identity independent of its display name. Version
   schemas intentionally; define duplicate-name and provider-precedence
   behavior.
3. Bound and parse model-produced arguments, then validate them against the
   declared schema before policy evaluation and invocation. Return typed
   argument failures to the loop; never let malformed JSON fall into reflection
   or dynamic binding unchecked.
4. Evaluate permissions per invocation using the effective agent/principal, tool
   identity and source, normalized arguments or fingerprint, resource scope,
   side-effect class, run/call IDs, and relevant policy context.
5. Represent policy outcomes explicitly: allow, deny, or require approval.
   Missing policy context, unknown tools, stale approvals, and scope expansion
   fail closed. Cache decisions only when the policy returns an explicit scope
   and lifetime.
6. Bind approvals to the exact tool, arguments/resource scope, principal, and
   expiry. Approval for discovery or one invocation is not approval for future
   calls.
7. Pass a `CancellationToken`, honor timeout and concurrency limits, and define
   whether a tool is idempotent. Never silently retry mutating tools.
8. Preserve the provider tool-call ID and produce exactly one terminal result
   per accepted call. Parallel execution must have explicit ordering and
   conflict rules.
9. Audit decisions and outcomes with redacted structured fields. Do not record
   credentials, raw secret-bearing arguments, or unrestricted tool output.
10. Test denial before side effects, approval binding, schema edge cases,
    cancellation, duplicate results, provider collisions, parallel calls,
    idempotency, redaction, and untrusted metadata.

Reflection-based function tools may be a convenience adapter, never the core
contract. Their schema generation and invocation must obey the same validation
and permission pipeline.
