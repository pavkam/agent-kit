# Network access and egress

**Status:** Normative host boundary

**Architecture:** [Network access](../architecture/network.md)

**Depends on:**
[Permissions, approvals, and trust](permissions-approvals-and-trust.md),
[cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md),
[error taxonomy](error-taxonomy.md)

## Purpose

Outbound network activity is a protected host effect. Inbound listeners belong
to hosting/protocol leaves and enter through authenticated admission. This
specification defines the observable behavior every network implementation MUST
provide so that destinations are canonical, egress is authorized against the
data actually sent, and bounds are enforced while streaming rather than after
buffering.

It specifies the transport boundary. Provider protocol translation belongs to
the [provider request pipeline](provider-request-pipeline.md), and remote
protocol semantics belong to [MCP integration](mcp-integration.md).

## Boundary ownership

Framework components, tools, and integrations MUST NOT create unrestricted
clients, resolve DNS, or open sockets directly. They MUST depend on narrow
contracts that separate destination resolution, connection policy, request
transport, streaming bodies, redirects, and response limits.

A consumer MUST request only the capability it needs. A universal network client
is forbidden. Unsupported schemes, streaming modes, proxy features, certificate
policies, or request sizes MUST be declared as capabilities and MUST fail
preflight or return a typed unsupported result. An implementation MUST NOT fall
back to an unrestricted client or raw socket.

Provider adapters, web tools, MCP transports, remote stores, telemetry
exporters, and discovery clients MUST use this boundary whenever their network
effects are governed by AgentKit.

## Canonical destination identity

Authorization has two steps. The caller canonicalizes the scheme, hostname,
port, and route without I/O and obtains a resolution grant. After authorized
resolution, it obtains a distinct send grant binding the resolved addresses,
method, redirect chain, exact request-body fingerprint, headers, and declared
data classification. DNS cannot be required as an unauthorized prerequisite to
constructing its own grant.

DNS names and resolved addresses are both security inputs. A grant bound to one
MUST NOT authorize rebinding, alternate addresses, redirects, proxy changes, or
protocol upgrades. Ambiguous destinations MUST be rejected.

## Authorization and data egress

Every request MUST pass through the shared security authority after canonical
construction and before DNS resolution, connection, or transmission.

The implementation MUST validate the bounded grant at each material boundary,
including resolution, connection, redirect, and upload. A change to the
destination, resolved address, security-relevant headers, method, or body MUST
invalidate the grant and return through authorization.

Policy MUST be able to distinguish public internet, private ranges, loopback,
local metadata services, named trusted services, tenant boundaries, and data
classifications. Default policy MUST reject unsupported schemes, credential
forwarding, unsafe redirects, DNS rebinding, and server-side request-forgery
paths.

Proxy and certificate policy MUST be explicit and MUST NOT become an authority
bypass. Credential resolution stays inside the owning leaf integration; secret
values MUST NOT enter security requests, audit records, or diagnostics.

Missing, expired, consumed, mismatched, or unauditable grants MUST deny before
DNS or egress, even when a higher-level tool call was already approved.

## Bounds and streaming

Operations MUST declare deadlines, connection and response-size bounds, redirect
limits, streaming ownership, cancellation behavior, retry eligibility, and
idempotency.

Response bodies MUST be bounded while streaming. Buffering a complete response
and checking its size afterward does not satisfy this requirement. A declared
oversize response returns `NetworkResponseLimitExceeded` before ownership is
transferred; an actual streamed overrun throws
`NetworkResponseTooLargeException` from the owned body stream. An expired body
deadline throws `NetworkResponseTimedOutException`. None of these conditions may
be projected as truncated success.

Redirects MUST be counted, and each hop MUST be re-canonicalized and
re-authorized. A redirect MUST NOT silently carry credentials to a new origin.

## Retries and uncertainty

Retry ownership belongs to the calling provider, tool, or storage pipeline,
which MUST check idempotency and side-effect certainty first. The transport MAY
repeat only a phase it can prove was not observably sent.

A retry MUST NOT widen the destination or reuse an expired grant. Uncertain
egress MUST be reported as uncertain; the transport MUST NOT convert it into a
safe replay or a clean failure.

## Ownership and time

Response handles own their body streams. Disposing a handle releases or
invalidates the underlying connection according to framing state.

Connection pools MUST be partitioned by every security-relevant route,
credential audience, proxy, and certificate setting, and MUST NOT retain
run-scoped authority. Requests and responses are immutable or operation-owned.

Framework-owned deadlines, retry timing, and event timestamps MUST use the
injected `TimeProvider`. External certificate and protocol facts remain external
truth but MUST be captured safely for diagnostics. Shutdown MUST be bounded.

The actual pooled peer and route MUST satisfy each request's grant, including
connection reuse and multiplexing. Full-payload egress authority requires an
immutable body or separately authorized bounded staging before send; hashing a
one-pass body after sending is not enforcement. Hidden SDK redirects, retries,
credential operations, or transports cannot bypass these rules. See
[request evidence and reuse](../architecture/network.md#request-evidence-and-connection-reuse).

## Determinism and testing

A deterministic implementation MUST be able to script responses, resolutions,
failures, and timing, and MUST expose redacted operation traces so tests can
prove that denied data was never resolved, connected, or transmitted.

Conformance MUST NOT require public internet access.

## Acceptance scenarios

- A request denied by policy performs no DNS resolution, connection, or
  transmission.
- A hostname that resolves to a private or loopback address is rejected when
  policy forbids that range, even though the name itself was allowed.
- A destination that re-resolves to a different address after authorization
  denies instead of connecting.
- A redirect to a new origin is re-canonicalized and re-authorized, and
  credentials are not forwarded.
- A redirect chain exceeding its limit returns a typed result rather than
  following further.
- A response exceeding its declared or actual size bound stops before or during
  streaming with its typed result/exception rather than returning truncated
  content as success.
- A request body changed after authorization invalidates the grant.
- A retry after uncertain egress is refused unless the operation declared
  idempotency or the transport can prove nothing was sent.
- An expired or single-use-consumed grant denies before egress.
- Secret credential values never appear in security requests, audit records, or
  traces.
- Cancellation at resolution, connection, upload, and streaming each produce
  typed outcomes with accurate side-effect certainty.
- The same conformance suite passes against the real and deterministic
  implementations.

## Related specifications

- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [File-system access and bounds](file-system-access-and-bounds.md)
- [Process execution and sandboxing](process-execution-and-sandboxing.md)
- [Provider request pipeline](provider-request-pipeline.md)
- [MCP integration](mcp-integration.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
