# MCP integration

**Status:** Normative integration boundary  
**Depends on:** [Tools and toolsets](tools-and-toolsets.md),
[permissions](permissions-approvals-and-trust.md)

## Purpose

MCP primitives enter AgentKit through the
[tool and toolset contracts](tools-and-toolsets.md), while every protected
remote effect remains subject to the
[security authority](permissions-approvals-and-trust.md).

The Model Context Protocol is a protocol integration, not a synonym for tools.
AgentKit keeps host policy, MCP client lifecycle, transport, protocol messages,
and primitive adapters separate.

## Roles and packages

AgentKit MUST state its role explicitly:

- a **host** coordinates model use, consent, roots, and client instances;
- a **client** connects to one MCP server and negotiates capabilities;
- a **server** exposes AgentKit-backed primitives to external clients.

Client and server support SHOULD live in separate leaf packages. The first
integration MAY implement host/client only. MCP SDK types MUST NOT leak into
`AgentKit.Abstractions`.

## Layering

```text
AgentKit host policy
  -> MCP primitive adapter
  -> MCP session/client lifecycle
  -> JSON-RPC correlation
  -> transport (stdio or HTTP)
```

Transport authorization is distinct from primitive authorization. A token that
permits connection to a server does not authorize the model to call every tool
or disclose every resource.

## Lifecycle and capabilities

The client MUST negotiate protocol version and capabilities before using a
method. It MUST enforce valid lifecycle state and advertised capability for each
request/notification. Unknown optional metadata is preserved for forward
compatibility; unsupported required behavior fails typed.

Protocol correlation IDs are separate from AgentKit run, message, and tool-call
IDs. Adapters maintain explicit mappings and reject duplicate, missing, or
mismatched responses.

List-change notifications MUST update an immutable versioned catalog snapshot.
An in-flight model request continues resolving against its original snapshot.

## Primitive semantics

- **Tools** are model-selected executable operations and adapt into AgentKit's
  tool discovery/execution pipeline.
- **Resources** are application-selected context/data and adapt into authorized
  retrieval or content sources.
- **Prompts** are user-selected templates/workflows and adapt into input or
  instruction sources only after trust classification.
- **Roots, sampling, and elicitation** are distinct client/host capabilities
  with separate consent and policy.
- **Progress, cancellation, logging, and completion** remain protocol utilities,
  not fake conversational messages or tools.

Mixed MCP content—text, image, audio, resource link, or embedded resource—MUST
remain typed. Stringifying non-text results is non-conforming.

## Tool adapter

MCP tool descriptors MUST receive stable source-qualified AgentKit identities.
Remote names, descriptions, schemas, annotations, and effect hints are
untrusted. Calls pass through canonical schema validation, the AgentKit security
authority, approval, scheduling, result bounds, and audit before and after the
remote request. The MCP operation grant does not replace the network or process
grant enforced by its HTTP or stdio transport.

AgentKit call ID and MCP request/call identifiers MUST remain correlated. A
disconnect after send yields unknown side-effect certainty unless idempotency or
server status proves an outcome.

## Transport ownership

Stdio transport MUST bound frame/message size, control the child environment,
drain or bound stderr, dispose streams, and terminate/reap its child process on
failure or host disposal. Server-provided commands MUST never be executed as a
discovery side effect.

HTTP transport MUST implement the current official transport and authorization
specification, bind credentials to the intended server/audience, bound redirects
and responses, and define reconnect/session behavior. Token pass-through to an
upstream API is forbidden.

Credentials use host credential abstractions and never enter prompts, messages,
events, logs, snapshots, or child arguments visible to unrelated processes.

## Server requests to the client

Sampling, elicitation, roots, and other reverse requests are untrusted remote
requests. They require explicit host capability and policy. A server MUST NOT
gain filesystem visibility, model budget, user data, or interactive authority
because it exposes a useful tool.

## Acceptance scenarios

- Calling a non-negotiated method fails before transport send.
- Duplicate/mismatched JSON-RPC correlation fails typed.
- Tool-list change does not redirect an in-flight call.
- Security denial prevents the MCP tool request and its transport effect.
- Stdio failure reaps the child and closes every owned stream.
- Mixed content round-trips without stringification.
- HTTP credentials cannot be forwarded across an unsafe redirect or upstream.

## Authoritative references

- The wire implementation MUST verify the current
  [MCP specification](https://modelcontextprotocol.io/specification/latest) at
  implementation time.
- Current security behavior MUST follow the official
  [security best practices](https://modelcontextprotocol.io/specification/latest/basic/security_best_practices)
  and authorization specification rather than a copied handshake.

## Related specifications

- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Memory, retrieval, and storage](memory-retrieval-and-storage.md)
- [Error taxonomy](error-taxonomy.md)
