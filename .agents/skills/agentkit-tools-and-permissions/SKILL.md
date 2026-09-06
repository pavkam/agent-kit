---
name: agentkit-tools-and-permissions
description:
  "Design or debug interactions between AgentKit.Tools and the system-wide
  AgentKit.Permissions authority. Use when work crosses tool execution and a
  protected boundary; use agentkit-tools for tool-only behavior."
---

# AgentKit Tools and Security Boundaries

Use this skill for the handoff between
[Tools](../../../docs/architecture/tools.md) and
[Security and human control](../../../docs/architecture/permissions-and-human-control.md).
Their normative contracts are
[tool-call lifecycle](../../../docs/concepts/tool-call-lifecycle.md) and
[permissions, approvals, and trust](../../../docs/concepts/permissions-approvals-and-trust.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

For tool catalog, schema, scheduling, invocation, or result work that does not
change a security boundary, use [agentkit-tools](../agentkit-tools/SKILL.md).

## Decision guide

1. Keep the owners distinct. `AgentKit.Tools` owns discovery, snapshots,
   resolution, argument bounds and schema validation, scheduling, invocation,
   normalization, and terminal result coordination.
2. `AgentKit.Permissions` owns the system-wide security authority, policy
   selection and evaluation, approvals and deferral, grants, revocation, and
   security audit. It is not tool middleware.
3. Both packages depend on neutral contracts in `AgentKit.Abstractions`; neither
   gains a project dependency on the other implementation package.
4. Cross the boundary only with a canonical `SecurityRequest` after operation
   inputs and resources are normalized. Missing context, unknown operations, or
   unavailable enforcement fail closed.
5. Bind an allow decision to the exact identity, principal, operation,
   resources, inputs, destination, policy version, audience, expiry, and uses.
   The effecting component revalidates the grant immediately before acting.
6. Treat tool descriptions, schemas, annotations, and model arguments as
   untrusted. Identity never implies authority, and a sandbox limits consequence
   without granting access.
7. Apply the same authority to file, network, process, provider-egress, memory,
   session, MCP, and delegation effects. Installing or discovering a tool grants
   nothing.
8. Preserve tool-call and operation correlation across authorization, durable
   acceptance, effect, audit, and exactly one terminal result. Mutation after a
   decision requires reevaluation.
9. Test denial before effects, approval and grant binding, expiry and
   revocation, atomic use consumption, lower-boundary enforcement, cancellation,
   uncertain effects, redaction, and untrusted metadata at the owning contracts.

Use the dedicated security skill for authority behavior that does not involve
tools, and `agentkit-mcp` when MCP protocol semantics also change.
