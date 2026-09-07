# AgentKit application profiles

Application profiles show how hosts can compose AgentKit extension points for a
particular product class. They are not part of AgentKit's core behavioral
contract and do not require the framework to own application orchestration, user
interfaces, deployment topology, or product-specific policy.

A conforming profile may define stricter requirements for applications that
claim compatibility. Those requirements remain consumers of the core
[concept specifications](../concepts/index.md) and
[architecture](../architecture/index.md); they do not add reverse dependencies
or broaden a component's owner boundary.

## Available profiles

- [Coding harness](coding-harness/index.md) composes AgentKit into coding-agent
  hosts with workspaces, code-editing tools, terminals, language services,
  snapshots, resource discovery, protocol adapters, and optional control-plane
  integration.
