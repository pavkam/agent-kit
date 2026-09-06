# Permissions and human control

**Role:** Decide whether proposed work has authority to proceed.

Model output is a proposal, never permission. This component evaluates every
operation that can expose data or cause effects, regardless of whether the tool
is local, reflected, remote, built in, or MCP-backed.

## Policy decisions

The policy engine receives the effective tenant, principal, agent, session, run,
call, stable tool identity, validated argument fingerprint, requested resources,
effect class, catalog version, and relevant host boundary. It returns allow,
deny, or require approval.

Rules have documented ordering and an explicit default. Missing policy, unknown
effect, ambiguous identity, stale context, evaluation failure, or unavailable
sandbox fails closed. Descriptions, schemas, chat history, retrieved text, and
remote annotations are evidence only; they cannot modify authority.

Policy is replaceable independently from the tool invoker and from human
approval handling. A sandbox constrains consequences after authorization. It
does not grant permission.

## Human approval

The approval broker presents a bounded, redacted explanation of the exact tool,
principal, resource and effect scope, safe argument summary, and expiry. A
response binds to the request, tool version and source, argument fingerprint,
principal, policy and catalog version, validity period, and allowed uses.

Single-use approval is consumed atomically with call recording. Any change to
arguments, resources, tool version, principal, or policy invalidates the prior
decision. Human-edited arguments return through validation and fresh policy
evaluation.

## Deferral

Approval may complete inline or become durable deferred work. External tool
execution, pending results, and provider suspension use the same causal model.
When the result cannot be produced now, the run settles with a typed deferred
outcome and durable request identities; it does not keep an in-memory task alive
indefinitely.

Resolution arrives later as authenticated, idempotent input. The session loads
the unresolved request, checks ownership and expiry, validates correlation and
payload, commits one resolution, and wakes a causally linked continuation run.
Late or conflicting resolution cannot overwrite the first.

## Trust boundaries

Transport authentication only permits communication with a remote service. It
does not authorize every primitive exposed by that service. Historical approvals
cannot be forged or replayed as current authority. Permission results sent back
to a model reveal only safe, policy-selected detail.

Every decision, approval transition, consumption, expiry, denial, and revocation
emits a redacted audit record. Required audit persistence is part of run
settlement.

## Related concept specifications

- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
- [Deferred tools and human-in-the-loop](../concepts/deferred-and-human-in-the-loop.md)
- [Observability and audit](../concepts/observability-and-audit.md)
