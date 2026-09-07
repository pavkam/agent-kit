# Execution identity and tenancy

**Status:** Normative

**Architecture:** [Execution identity and tenancy](../architecture/identity.md)

**Depends on:**
[Agent definition and run context](agent-definition-and-run-context.md),
[permissions, approvals, and trust](permissions-approvals-and-trust.md)

## Purpose

Every operation must identify who is acting, for which tenant, through which
trusted ingress, and with what authentication evidence. Identity describes the
subject; it does not grant authority. Authorization remains the responsibility
of the security system.

## Trusted ingress

AgentKit MUST NOT authenticate arbitrary bearer tokens, cookies, API keys, or
client certificates inside the agent loop. A trusted host or ingress adapter
authenticates external credentials and produces an immutable execution identity
with its evidence and assurance level.

The public execution surface MUST distinguish a trusted identity assertion from
untrusted user-supplied content. A principal identifier embedded in a prompt,
tool argument, message header, or workspace file is data and MUST NOT become the
execution principal.

Anonymous execution MAY be supported only through an explicit host policy and a
dedicated anonymous principal identity. Missing identity MUST NOT silently
become a process-wide default user.

## Identity shape

An execution identity MUST include:

- stable tenant and principal identities;
- subject kind, such as human, service, workload, or explicitly anonymous;
- trusted ingress and authentication-method identity;
- authentication instant, expiry, and evidence reference where applicable;
- normalized claims, groups, roles, or scopes with issuer provenance;
- assurance level and impersonation or delegation chain; and
- a version or fingerprint suitable for cache partitioning and audit.

Credentials and raw authentication tokens MUST NOT be stored in the identity,
messages, session history, context manifests, or diagnostics.

The trusted issuer mapping establishes the subject and issuer-provenanced claim
set. Subsequent normalization policies MUST preserve tenant, principal, subject
kind, authentication evidence, mapping version, and delegation chain. They MAY
remove claims, lower assurance, or reject the identity; they MUST NOT enrich
claims, replace a subject, or restore values removed by an earlier policy. Claim
enrichment belongs to the versioned issuer mapping and delegation belongs to the
identity-derivation contract. The resolver validates every policy result against
the preceding candidate before the identity can enter admission.

## Propagation and boundaries

The immutable identity is captured when input is admitted or a run begins and
flows explicitly through run, session, context, provider-egress, tool,
filesystem, network, process, memory, MCP, artifact, and delegation requests.
Ambient principal state and static current-user accessors are forbidden.

Changing tenant or principal creates a new admission or run boundary. It MUST
NOT mutate an in-flight identity snapshot. Caches, catalogs, durable records,
idempotency keys, and authorization decisions MUST include the tenant and
principal dimensions needed to prevent cross-boundary reuse.

## Delegation and impersonation

A delegated identity MUST retain the parent subject and delegation chain. A
child may receive the same subject or a narrower derived workload identity, but
delegation MUST NOT broaden authority, erase provenance, or impersonate another
principal without an explicit authenticated host mechanism and security
decision.

The approver of an operation is separate from the requesting execution identity.
Approval authentication proves who approved; it does not replace the requesting
subject or rewrite the causal chain.

## Tenancy

Tenant identity is an isolation boundary, not a display label. Stores,
directories, artifact backends, memory, vectors, session routing, provider
accounts, caches, and observability exports MUST either partition by tenant or
reject multi-tenant use explicitly.

A host MAY map several external issuers into one tenant, but that mapping is a
versioned trusted policy. Untrusted configuration cannot change it. A missing or
ambiguous mapping fails before protected state is read.

## Failure and audit

Expired, revoked, ambiguous, malformed, unsupported, or insufficiently assured
identity produces a typed authentication or identity-validation result. It MUST
NOT be reported as permission denial because callers need to distinguish “we do
not know who you are” from “you are not allowed.”

Audit records retain safe identity, issuer, ingress, assurance, delegation, and
correlation data. They MUST NOT contain raw credentials or unrestricted claim
payloads.

## Acceptance scenarios

- A principal name in model output cannot change the run identity.
- A normalization policy cannot replace the mapped subject or widen claims and
  assurance, including restoring claims removed by an earlier policy.
- A policy that only removes claims or lowers assurance preserves the captured
  evidence and remains a valid normalization step.
- Two tenants using the same external subject string never share cache or store
  entries.
- A child run retains its parent delegation chain and cannot gain authority.
- Expired authentication fails before session, memory, artifact, or tool data is
  exposed.
- Approval by another person records both requester and approver identities.
- A run resumed after a host identity change uses the durable captured identity
  policy or requires explicit reauthentication; it never silently substitutes.

## Related specifications

- [Input admission and message queues](input-admission-and-message-queues.md)
- [Sessions, persistence, and branching](sessions-persistence-and-branching.md)
- [Goals and multi-agent delegation](goals-and-multi-agent-delegation.md)
- [Observability and audit](observability-and-audit.md)
- [Configuration and overrides](configuration-and-overrides.md)
