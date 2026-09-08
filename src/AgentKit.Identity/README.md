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

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

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
