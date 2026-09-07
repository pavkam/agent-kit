# Coding-harness MCP exposure

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:** [MCP integration](../../concepts/mcp-integration.md),
[tools](../../concepts/tools-and-toolsets.md),
[context](../../concepts/context-assembly-and-instructions.md)

## Purpose

MCP can contribute tools, resources, prompts, roots, sampling, elicitation, and
server instructions to a coding harness. Every contribution is untrusted remote
metadata until the owning AgentKit boundary validates, namespaces, authorizes,
and bounds it.

## Names and catalog identity

The canonical identity combines endpoint registration, negotiated server
identity, primitive kind, remote name, and descriptor revision. A sanitized
display or wire name is not identity. Sanitization, truncation, case folding,
and flattening can collide; catalog construction detects collisions and applies
a deterministic namespace/remapping policy before a model sees tools.

Reconnect or `tools/list_changed` creates a new immutable catalog generation.
Accepted calls resolve against the generation sent to the model. Map overwrite
and longest-prefix guessing are not valid resolution algorithms.

## Instructions, prompts, resources, and roots

Server instructions and prompt text enter context as attributed untrusted
sources. They MUST NOT be promoted directly to system/developer authority or
override managed instructions. Prompt arguments are schema-validated and output
is bounded before context assembly.

Resources and resource links preserve URI, media type, annotations, provenance,
and access policy. A link is not fetched implicitly. Embedded blobs use artifact
storage when they exceed request bounds. Unknown content blocks are preserved as
extension data or rejected observably, never silently discarded.

Workspace roots are disclosed only through an explicit endpoint profile and
security decision. Connecting a server does not grant it the process current
directory, repository root, sibling worktrees, home directory, or tenant paths.
Root updates bind canonical workspace identity and emit the negotiated protocol
notification.

## Paging, changes, and lifecycle

Tools, resources, prompts, and templates follow the protocol's cursor semantics
until exhaustion, with page/count/byte limits and loop detection. Partial
enumeration is labelled incomplete. Change notifications invalidate the affected
generation; they do not mutate an in-flight request catalog.

Initialization, capability negotiation, authentication, request correlation,
progress, cancellation, reconnect, and shutdown remain transport-owned and
bounded. A timeout reset on progress accepts only correlated valid progress and
still respects an overall deadline.

### OAuth credential and callback lifecycle

OAuth client registration, access/refresh tokens, expiry and scope, PKCE
verifier, and CSRF state are separate credential records bound to the exact MCP
endpoint registration, canonical server URL, account, and redirect profile. A
display name or reused endpoint key cannot move them to another audience.
Expired clients and tokens are refreshed or invalidated through typed state
transitions; silent fallback to credentials for an old URL is forbidden.

An authorization attempt owns its state, verifier, redirect, deadline,
cancellation, and callback-listener lease. Concurrent logins either serialize
per endpoint/account or use disjoint state and listener ownership. Dynamic
client information and tokens remain staged until the whole exchange succeeds,
then commit atomically; cancellation or partial failure cannot overwrite the
last known-good credential set.

A loopback callback listener binds the configured host, port, and path,
validates the exact state before accepting a code, rejects expired/replayed
callbacks, and is stopped when its final attempt settles. Listener startup,
shutdown, timeout, and cancellation are bounded. Finding the port occupied does
**not** prove that the expected callback listener is running; ownership must be
established or the attempt fails without sending the user to an unusable
redirect.

Server requests to sample, elicit, list roots, or perform another client-side
operation pass through the same identity, context, output, and security owners
as local calls. MCP metadata never grants those capabilities.

## Tool calls and results

Every MCP tool call passes normal resolution, schema validation, security,
scheduling, invocation, output normalization, recording, and settlement. Remote
`isError` maps to a tool failure without losing typed/structured content needed
for diagnostics. Text, image, audio, embedded resources, resource links, and
structured content have separate bounds and projection rules.

Nested code-mode/orchestration remains subject to per-child authorization and
budgets. The interpreter's sandbox is not permission to call every connected
server.

## Acceptance scenarios

- Two names that sanitize identically remain distinct or fail catalog
  publication; neither overwrites the other.
- An accepted call continues to resolve against its captured catalog after a
  `tools/list_changed` event.
- Connecting a server discloses no roots until an explicit authorized profile
  supplies them.
- Server instructions appear as attributed untrusted context, never system
  authority.
- Pagination detects a repeated cursor and reports incomplete enumeration.
- An uncorrelated progress flood cannot extend the overall request deadline.
- OAuth credentials for one canonical server URL are rejected after the endpoint
  moves to another URL.
- A callback port occupied by an unrelated process fails login before redirect;
  cancel and timeout remove the pending state without committing staged tokens.
- Resource links are not fetched merely while normalizing a tool result.
- Server sampling and elicitation use normal admission and permission policy.

## Interoperability pitfalls

Dynamic tool conversion does not justify flattened names that can collide,
automatic disclosure of the current directory as a root, promotion of server
instructions, or incomplete enumeration. Every converted primitive retains
canonical server identity, catalog generation, trust, and pagination evidence.

OAuth binds credentials to the canonical endpoint, stages them until PKCE and
callback-state validation succeed, and invalidates them when that binding
changes. Callback listener ownership, timeout, cancellation, and cleanup are
explicit; an occupied port is a login failure, never evidence that the expected
callback listener already exists.

## Related specifications

- [Coding-harness resources and project trust](coding-harness-resources-and-project-trust.md)
- [Coding-harness built-in tools](coding-harness-built-in-tools.md)
- [Permissions, approvals, and trust](../../concepts/permissions-approvals-and-trust.md)
- [Streaming and event protocol](../../concepts/streaming-and-event-protocol.md)
