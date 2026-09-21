---
name: agentkit-identity-and-tenancy
description:
  "Design, implement, or debug AgentKit execution identity, trusted ingress,
  tenant isolation, claims, and delegation chains. Use for authentication
  normalization and propagation; not authorization policy."
---

# AgentKit Identity and Tenancy

Read [AGENTS.md](../../../AGENTS.md), the
[identity architecture](../../../docs/architecture/identity.md), and the
[normative identity specification](../../../docs/concepts/execution-identity-and-tenancy.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Boundary

- A trusted host ingress authenticates external credentials. AgentKit.Identity
  normalizes, validates, versions, and derives immutable execution identities;
  credentials stay in the leaf integration.
- Identity states who is acting and for which tenant. It does not authorize any
  operation, create grants, interpret permission policy, or replace security.
- Keep tenant, principal, issuer, evidence, assurance, claim provenance, and
  delegation-chain values in AgentKit.Abstractions. Never store raw tokens,
  cookies, certificates, or provider credentials in them.
- Propagate identity explicitly with admission and run data. Prompt text,
  headers, workspace content, ambient principals, and static current-user state
  cannot change an in-flight identity.
- Tenant identity is an isolation boundary for stores, caches, catalogs,
  providers, artifacts, memory, sessions, and telemetry; ambiguous mapping fails
  before protected data is read.
- Delegated identities retain provenance and may only narrow. Requester,
  delegate, impersonator, and approver remain distinct causal identities.
- Keep dependency flow one-way: host authentication to identity normalization to
  admission/run input to downstream security evaluation.
- Facade assertion ingress: `Agent.RunAsync` / `StreamAsync` overloads accepting
  `IdentityAssertion` resolve through scoped `IExecutionIdentityResolver` after
  `AddAgentIdentity`; map failures to typed run rejection before admission.
- Protected-boundary revalidation: when `IIdentityValidationPolicy` is composed,
  `SecurityAuthority` revalidates identity before policy evaluation; expired
  evidence maps to `AuthorizationDenied`, other failures to
  `AuthenticationFailed`.

Test issuer mapping, expiry/revocation, tenant partitioning, propagation,
delegation narrowing, reauthentication, and the absence of credential leakage.
