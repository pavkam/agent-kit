# Network access

**Role:** Make outbound and inbound network activity replaceable, bounded,
security-aware, and deterministic in tests.

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
public readonly record struct NetworkProfileKey(string Value);
public readonly record struct NetworkProfileVersion(long Value);

public sealed record NetworkDestination(
    string Scheme,
    NormalizedHost Host,
    int Port,
    NetworkRoute Route);

public sealed record NetworkResolutionRequest(
    NetworkOperationId Id,
    OperationId CausalOperationId,
    AgentId AgentId,
    RunId? RunId,
    NetworkDestination Destination,
    NetworkResolutionBounds Bounds);

public sealed record NetworkRequest(
    NetworkOperationId Id,
    OperationId CausalOperationId,
    AgentId AgentId,
    RunId? RunId,
    NetworkMethod Method,
    NetworkDestination Destination,
    NetworkHeaderSet Headers,
    NetworkRequestContent? Content,
    DataClassification Classification,
    NetworkRequestBounds Bounds);

public interface INetworkNameResolver
{
    ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        SecurityGrant grant,
        CancellationToken cancellationToken);
}

public interface INetworkTransport
{
    ValueTask<NetworkSendResult> SendAsync(
        NetworkRequest request,
        SecurityGrant grant,
        CancellationToken cancellationToken);
}

public interface INetworkSelector
{
    ValueTask<NetworkSelectionResult> SelectAsync(
        NetworkProfileKey key,
        CancellationToken cancellationToken);
}

public interface INetworkResponse : IAsyncDisposable
{
    NetworkResponseMetadata Metadata { get; }
    Stream Content { get; }
}
```

`NetworkOperationId` and all causal identities are validated readonly values;
callers use `IIdentifierGenerator<NetworkOperationId>` for new operations.
`NetworkProfileKey` is a validated non-empty configuration key selected through
options or an agent definition and is never generated as an operation ID.
`NormalizedHost`, resolved address values, route, headers, classifications, and
bounds have canonical serializers and validation; a URI string is not itself a
security decision. Credential values are represented by private leaf-adapter
content and are excluded from request display, security records, and audit.

Resolution returns typed addresses with source, expiry, and canonical-host
evidence. Sending returns a discriminated result or a response handle. Redirect,
authentication challenge, denial, limit, cancellation, timeout, unsupported
scheme, and transport failure remain typed outcomes. A redirect is a new
destination requiring policy and grant evaluation; it is not hidden inside a
successful response.

The caller owns and asynchronously disposes a returned response handle. Response
bounds are enforced while reading its stream. Cancelling `SendAsync` stops
awaiting/start work according to the returned side-effect certainty; it does not
claim bytes were unsent when the transport cannot prove that.

## First-party classes and service dependencies

| Package class                                                                             | Role and injected dependencies                                                                                                                          |
| ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DefaultNetworkNameResolver` in AgentKit.Network                                          | Bounded system resolution, configured address policy, `TimeProvider`, `ISecurityGrantStore` validation/consumption, and required audit                  |
| `DefaultNetworkTransport` in AgentKit.Network                                             | Connection/request/redirect/response enforcement over host-provided primitives, resolver, security authority/grant store, audit, and safe observability |
| `ScriptedNetworkNameResolver` and `ScriptedNetworkTransport` in AgentKit.Network.InMemory | Deterministic addresses, fragmentation, redirects, failures, timing, and redacted proof that no denied phase ran                                        |

The real transport depends on narrow mechanics and security services rather than
a provider, tool, or engine facade:

```csharp
namespace AgentKit.Network;

internal sealed record AgentNetworkOptionsSnapshot(
    NetworkProfileKey ProfileKey,
    NetworkProfileVersion ProfileVersion,
    NetworkDestinationPolicy DestinationPolicy,
    NetworkConnectionPolicy ConnectionPolicy,
    NetworkRequestBounds RequestBounds,
    NetworkResponseBounds ResponseBounds);

internal sealed record AgentNetworkProfileBinding(
    INetworkNameResolver Resolver,
    AgentNetworkOptionsSnapshot Options);

internal sealed class DefaultNetworkTransport(
    AgentNetworkProfileBinding profile,
    INetworkConnectionFactory connections,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    ISecurityAuditDispatcher audit,
    TimeProvider timeProvider) : INetworkTransport
{
    public ValueTask<NetworkSendResult> SendAsync(
        NetworkRequest request,
        SecurityGrant grant,
        CancellationToken cancellationToken) =>
        NetworkRequestExecution.SendAsync(
            request,
            grant,
            profile.Resolver,
            connections,
            securityAuthorities,
            grants,
            audit,
            timeProvider,
            profile.Options,
            cancellationToken);
}
```

Direct implementations remain supported; no transport base class is required.

Provider, MCP, store, observability, and tool packages depend on
`INetworkNameResolver` / `INetworkTransport`, never on AgentKit.Network or an
unrestricted `HttpClient`. Authentication remains in the leaf integration. The
real implementation re-canonicalizes and revalidates the grant before DNS,
connection, redirect, and upload; a different address, proxy route, certificate
identity, method, sensitive header audience, or body fingerprint returns through
security evaluation. If enforcement or required audit is unavailable, no next
network phase begins.

`ISecurityAuthoritySelector` selects only the authority key and profile version
captured in `SecurityGrant.Authorization` when a redirect or other changed phase
requires fresh evaluation; it never consults a latest agent definition.

## Lifetime, concurrency, and ownership

Resolvers and transports are thread-safe singletons and may pool safe connection
resources across many agents and runs in one `AgentEngine`. Pools are
partitioned by every security-relevant route, credential audience, proxy, and
certificate setting; they contain no run-scoped authority. Requests and
responses are immutable/operation-owned. The in-memory implementation is scoped
to its registration and concurrency-controlled.

Response handles own their body streams; disposing one releases or invalidates
the underlying connection according to framing state. The container owns pools,
handlers, and resolver resources. Retries are not owned by this abstraction: the
calling provider/tool/storage pipeline retries only after checking idempotency
and side-effect certainty. A transport may repeat only a phase it can prove was
not observably sent. Shutdown is bounded and uses injected time.

## Dependency-injection registration

```csharp
namespace AgentKit.Network;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentNetwork(
            NetworkProfileKey key,
            Action<AgentNetworkOptions> configure) =>
            NetworkRegistration.AddDefault(services, key, configure);
    }
}
```

```csharp
namespace AgentKit.Network.InMemory;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInMemoryNetwork(
            NetworkProfileKey key,
            Action<InMemoryNetworkOptions>? configure = null) =>
            InMemoryNetworkRegistration.Add(services, key, configure);
    }
}
```

```csharp
namespace AgentKit;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNetwork<TResolver, TTransport>(
            NetworkProfileKey key,
            NetworkCapabilities capabilities)
            where TResolver : class, INetworkNameResolver
            where TTransport : class, INetworkTransport =>
            NetworkServiceRegistration.Add<TResolver, TTransport>(
                services,
                key,
                capabilities);
    }
}
```

The first-party methods `TryAddKeyed` one resolver and transport pair per
`NetworkProfileKey`. Each capability is singular and replaceable for that key,
but composition rejects an incomplete or incompatible pair. Repeated identical
registration is idempotent; adding the in-memory package does not silently
displace a real implementation under an existing key.

Every network profile key owns named `AgentNetworkOptions`. Registration
validates and copies them into a package-owned immutable
`AgentNetworkOptionsSnapshot` containing the key and `NetworkProfileVersion`,
then supplies that snapshot to the keyed resolver/transport pair. Neither
service injects unkeyed `IOptions<T>`. Default options are captured for the
provider lifetime, not monitored in place; a validated new provider or versioned
profile publication is required for changes, and in-flight requests retain the
snapshot with which they began. The keyed transport factory closes over one
exact profile snapshot and the resolver registered under that same key,
constructing `AgentNetworkProfileBinding`; the transport never receives an
unkeyed resolver or performs runtime keyed lookup.

Multiple isolated network profiles are keyed, additive registrations selected by
a singular injected `INetworkSelector` using an explicit profile reference from
options or an agent definition. Keys are unique and stable. There is no additive
executor chain and no implicit last-registration-wins route. Network policy,
security audit, hooks, and observers remain additive through their own
contracts.

## Build validation and unsupported behavior

Network is optional until a selected provider, web tool, MCP endpoint, remote
store, exporter, or other feature declares it. Composition then validates a
matching resolver/transport profile, supported schemes/methods/streaming,
private-address and redirect policy, bounds, proxy/certificate options, security
authority and grant store, audit delivery, scopes, keyed options/profile-version
agreement, and key selection. Credential configuration is validated by its leaf
integration without exposing secrets.

Unsupported schemes, streaming modes, proxy features, certificate policies, or
request sizes are declared in `NetworkCapabilities` and fail preflight or return
a typed unsupported result. The implementation never falls back to unrestricted
clients or sockets. Missing, expired, consumed, mismatched, or unauditable
grants deny before DNS or egress. DNS rebinding, changed redirects, and changed
content cannot reuse the earlier grant, even when a higher-level tool was
approved.

## Testing

Shared conformance suites run against the real implementation with loopback
infrastructure and against AgentKit.Network.InMemory. They cover canonical
destinations, DNS changes, private-address blocking, redirects, proxy behavior,
certificate failures, request and response bounds, streaming fragmentation,
cancellation, retries, grant expiry and consumption, and denial before egress.

Tests never require public internet access. The in-memory implementation exposes
redacted operation traces so assertions can prove that denied data was never
resolved, connected, or transmitted.

## Related architecture

- [Project structure](project-structure.md)
- [Tools](tools.md)
- [Security and human control](permissions-and-human-control.md)
- [MCP](mcp.md)
- [Coding-harness built-in tools](../concepts/coding-harness-built-in-tools.md)
- [Coding-harness export, sharing, and control plane](../concepts/coding-harness-export-sharing-and-control-plane.md)
