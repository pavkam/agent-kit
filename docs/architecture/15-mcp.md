# MCP

**Role:** Adapt the Model Context Protocol without confusing protocol access
with application authority.

MCP is a leaf integration, not a synonym for tools. AgentKit separates host
policy, protocol primitives, client or server lifecycle, request correlation,
transport, and authorization.

## Roles and packages

The AgentKit host coordinates models, consent, roots, and MCP clients. An MCP
client connects to one server and negotiates capabilities. An MCP server exposes
selected AgentKit-backed primitives to external clients. Client and server
support belong in separate integration packages, and MCP SDK types never enter
AgentKit.Abstractions.

The internal layering runs from AgentKit host policy through a primitive
adapter, an MCP session, protocol correlation, and finally the transport. Each
layer has one lifecycle and one error boundary.

## Lifecycle and correlation

A client negotiates protocol version and capabilities before using a method.
Every request and notification is valid only in the appropriate lifecycle state.
Protocol request identities remain separate from AgentKit run, message, and tool
call identities, with explicit correlation between them.

List-change notifications publish a new immutable, versioned catalog. In-flight
model requests continue to resolve against the snapshot they originally saw.

## Primitive mapping

MCP tools enter AgentKit's normal tool catalog, validation, permission,
scheduling, result, and audit pipeline. Resources become authorized retrieval or
context sources. Prompts become user-selected input or instruction sources only
after trust classification.

Roots, sampling, elicitation, progress, cancellation, logging, and completion
remain distinct protocol capabilities. They are not disguised as conversational
messages or application tools. Reverse requests from a server require explicit
host support, budgets, consent, and policy.

Mixed text, image, audio, resource-link, and embedded-resource content remains
typed across the adapter. Remote names, descriptions, schemas, and effect hints
are untrusted.

## Transport and security

Stdio transport bounds messages, controls the child environment, drains stderr,
owns stream disposal, and terminates its child process on failure or host
shutdown. Discovery never executes a server-provided command as a side effect.

HTTP transport follows the current protocol and authorization specifications,
binds credentials to the intended audience, bounds redirects and responses, and
defines reconnection and session behavior. A successful MCP login authorizes a
connection, not every tool, resource, root, model request, or user interaction.

If a connection is lost after sending an effectful request, the result reports
unknown side-effect certainty unless idempotency or server reconciliation proves
otherwise.

## Related concept specifications

- [MCP integration](../concepts/mcp-integration.md)
- [Tools and toolsets](../concepts/tools-and-toolsets.md)
- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
