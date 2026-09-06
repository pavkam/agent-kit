---
name: agentkit-architecture
description:
  "Design or review AgentKit package boundaries, extension contracts, base
  classes, and dependency-injection composition. Use for new subsystems,
  cross-package refactors, or dependency-direction questions; not for provider
  protocol details."
---

# AgentKit Architecture

Use this skill to keep new capabilities composable without inventing one giant
plugin API.

1. Read [AGENTS.md](../../../AGENTS.md) and map the requested behavior to an
   extension axis: contract, selection, execution, state, policy, or
   observation.
2. Identify at least the consumer and two plausible implementations. If only one
   implementation is credible, keep the code focused and postpone a public
   abstraction unless the user explicitly requires it.
3. Place provider-neutral contracts in `AgentKit.Abstractions`. Put default
   orchestration in `AgentKit`; put vendor, transport, and persistence
   implementations in leaf packages. Reject any dependency from an abstraction
   or runtime package toward a concrete integration.
4. Give each extension point a narrow interface. Add an optional base class only
   when it supplies real reusable lifecycle, validation, streaming assembly, or
   error-mapping behavior. Keep direct interface implementation supported.
5. Define lifecycle and ownership: creation scope, thread safety, disposal,
   cancellation, concurrency, retry ownership, and whether state may survive a
   run.
6. Design DI registration as the public composition surface. State whether
   registrations are singular, additive, named/keyed, replaceable, and
   idempotent. Registration code must not build or resolve a container.
7. Keep capabilities explicit. Do not add optional members that return null or
   throw for half the implementations when a capability interface, descriptor,
   or discriminated result makes support observable.
8. Add conformance tests for the contract and DI tests proving defaults can be
   replaced. Review public API compatibility, XML documentation, and package
   dependencies before finishing.

The output of architecture work should make the dependency graph, extension
contract, default behavior, and unsupported behavior unambiguous.
