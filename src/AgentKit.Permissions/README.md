# AgentKit.Permissions

Evaluate security requests and manage bounded grants, profiles, and audit
dispatch.

Use this authority across protected operations, including tools and host access.
A selected policy and valid grant are required at the effecting boundary;
identity and tool metadata do not grant permission.

## Use this project

Start with `AddAgentPermissions`, then explicitly select one grant-store adapter
before registering `AddSecurityProfilePublication` and `AddSecurityAuditSink`.
For deterministic tests and short-lived hosts, install
[AgentKit.Permissions.InMemory](../AgentKit.Permissions.InMemory/README.md) and
call `AddInMemorySecurityGrantStore()`. Read the [runtime](ServiceExtensions.cs)
and adapter registration XML for required collaborators, lifetimes, and
duplicate-registration behavior.

For one standalone agent composition, `AddStandaloneSecurityProfile` derives a
matching `SecurityPolicySnapshotReference`, calls `AddAgentPermissions` with it,
and registers the profile publication and keyed authority binding that reference
it — all without building an intermediate provider. Composing this by hand is a
documented trap: `AgentPermissionOptions.PolicySnapshot` defaults to `null`,
which silently denies every request carrying captured authorization (the
standard session/run flow) with `"security.captured_context_mismatch"` until the
snapshot is wired through consistently; see its own remarks for the exact
mechanism.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Example security policies

`AddWorkspaceScopedFileAccessPolicy` registers
`WorkspaceScopedFileAccessPolicy`, an illustrative `ISecurityPolicy`.
`SecurityAuthority` denies by default when no policy allows a request, so
registering only this one turns that fail-closed default into "workspace-scoped
file and directory access is allowed" for `FileRead`, `DirectoryRead`,
`FileSearch`, `FileWrite`, and `DirectoryCreate` requests, while granting
nothing for any other operation kind.

It allows a request only when every resource has kind `File` or `Directory` and
an identifier that is a non-rooted, traversal-free relative path — the same
structural invariant `FileSystemPath` enforces at construction, re-checked here
as defense in depth. It abstains, rather than denies, on anything it cannot
vouch for, so a more specific policy can still decide; absent one, the
authority's fail-closed default still applies. It does not know a configured
filesystem root and cannot prove a resource resolves inside one — applications
with sharper requirements should replace or compose it with their own policy.

`AddAllowAllSecurityPolicy` registers `AllowAllSecurityPolicy`, which allows
every operation kind unconditionally. Use it only for a local, single-tenant
composition (a demo, an example, or a developer's own machine) that already
trusts every operation its own agent can request — see its own remarks before
reaching for it in anything else.

## Related projects

- [AgentKit.Permissions.InMemory](../AgentKit.Permissions.InMemory/README.md) —
  explicitly selected process-local grant storage for tests and short-lived
  hosts.
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
