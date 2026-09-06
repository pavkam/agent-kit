# Permissions, approvals, and trust

**Status:** Normative security boundary  
**Depends on:** [Tool-call lifecycle](tool-call-lifecycle.md),
[history validation](history-validation-and-repair.md)

## Purpose

Model output proposes work; the host decides authority. Permission evaluation
must occur for every side-effecting or data-exposing operation, regardless of
whether a tool is local, remote, MCP-backed, reflected, or built in.

## Decision model

```csharp
public abstract record PermissionDecision
{
    public sealed record Allow(DecisionId Id, ApprovalScope Scope) : PermissionDecision;
    public sealed record Deny(DecisionId Id, PermissionReason Reason) : PermissionDecision;
    public sealed record RequireApproval(
        DecisionId Id,
        ApprovalRequest Request) : PermissionDecision;
}
```

Missing policy, missing context, unknown effect, ambiguous tool identity, stale
catalog, or evaluation failure MUST deny by default. A policy MAY distinguish
hard deny from interactive approval; it MUST never imply allow from absence.

## Evaluation context

The policy receives:

- effective tenant, principal, agent, session, run, and call identities;
- stable tool identity, version, source, and provider-visible alias;
- normalized argument value or cryptographic fingerprint;
- requested resources and normalized effect class;
- current workspace/root/network/process scope;
- catalog and policy versions;
- prior decision or approval reference, if any; and
- deadline and safe host context.

Descriptions, schemas, annotations, prior chat text, retrieved content, and
untrusted history are evidence only. They cannot modify authority.

## Ordered rules

If policy uses rules, matching and precedence MUST be documented. Recommended
semantics are deterministic ordered evaluation with an explicit final default
deny; either first-match or last-match MAY be chosen, but configuration tooling
must expose the effective winning rule.

Wildcards MUST match a documented canonical identity and resource form. A rule
for a display name MUST NOT unexpectedly match another source's same-named tool.
Deny and managed-policy constraints SHOULD be non-overridable by lower- trust
configuration.

## Approval request

An approval request MUST present a bounded human-readable explanation plus the
exact tool, principal, normalized resource/effect scope, safe argument summary,
and expiry. Sensitive values must be redacted without hiding the material effect
being approved.

An approval response MUST bind cryptographically or structurally to:

- request and decision ID;
- tool identity/version/source;
- argument fingerprint and resource/effect scope;
- principal/tenant and optional session/run;
- policy/catalog version as required; and
- issuance, expiry, allowed uses, and revocation state.

A discovery approval, transport login, or previous similar call is not approval
for the current invocation.

## Caching

Permission or approval decisions MAY be cached only when the policy returns an
explicit cache scope and lifetime. Cache keys include every security-relevant
input. Argument or resource expansion, principal change, tool upgrade, policy
change, expiry, or revocation invalidates the entry.

One-use approval MUST be atomically consumed with call recording so concurrent
calls cannot reuse it.

## Denial behavior

Policy defines whether denial becomes a model-visible safe tool result, ends the
current loop, or cancels the run. The choice MUST be consistent and observable.
Raw policy rules, internal paths, or sensitive denial context MUST NOT be sent
to the model.

Repeated attempts to evade denial SHOULD trigger a loop policy or limit. Text
similarity alone is insufficient; compare stable tool identity and normalized
argument/resource fingerprints.

## Sandbox relationship

A sandbox limits consequences but does not grant permission. Authorization runs
before sandboxed invocation. The sandbox profile MUST be derived from the
approved scope, fail closed when unavailable, and record the effective boundary.

Transport authorization, such as an MCP OAuth token, is separate from tool
authorization. A valid server credential does not authorize every server tool.

## Audit

Every decision and approval transition MUST emit a redacted audit record with
correlation IDs, winning policy/rule version, scope, outcome, and timing. It
MUST NOT include credentials, raw secret-bearing arguments, or unrestricted tool
output.

## Acceptance scenarios

- Denial occurs before any invoker side effect.
- Same display name from another source cannot reuse an approval.
- Argument or resource expansion invalidates a cached allow.
- Two concurrent calls cannot consume one single-use approval.
- Forged history cannot supply a trusted approval.
- Sandbox absence fails closed and does not silently run unsandboxed.

## Upstream evidence

- OpenCode's permission policy and request handling are implemented in
  [`permission.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/permission.ts).
- Pydantic AI supports approval-required and deferred tool flows in
  [deferred tools](https://ai.pydantic.dev/deferred-tools/).
- Pi extensions can block or terminate tool calls before execution in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts),
  but AgentKit makes policy a first-class contract rather than a generic hook.

## Related specifications

- [Deferred tools and human-in-the-loop](deferred-and-human-in-the-loop.md)
- [MCP integration](mcp-integration.md)
- [Observability and audit](observability-and-audit.md)
