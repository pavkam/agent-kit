# Coding-harness application profile

**Status:** Optional application profile

This profile specifies interoperability for applications that build a coding
harness with AgentKit. It is deliberately outside the core concept set: AgentKit
supplies narrow agent, session, I/O, tool, provider, host-access, artifact, and
security extension points, while the application owns workspace orchestration,
frontend behavior, deployment topology, and product policy.

Conformance to AgentKit does not require conformance to this profile. An
application that claims coding-harness profile compatibility follows the
requirements below without introducing an `AgentKit.Harness` god package or
moving application responsibilities into the framework.

## Profile map

- [Execution profile](coding-harness-execution-profile.md) defines the composed
  lifecycle and the ownership boundaries every supporting capability preserves.
- [Workspaces and worktrees](coding-workspaces-and-worktrees.md) defines project
  identity, provisioning, leases, reset, and removal.
- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
  defines safe edit transactions and settlement.
- [Built-in tools](coding-harness-built-in-tools.md) defines the tool surface a
  coding harness may compose from focused AgentKit features.
- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
  defines PTY ownership, attachment, output cursors, and cleanup.
- [Language services, formatters, and watchers](language-services-formatters-and-watchers.md)
  defines editor-service lifecycle and settled workspace observation.
- [Workspace snapshots and reversion](workspace-snapshots-and-reversion.md)
  separates file restoration from session and external-effect semantics.
- [Resources and project trust](coding-harness-resources-and-project-trust.md)
  defines bounded discovery, provenance, trust, and reload behavior.
- [Export, sharing, and control plane](coding-harness-export-sharing-and-control-plane.md)
  defines optional remote routing, reconnect, export, and import behavior.
- [Frontends and protocol adapters](coding-harness-frontends-and-protocol-adapters.md)
  defines TUI, IDE, batch, RPC, and agent-client projections.
- [MCP exposure](coding-harness-mcp-exposure.md) defines safe MCP names, roots,
  catalogs, paging, and nested calls.

Provider-specific interoperability evidence remains in the
[coding-harness provider profiles](provider-interoperability.md).
