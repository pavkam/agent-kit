---
name: agentkit-mcp
description:
  "Implement or debug AgentKit MCP clients, servers, lifecycle, transports,
  capability negotiation, and primitive adapters. Use for Model Context Protocol
  integration; not generic tools or application security policy."
---

# AgentKit MCP

Treat [MCP architecture](../../../docs/architecture/mcp.md) and the
[normative MCP integration contract](../../../docs/concepts/mcp-integration.md)
as canonical. When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

For an MCP-enabled coding host, also read the
[coding-harness MCP profile](../../../docs/profiles/coding-harness/coding-harness-mcp-exposure.md)
for namespace collisions, instruction trust, root disclosure, pagination,
catalog generations, endpoint-bound OAuth/callback lifecycle, and nested
orchestration.

Read the local [protocol model](references/protocol-model.md) only when work
changes wire lifecycle, primitives, transports, or protocol security. Then
verify volatile details against the current official MCP specification.

## Decision guide

1. State the role: AgentKit host, MCP client, MCP server, or an explicit
   combination. Shared reflection/version contracts belong in AgentKit.Mcp;
   client and server support remain separate leaf packages.
2. Keep host policy, primitive adapters, protocol-era selection and correlation,
   capability enforcement, and transport as distinct layers. Protocol SDK types
   do not enter `AgentKit.Abstractions`.
3. Enumerate supported revisions explicitly: 2026-07-28 is per-request, while
   supported legacy revisions such as 2025-11-25 use initialize. Unknown future
   dates do not inherit a contract. Accept only methods valid for the captured
   era, revision, and capability snapshot. Keep MCP request IDs distinct from
   AgentKit domain identities while preserving correlation.
4. Map tools into the normal tool pipeline, resources into authorized retrieval
   or context sources, and prompts into classified user-selected input or
   instructions. Roots, sampling, elicitation, logging, progress, cancellation,
   and completion remain separate protocol capabilities.
5. For reflection-driven tools, use attributed classes with one request object
   and one asynchronous response object per method. Keep protocol revision,
   SDK/package version, tool contract version, and catalog generation distinct;
   reject duplicate names and version drift before invocation.
6. Treat remote descriptors, schemas, annotations, prompts, and content as
   untrusted. Preserve mixed typed content and safe unknown extension data.
7. Route stdio through AgentKit process contracts and HTTP through AgentKit
   network contracts. Connection authentication never grants authority for the
   effects exposed through that connection.
8. Give sessions, streams, child processes, reconnects, and in-flight requests
   explicit ownership, bounds, cancellation, and unknown-side-effect behavior.
9. Test lifecycle and capability rejection, request correlation, reflected
   request/response schemas, tool-version mismatch, list changes, concurrent
   calls, malformed frames, disconnects, cancellation, cleanup, and security
   denial before transport or exposed effects.

Use the official SDK when it preserves these seams; wrap it at the integration
boundary rather than reshaping AgentKit around SDK types.
