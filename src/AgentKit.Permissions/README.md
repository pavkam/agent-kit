# AgentKit.Permissions

Evaluate security requests and manage bounded grants, profiles, and audit
dispatch.

Use this authority across protected operations, including tools and host access.
A selected policy and valid grant are required at the effecting boundary;
identity and tool metadata do not grant permission.

## Use this project

Start with `AddAgentPermissions`, `AddSecurityProfilePublication`,
`AddSecurityAuditSink` in [ServiceExtensions.cs](ServiceExtensions.cs). Read the
overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Identity](../AgentKit.Identity/README.md) — normalize trusted
  ingress identity and derive constrained delegated identities.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Network](../AgentKit.Network/README.md) — resolve network
  destinations and send bounded HTTP requests through security enforcement.
- [AgentKit.FileSystem](../AgentKit.FileSystem/README.md) — access files through
  a root-bounded implementation of the filesystem contract.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Permissions.Tests](../../tests/AgentKit.Permissions.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/permissions-and-human-control.md)
  — intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
