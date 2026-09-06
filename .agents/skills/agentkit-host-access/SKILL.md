---
name: agentkit-host-access
description:
  "Design, implement, or debug AgentKit filesystem, network, or process
  contracts and host adapters. Use for protected low-level host effects; not
  higher-level tool, provider, or MCP semantics."
---

# AgentKit Host Access

Read [AGENTS.md](../../../AGENTS.md), then load only the affected surface:

- file and directory work:
  [file-system architecture](../../../docs/architecture/file-system.md);
- DNS, connections, requests, redirects, or egress:
  [network architecture](../../../docs/architecture/network.md);
- executable resolution, sandboxing, standard streams, or termination:
  [process architecture](../../../docs/architecture/process-execution.md).

For protected effects also read the
[security specification](../../../docs/concepts/permissions-approvals-and-trust.md).
For timeout, cancellation, or retry work read the
[resilience specification](../../../docs/concepts/cancellation-timeouts-and-resilience.md).
When changing C#, read the [modern C# rules](../references/modern-csharp.md).

## Boundary

- Keep file, network, and process contracts separate in AgentKit.Abstractions.
  Do not create a universal host-access interface or let one grant imply
  another.
- Real and deterministic test implementations belong to their focused packages;
  higher-level consumers depend only on the narrow contracts they need.
- Canonicalize the exact target and bounds before authorization. The effecting
  implementation revalidates and consumes the bounded grant immediately before
  each material host effect.
- Sandboxing and transport restrictions reduce consequences but never grant
  permission. Changed paths, destinations, redirects, executable inputs, or
  scopes require reevaluation.
- Declare capabilities, ownership, disposal, cancellation, backpressure, and
  unsupported behavior explicitly; never fall back to raw host APIs.
- Test denial before effects and run the same conformance suite against the
  deterministic and real implementation using isolated fixtures.

Load more than one surface document only when the requested operation truly
crosses those boundaries, and preserve their independent authorization.
