# Network access

**Role:** Make outbound network activity replaceable, bounded, security-aware,
and deterministic in tests.

Framework components and tools do not create unrestricted clients, resolve DNS,
or open sockets directly. They depend on narrow network contracts in
AgentKit.Abstractions. AgentKit.Network supplies the first-party real
implementation; AgentKit.Network.InMemory supplies scripted responses,
resolutions, failures, timing, and traces.

## Contract boundaries

The abstraction separates destination resolution, connection policy, request
transport, streaming bodies, redirects, and response limits. Consumers request
only the capability they need rather than receiving a universal network client.

Canonical destination identity includes scheme, normalized host, resolved
addresses, port, route or operation, redirect chain, method, request-body
fingerprint, and declared data classification. DNS names and addresses are both
security inputs. A grant for one does not silently authorize rebinding,
alternate addresses, redirects, proxy changes, or protocol upgrades.

Inbound listener ownership stays with hosting and protocol leaves. The outbound
resolver/transport contracts below do not claim to implement listeners, peer
authentication, or request admission. An inbound leaf authenticates through its
host and enters AgentKit through ordinary admission and security boundaries.

## Security and data egress

The
[security request and grant model](../concepts/permissions-approvals-and-trust.md)
binds authority to the canonical destination, data classification, and request
fingerprint before any network phase starts.

Every request passes through the shared security authority after canonical
request construction and before DNS, connection, or data transmission. The
network implementation validates a bounded grant at each material boundary,
including resolution, connection, redirect, and upload. A change to destination,
resolved address, headers with security meaning, method, or body invalidates the
grant and returns through authorization.

Policies can distinguish public internet, private ranges, loopback, local
metadata services, named trusted services, tenant boundaries, and data
classifications. Default policy rejects ambiguous destinations, unsupported
schemes, credential forwarding, unsafe redirects, DNS rebinding, and server-side
request-forgery paths. Proxy and certificate policy are explicit; neither
becomes an authority bypass.

Provider adapters, web tools, MCP transports, remote stores, telemetry
exporters, and update or discovery clients use this same boundary when their
network effects are governed by AgentKit. Credential resolution remains inside
the leaf integration and secret values never enter security requests or audit.

## Bounds and lifecycle

Deadlines and retry eligibility follow the
[cancellation and resilience contract](../concepts/cancellation-timeouts-and-resilience.md);
the transport cannot turn uncertain egress into a safe replay.

Operations declare deadlines, connection and response-size bounds, redirect
limits, streaming ownership, cancellation, retry eligibility, and idempotency.
Retries do not widen the destination or reuse an expired grant. Response bodies
are bounded while streaming, not only after buffering.

AgentKit.Network uses TimeProvider for framework-owned deadlines, retry timing,
and event timestamps. External certificate and protocol facts remain external
truth but are captured safely for diagnostics.

## Contract shape

DNS resolution and request transport are separate capabilities. A consumer that
only needs a provider-neutral request transport does not receive raw socket or
DNS APIs.

```csharp
namespace AgentKit;

public readonly record struct NetworkOperationId(Guid Value);
public sealed record NetworkDestination(
    string Scheme, NormalizedHost Host, int Port, NetworkRoute Route);

public sealed record NetworkResolutionRequest(
    NetworkOperationId Id,
    NetworkDestination Destination,
    NetworkBounds Bounds,
    SecurityGrant Grant);

public sealed record NetworkRequest(
    NetworkOperationId Id,
    NetworkMethod Method,
    NetworkDestination Destination,
    NetworkHeaderSet Headers,
    NetworkRequestContent? Content,
    NetworkBounds Bounds,
    ImmutableArray<NetworkAddress> ResolvedAddresses,
    NetworkDataClassification Classification,
    SecurityGrant Grant);

public interface INetworkNameResolver
{
    ComponentId SecurityAudience { get; }

    ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken);
}

public interface INetworkTransport
{
    ComponentId SecurityAudience { get; }

    ValueTask<NetworkSendResult> SendAsync(
        NetworkRequest request,
        CancellationToken cancellationToken);
}

public interface INetworkResponse : IAsyncDisposable
{
    NetworkResponseMetadata Metadata { get; }
    Stream Content { get; }
}
```

`NetworkOperationId` is a validated readonly value; callers use
`IIdentifierGenerator<NetworkOperationId>` for new operations. The minimum
composition selects one engine-wide resolver/transport pair through ordinary DI
replacement. A host supporting several profiles must expose explicit keyed
selection and capture the selected pair and immutable options in its operation
binding; registration order is never profile selection. `NormalizedHost`,
resolved address values, route, headers, classifications, and bounds have
canonical serializers and validation; a URI string is not itself a security
decision. Credential values are represented by private leaf-adapter content and
are excluded from request display, security records, and audit.

Resolution returns typed addresses with source, expiry, and canonical-host
evidence. Sending returns a discriminated result or a response handle. Redirect,
authentication challenge, denial, limit, cancellation, timeout, unsupported
scheme, and transport failure remain typed outcomes. A redirect is a new
destination requiring policy and grant evaluation; it is not hidden inside a
successful response.

The caller owns and asynchronously disposes a returned response handle. Declared
oversize responses return `NetworkResponseLimitExceeded`; actual streamed
overruns and body-deadline expiry throw the provider-neutral
`NetworkResponseTooLargeException` and `NetworkResponseTimedOutException` from
the owned stream. Cancelling `SendAsync` stops awaiting/start work according to
the returned side-effect certainty; it does not claim bytes were unsent when the
transport cannot prove that.

## First-party classes and service dependencies

| Package class                                                                             | Role and injected dependencies                                                                                                                          |
| ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DefaultNetworkNameResolver` in AgentKit.Network                                          | Bounded system resolution, configured address policy, `TimeProvider`, `ISecurityGrantStore` validation/consumption, and required audit                  |
| `DefaultNetworkTransport` in AgentKit.Network                                             | Connection/request/redirect/response enforcement over host-provided primitives, resolver, security authority/grant store, audit, and safe observability |
| `ScriptedNetworkNameResolver` and `ScriptedNetworkTransport` in AgentKit.Network.InMemory | Deterministic addresses, fragmentation, redirects, failures, timing, and redacted proof that no denied phase ran                                        |

The real resolver and transport inject `ISecurityGrantStore`, `TimeProvider`, an
immutable validated `AgentNetworkOptions` snapshot, and content-free logging.
Each exposes its own `SecurityAudience`. The resolver consumes a grant bound to
the canonical origin before IP-literal handling or DNS. The transport consumes a
different grant bound to the routed destination, exact resolved-address set,
method, secret-safe header/body fingerprints, data classification, and bounds
before policy checks or connection.

`DefaultNetworkTransport` disables automatic redirects and pins its socket to
one still-fresh, policy-eligible address carried in the authorized request; the
operating system never silently re-resolves the hostname for that connection.
TLS still uses the canonical DNS endpoint for SNI and certificate validation. A
redirect is returned as `NetworkRedirectReceived`; the caller must create a new
operation, resolve, and authorize the next hop. The transport performs no
retries.

Provider, MCP, store, observability, and tool packages depend on
`INetworkNameResolver` / `INetworkTransport`, never on AgentKit.Network or an
unrestricted `HttpClient`. Direct implementations remain supported; no transport
base class is required.

## Lifetime, concurrency, and ownership

Resolvers and transports are thread-safe singletons. The real transport owns one
handler and connection pool; requests carry operation authority but the pool
does not. Requests are immutable and responses are operation-owned. The
in-memory implementation is singleton-scoped to its registration,
lock-protected, and transfers every scripted response outcome exactly once so a
disposed body is never handed out again.

Response handles own their body streams; disposing one releases or invalidates
the underlying connection according to framing state. The container owns pools,
handlers, and resolver resources. Retries are not owned by this abstraction: the
calling provider/tool/storage pipeline retries only after checking idempotency
and side-effect certainty. A transport may repeat only a phase it can prove was
not observably sent. Shutdown is bounded and uses injected time.

## Request evidence and connection reuse

The caller first authorizes pure canonical origin data for resolution, then
passes that resolver grant to `INetworkNameResolver`. After resolution it
constructs and authorizes a separate exact send request. `INetworkTransport`
validates and consumes that send grant; it does not invent authority, perform
hidden DNS, or consume the resolution grant again.

A connection pool is partitioned by effective route, peer address, proxy, TLS
and credential-audience policy. Before each send, including multiplexed or
reused connections, the transport verifies that the actual peer and pool binding
satisfy the current grant and address freshness. Opening a socket once under an
earlier grant does not authorize later requests. Unsupported connection
coalescing or opaque SDK pooling fails capability validation.

The egress fingerprint binds bytes actually sent. A replayable immutable body
may be hashed directly. A one-pass stream requiring a full-payload fingerprint
must first be staged under a bounded, separately authorized spool operation;
after-send hashing cannot prevent unauthorized bytes from leaving. A declared
incremental authentication protocol is a separate capability. SDK automatic
retries, redirects, hidden credential refresh, and alternate transports must be
disabled or adapted through their own authorized operation boundaries.

## Dependency-injection registration

```csharp
namespace AgentKit.Network;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentNetwork(
            Action<AgentNetworkOptions>? configure = null);
    }
}
```

```csharp
namespace AgentKit.Network.InMemory;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentNetworkInMemory();
    }
}
```

Both methods use `TryAdd` so repeated registration is idempotent and explicit
host registrations remain replaceable through ordinary DI. Both require an
externally registered `ISecurityGrantStore`; neither fabricates authority.
`AddAgentNetwork` validates destination policy, resolution lifetime, and the
real handler's response-header ceiling. The in-memory package registers its
concrete scripted services as well as their interfaces so tests configure
scenarios and inspect phase traces through the same singleton.

## Build validation and unsupported behavior

Network is optional until a provider, web tool, MCP endpoint, remote store,
exporter, or other feature declares it. That feature must resolve both narrow
interfaces plus the system-wide authority and grant store. The baseline
real-leaf profile requires HTTP(S), explicit methods/content, pinned direct
connections, bounded headers and bodies, and platform TLS validation. Proxy
selection, custom certificate policy, automatic decompression, and multiple
network profiles are independent optional capabilities with explicit descriptors
and conformance; absence fails selection rather than falling back to
unrestricted behavior. Missing, expired, consumed, mismatched, or unauditable
grants deny before DNS or egress.

## Testing

Focused suites run the real implementation against loopback infrastructure and
the deterministic implementation without public network access. They cover exact
grant evidence and denial-before-effect, literal resolution, pinned loopback
connections, unfollowed redirects, secret-free fingerprints, declared and actual
response bounds, response-body deadlines, deterministic scenario ordering,
one-shot response ownership, and fail-closed DI. Any adapter advertising proxy,
decompression, or custom certificate capabilities also runs their focused
security and lifecycle suites.

Tests never require public internet access. The in-memory implementation exposes
redacted operation traces so assertions can prove that denied data was never
resolved, connected, or transmitted.

## Related concept specifications

- [Network access and egress](../concepts/network-access-and-egress.md)
- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
- [Provider request pipeline](../concepts/provider-request-pipeline.md)
- [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)

## Related architecture

- [Project structure](project-structure.md)
- [Tools](tools.md)
- [Security and human control](permissions-and-human-control.md)
- [MCP](mcp.md)
