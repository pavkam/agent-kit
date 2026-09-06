---
name: agentkit-mcp
description:
  "Implement or debug AgentKit Model Context Protocol clients, servers,
  transports, lifecycle, capability negotiation, tools, resources, prompts,
  sampling, and authorization. Use for MCP protocol work; not for generic
  in-process tools."
---

# AgentKit MCP

Read [AGENTS.md](../../../AGENTS.md) and the
[MCP protocol model](references/protocol-model.md). Always verify wire details
against the latest official specification before implementation.

1. State AgentKit's role: host, client, server, or more than one explicit
   package. Do not blur host permission policy with protocol client behavior.
2. Keep JSON-RPC messages, lifecycle/version handling, transport, session state,
   capability negotiation, and AgentKit primitive adapters as separate layers.
3. Model MCP primitives faithfully. Tools are model-selected executable
   operations; resources are application-selected context; prompts are
   user-selected templates. Roots, sampling, elicitation, logging, progress, and
   cancellation are distinct client or utility capabilities, not tool aliases.
4. Respect advertised capabilities and the negotiated/current lifecycle. Never
   send a method merely because a concrete server happened to accept it.
   Preserve unknown metadata for forward compatibility.
5. Keep stdio process ownership and HTTP connection/authentication behavior
   behind transport contracts. Dispose child processes and streams on failure or
   cancellation, bound message sizes, and reject malformed JSON-RPC correlation.
6. Map MCP tools into AgentKit's tool description and invocation contracts only
   at the adapter boundary. Route every invocation through AgentKit permissions;
   server annotations and descriptions are untrusted and cannot authorize work.
7. Keep transport authorization separate from tool authorization. Never pass an
   MCP access token through to an upstream API, expose it to the model, or log
   it. Store credentials through host-provided credential abstractions.
8. Test initialization/lifecycle variants, capability rejection, request and
   notification correlation, concurrent calls, list-change events, progress,
   cancellation, disconnects, malformed frames, process cleanup, permission
   denial, and unknown extension data.

Use the official SDK when it preserves the required seams. Wrap SDK types at the
integration boundary so `AgentKit.Abstractions` remains protocol-library
independent.
