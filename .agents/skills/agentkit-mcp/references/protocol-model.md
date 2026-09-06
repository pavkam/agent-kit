# MCP Protocol Model

MCP is a protocol integration, not a synonym for tools. The authoritative source
is the
[latest MCP specification](https://modelcontextprotocol.io/specification/latest).
The latest revision may change lifecycle and transport requirements, so never
copy a dated handshake or event sequence from this file.

## Boundaries

- A host coordinates model integration, user consent, client creation, and
  security policy.
- A client speaks MCP to one server endpoint through a transport and exposes the
  server's negotiated capabilities to its host.
- A server exposes focused capabilities and may also request client features
  when the negotiated protocol permits them.
- JSON-RPC request IDs correlate requests and responses. Notifications do not
  receive responses. Protocol correlation is separate from AgentKit agent-run
  and tool-call correlation.

## Primitive semantics

The official
[server primitives overview](https://modelcontextprotocol.io/specification/latest/server)
distinguishes:

- prompts: user-controlled templates or workflows;
- resources: application-controlled context and data; and
- tools: model-controlled requests to execute operations.

Client features such as roots, sampling, and elicitation have their own trust
and consent boundaries. Utilities such as progress, cancellation, logging, and
completion remain protocol messages, not ordinary content.

Keep native MCP descriptors and results available beside AgentKit-normalized
forms. If an MCP result contains mixed text, images, audio, resource links, or
embedded resources, do not stringify or discard the non-text parts.

## Lifecycle and transport

Version and capability negotiation determine valid methods. MCP revisions may
use different stateful or stateless lifecycle models; isolate that difference
inside the protocol client rather than leaking it into tools or the agent loop.

Stdio and HTTP transports have different ownership and authorization rules.
Stdio requires bounded framing, stderr handling, child-process cleanup, and host
control of its environment. HTTP requires current transport semantics,
authentication challenges, audience-bound credentials, reconnect behavior, and
bounded response handling.

## Security

Follow the official
[security guidance](https://modelcontextprotocol.io/specification/latest/basic/security_best_practices)
and current authorization specification.

- Treat server descriptions, schemas, annotations, resources, and prompts as
  untrusted remote input.
- Require user/host policy for data exposure, sampling, elicitation, roots, and
  tool execution.
- Bind credentials to the intended MCP server and never perform token
  pass-through.
- Do not expose host filesystem roots or environment secrets merely because a
  server requests them.
