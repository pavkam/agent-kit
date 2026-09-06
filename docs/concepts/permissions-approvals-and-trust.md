# Permissions, approvals, and trust

**Status:** Normative security boundary  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[history validation](history-validation-and-repair.md),
[hooks](extensions-hooks-and-middleware.md)

## Purpose

Any AgentKit component may propose a protected operation; only the configured
security authority can grant it. This applies to tools, files, network access,
model data egress, processes, memory, session access, MCP, delegation, dynamic
code, and future capabilities.

AgentKit.Permissions supplies the first-party authority, policy pipeline,
approval broker, grant lifecycle, and fail-closed defaults. The contracts and
serializable values live in AgentKit.Abstractions. Components depend on those
contracts rather than on AgentKit.Permissions.

## Security request

A `SecurityRequest` MUST contain:

- stable request, operation, component, and causal identities;
- the complete immutable `ExecutionIdentity`, plus agent, session, run, and
  optional tool-call identity;
- a canonical operation kind and effect class;
- normalized resources, destination, and input fingerprint;
- a bounded safe reason and presentation metadata;
- configuration, catalog, and policy versions relevant to the operation;
- requested lifetime, uses, deadline, and idempotency identity; and
- typed operation details or namespaced extension data without credentials.

Each component MUST translate its domain operation into this common envelope
without discarding the typed values needed for enforcement. Display names,
descriptions, schemas, model text, retrieved content, and remote annotations are
untrusted evidence. They cannot modify authority.

Requests MUST be evaluated after inputs and resources are canonicalized but
before the effect starts. An operation changed after evaluation requires a new
request.

## Authority and policy

The authority identified by the captured security profile MUST be selected
through `ISecurityAuthoritySelector`; protected components MUST NOT inject an
unkeyed authority or resolve keyed services themselves. The selected
`ISecurityAuthority` MUST return one typed decision:

- allow with a bounded `SecurityGrant`;
- deny with a safe reason and policy reference; or
- require approval with a durable `ApprovalRequest`.

Missing policy, missing context, unknown operation or effect, ambiguous
resource, stale configuration, evaluation failure, unavailable required audit,
or unavailable enforcement MUST deny by default. A policy may distinguish hard
deny from approval-required; absence never implies allow.

Ordered policy matching and precedence MUST be deterministic and observable.
Managed deny and scope limits may be non-overridable by lower-trust
configuration. Wildcards match canonical operation and resource identities, not
display strings.

Policy MAY approve some requests automatically. Emitting a security request does
not mean every operation interrupts a human.

## Security grants

A grant MUST bind:

- request and operation identity;
- the exact immutable execution identity, including tenant, principal, evidence,
  assurance, delegation chain, and identity version;
- canonical operation kind, effect, resources, destination, and input
  fingerprint;
- intended enforcing component or audience;
- policy and configuration versions;
- issue, not-before, expiry, remaining uses, and revocation state; and
- optional session, run, call, or idempotency scope.

The effecting component MUST validate the grant immediately before the protected
operation. A higher-level component may not treat an allow decision as authority
for a different lower-level request. File-system, network, and process
implementations MUST enforce the derived scope again.

Single-use grants MUST be consumed atomically with durable operation recording.
Concurrent requests cannot spend the same use twice. Expiry or revocation stops
future use but does not rewrite completed effects.

Every grant must carry the same execution-identity snapshot as its captured
`SecurityAuthorizationContext`. Reauthentication, delegation, or any change to
that identity requires a fresh context and decision; matching tenant and
principal projections alone is insufficient.

## Approval request and resolution

An approval request MUST provide a bounded, redacted human explanation plus the
exact operation, principal, component, normalized resource and effect scope,
safe input summary, policy basis, and expiry. Redaction must hide sensitive
values without hiding the material effect being approved.

An approval response MUST bind structurally or cryptographically to:

- request, decision, and operation identity;
- operation kind, component, resources, destination, effect, and input
  fingerprint;
- principal, tenant, and optional session/run/call;
- policy and configuration versions;
- approver identity, issue and expiry time, allowed uses, and revocation state;
  and
- the authenticated approval channel and response idempotency identity.

Approval produces a grant only after the response and current operation are
revalidated. Human-edited input creates a new fingerprint and MUST undergo
domain validation and fresh security evaluation.

If no handler can answer inline, the request becomes durable deferred work. A
headless host MUST deny or defer according to explicit policy rather than wait
indefinitely.

The approval-resolution transport is a trusted bootstrap boundary. It MUST NOT
recursively require the same unresolved operation. Transport authentication
identifies a responder; it does not authorize the requested effect by itself.

## Caching and aggregation

Decisions or grants MAY be cached only when policy returns an explicit scope and
lifetime. Cache keys include every security-relevant input. Resource expansion,
input change, principal change, component or operation change, provider/tool
upgrade, policy change, expiry, or revocation invalidates the entry.

Several operations MAY share one human presentation only when each underlying
request remains explicit and policy authorizes the aggregate scope. Approval of
a displayed batch MUST NOT become unbounded approval for later similar work.

## Hooks

Security hook points may prepare or redact the human presentation, request a
stricter outcome, and observe decisions, grants, resolutions, or audits. Hook
event arguments MUST keep authority-bearing fields read-only.

A hook MUST NOT allow, widen, forge, cache, consume, or mint a grant. If any
other hook changes resources, destination, effect, principal, or protected input
after authorization, execution MUST stop and return through the authority.

## Denial and deferral behavior

The owning component defines whether denial produces a safe domain result, halts
the current loop, or cancels the run. The outcome MUST remain typed and
observable. Raw policy rules, internal paths, credentials, or sensitive denial
context MUST NOT be sent to the model.

Repeated attempts to evade denial SHOULD trigger a loop policy or limit using
stable operation and resource fingerprints, not text similarity alone.

## Sandbox relationship

A sandbox constrains consequences but does not grant permission. Authorization
runs before sandboxed execution. The sandbox profile derives from the grant,
fails closed when unavailable, and records the effective boundary.

Transport authorization, provider credentials, and MCP OAuth identify access to
an external service. They remain separate from AgentKit authorization for the
operation performed through that service.

## Audit

Every request, winning policy, decision, approval transition, grant issue and
consumption, denial, expiry, revocation, and enforcement failure MUST emit a
redacted audit record with stable correlation. Required audit persistence is
part of settlement.

Audit MUST NOT contain credentials, raw secret-bearing inputs, unrestricted tool
output, or unredacted protected content. Audit failure follows declared required
or best-effort delivery policy and can never convert denial into allow.

## Acceptance scenarios

- File read, network egress, process start, memory write, and tool invocation
  use the same security authority with distinct typed operation details.
- Denial occurs before any protected effect.
- A tool grant for one path cannot authorize a different file-system path.
- Changed provider destination invalidates a prior model-egress grant.
- Two concurrent operations cannot consume one single-use grant.
- Human-edited input returns through validation and policy.
- A headless host denies or defers instead of waiting forever.
- Approval resolution cannot recurse on its own unresolved approval.
- A hook cannot change a denial into an allow.
- Sandbox absence fails closed and does not silently run unsandboxed.

## Related specifications

- [Deferred operations and human-in-the-loop](deferred-and-human-in-the-loop.md)
- [Tool-call lifecycle](tool-call-lifecycle.md)
- [MCP integration](mcp-integration.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Observability and audit](observability-and-audit.md)
