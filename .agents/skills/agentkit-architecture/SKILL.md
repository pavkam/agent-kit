---
name: agentkit-architecture
description:
  "Design or review AgentKit package boundaries, extension contracts, and DI
  composition. Use for new subsystems, cross-package refactors, or dependency
  direction; not provider protocol details or one component's local behavior."
---

# AgentKit Architecture

Use the [architecture index](../../../docs/architecture/index.md) as the map and
the linked component and concept documents as the source of truth. When changing
C#, also read the [modern C# rules](../references/modern-csharp.md).

For a coding host or cross-package harness design, also read the
[coding-harness execution profile](../../../docs/profiles/coding-harness/coding-harness-execution-profile.md)
and only the focused workspace, tool, terminal, language-service, snapshot,
resource, control-plane, or MCP profile linked from the
[coding-harness profile index](../../../docs/profiles/coding-harness/index.md).

## Decision guide

1. Name the behavior, its single policy owner, and the collaborators that
   consume it. Do not create a package merely because a domain value has a name.
2. Preserve the dependency direction in
   [project structure](../../../docs/architecture/project-structure.md): neutral
   contracts in `AgentKit.Abstractions`, the dependency-light facade in
   `AgentKit`, focused implementations above it, and integrations as leaves.
3. Validate both DAGs: project references and the declared closed constructor/
   factory graph. Shared exporter-free observability is infrastructure;
   evaluation and goal-worker hosting are application leaves allowed to consume
   the facade. A service locator, deferred factory, or nested scope does not
   repair a cycle.
4. Add a narrow contract only for a demonstrated extension axis with plausible
   alternatives. Keep discovery, selection, policy, execution, persistence, and
   observation separate; inheritance remains optional.
5. Define DI cardinality, keying, replacement, collision behavior, lifetime,
   ownership, disposal, threading, and cancellation. Registrations never build a
   provider.
6. Put each behavioral choice in one explicit home: DI, validated package
   options, an immutable agent definition, or a bounded run override.
7. Make capabilities and unsupported behavior observable before execution.
   Credentials, endpoints, persistence targets, and authority have no fabricated
   defaults. When several external services or accounts can coexist, bind and
   capture their independently keyed identities rather than depending on an
   unkeyed registration-order singleton.
8. Keep cross-cutting hooks typed and security-neutral. Host file, network, and
   process effects remain protected leaf boundaries.
9. Update the architecture index, affected component page, normative concepts,
   repository guidance, and routed skills together when ownership changes.

Start with
[composition and configuration](../../../docs/architecture/composition-and-configuration.md),
[foundation contracts](../../../docs/architecture/foundation-contracts.md),
[architecture boundaries](../../../docs/concepts/architecture-and-dependency-boundaries.md),
[public API and DI](../../../docs/concepts/public-api-and-dependency-injection.md),
and
[configuration and overrides](../../../docs/concepts/configuration-and-overrides.md).

Synchronous build consumes a materialized initial catalog without network or
secret access. Validate publication, admission, and effects at their separate
boundaries. Revalidate pinned handles for new work; retained versions support
recovery but never override live revocation. Implementation and tests do not
supersede architecture or its linked normative concepts.

An architecture result must state the owner, contract location, dependency
edges, registration and selection model, lifecycle, unsupported behavior, and
verification boundary.
