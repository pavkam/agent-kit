# AgentKit.Identity

Normalize trusted ingress identity and derive constrained delegated identities.

Use this package where authenticated host facts enter an AgentKit operation.
Identity describes the caller; the security authority separately decides which
effects are allowed.

## Use this project

Start with `AddAgentIdentity`, `AddIdentityIssuer`,
`AddIdentityNormalizationPolicy` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Build an identity at the ingress

When your host has already authenticated the caller and only needs to carry that
fact into AgentKit, the two factories on `ExecutionIdentity` record it without
hand-assembling evidence and fingerprints:

```csharp
// A worker running under a platform-issued identity.
var worker = ExecutionIdentity.ForService(
    new TenantId("acme"), new PrincipalId("triage-worker"),
    new IdentityIssuerId("azure-managed-identity"), "managed-identity", clock.GetUtcNow());

// A person your OIDC middleware signed in.
var customer = ExecutionIdentity.ForHuman(
    new TenantId(user.FindFirstValue("tenant")!), new PrincipalId(user.FindFirstValue("sub")!),
    new IdentityIssuerId("acme-idp"), "oidc", authenticatedAt, expiresAt);
```

The fingerprint is a SHA-256 over issuer, subject, method, and time; no token or
secret is an input. These factories do not authenticate anything: the caller
states who authenticated the subject and how. Hosts with several issuers, claim
normalization, or delegation compose `AddAgentIdentity` and resolve through
`IExecutionIdentityResolver` instead.

## Related projects

- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.
- [AgentKit.Goals](../AgentKit.Goals/README.md) — coordinate protected
  task-delegation requests.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Identity.Tests](../../tests/AgentKit.Identity.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/identity.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
