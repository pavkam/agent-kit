# MCP

**Role:** Adapt the Model Context Protocol without confusing protocol access
with application authority.

MCP is a leaf integration, not a synonym for tools. AgentKit separates host
policy, protocol primitives, client or server lifecycle, request correlation,
transport, and authorization.

Client support belongs in AgentKit.Mcp.Client and server support in
AgentKit.Mcp.Server. Both depend on AgentKit.Abstractions and the official
protocol SDK or transport dependencies they require. Neither is referenced by
the facade, loop, tool runtime, or context package.

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

The [MCP integration contract](../concepts/mcp-integration.md) defines how
tools, resources, and prompts cross into AgentKit without becoming one universal
remote-capability abstraction.

MCP tools enter AgentKit's normal tool catalog, validation, security authority,
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

Connection authentication remains subordinate to the
[security authority](../concepts/permissions-approvals-and-trust.md) for every
protected effect exposed through that connection.

Stdio transport bounds messages, controls the child environment, drains stderr,
owns stream disposal, and terminates its child process on failure or host
shutdown. It uses AgentKit process abstractions and a process grant. Discovery
never executes a server-provided command as a side effect.

HTTP transport follows the current protocol and authorization specifications,
binds credentials to the intended audience, bounds redirects and responses, and
defines reconnection and session behavior through AgentKit network abstractions
and a network grant. A successful MCP login authorizes a connection, not every
tool, resource, root, model request, or user interaction.

OAuth state is endpoint- and audience-bound. Client registration, PKCE verifier,
CSRF state, staged tokens, refresh, invalidation, and any loopback callback
listener have explicit owners and lifetimes. A successful exchange atomically
commits one credential set; timeout, cancellation, occupied-port ambiguity, or a
changed endpoint commits nothing. Credential storage and callback hosting remain
integration leaves behind narrow contracts rather than mutable state on an MCP
session.

If a connection is lost after sending an effectful request, the result reports
unknown side-effect certainty unless idempotency or server reconciliation proves
otherwise.

## Contract shape

MCP-specific lifecycle and wire contracts are provider-neutral but belong to the
MCP packages; the core abstractions expose only the AgentKit tool, retrieval,
prompt, security, event, and identity contracts those adapters implement. This
keeps protocol SDK types out of AgentKit.Abstractions. A minimal client
extension surface is:

```csharp
namespace AgentKit;

public readonly record struct McpSessionId(Guid Value);
public readonly record struct McpRequestId(Guid Value);
public readonly record struct McpEndpointKey(string Value);
public readonly record struct McpServerKey(string Value);
public readonly record struct McpEndpointRevision(long Value);
public readonly record struct McpCapabilityProfileRevision(long Value);
public readonly record struct McpCatalogVersion(long Value);

public static class McpCapabilityIds
{
    public static CapabilityId Client { get; } = new("agentkit.mcp.client");
}

public sealed record McpEndpoint(
    McpEndpointKey Key,
    McpEndpointRevision Revision,
    McpTransportProfile Transport,
    McpAuthenticationReference? Authentication,
    McpEndpointBounds Bounds);

public sealed record McpCapabilityProfile(
    CapabilityId CapabilityId,
    CapabilityProfileId ProfileId,
    McpCapabilityProfileRevision Revision,
    ImmutableArray<McpEndpointKey> EndpointKeys);

public sealed record McpClientOpenRequest(
    McpCapabilityProfile CapabilityProfile,
    McpEndpoint Endpoint,
    ProtectedSemanticOperationContext Operation);

public sealed record McpCatalogSnapshot(
    McpSessionId SessionId,
    McpCatalogVersion Version,
    ImmutableArray<ToolDescriptor> Tools,
    ImmutableArray<McpResourceDescriptor> Resources,
    ImmutableArray<McpPromptDescriptor> Prompts,
    McpCapabilitySet Capabilities);

public abstract record McpRequest(
    McpRequestId Id,
    ProtectedSemanticOperationContext Operation,
    ToolCallId? ToolCallId);

public sealed record AuthorizedMcpRequest(
    McpRequest Request,
    SecurityGrant Grant);

public interface IMcpClientSession : IAsyncDisposable
{
    McpSessionId Id { get; }
    McpSessionState State { get; }

    ValueTask<McpInitializeResult> InitializeAsync(
        CancellationToken cancellationToken);

    ValueTask<McpCatalogSnapshot> GetCatalogAsync(
        CancellationToken cancellationToken);

    ValueTask<McpResponse> InvokeAsync(
        AuthorizedMcpRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<McpNotification> ReadNotificationsAsync(
        CancellationToken cancellationToken);
}

public interface IMcpClientSessionFactory
{
    ValueTask<IMcpClientSession> OpenAsync(
        McpClientOpenRequest request,
        CancellationToken cancellationToken);
}

public interface IMcpEndpointCatalog
{
    ValueTask<McpEndpointResolution> ResolveAsync(
        McpEndpointKey key,
        CancellationToken cancellationToken);
}

public interface IMcpCapabilityProfileCatalog
{
    ValueTask<McpCapabilityProfileResolution> ResolveAsync(
        AgentCapabilityReference capability,
        CancellationToken cancellationToken);
}

public interface IMcpServer
{
    Task RunAsync(
        McpServerEndpoint endpoint,
        CancellationToken cancellationToken);
}

public interface IMcpPrimitiveHandler
{
    ValueTask<McpResponse> HandleAsync(
        McpPeerContext peer,
        McpRequest request,
        CancellationToken cancellationToken);
}
```

`McpRequest` is a closed, typed request hierarchy for tools, resources, prompts,
roots, sampling, elicitation, and other negotiated operations; it is not an
event-name plus `object` escape hatch. `McpResponse` is a discriminated result
covering success, protocol failure, denial, unsupported capability,
cancellation, and unknown effect certainty. `McpSessionId` and `McpRequestId`
are validated readonly identifiers. JSON-RPC IDs remain typed external
correlation values and never replace `OperationId` or `ToolCallId`.
`McpEndpointKey` and `McpServerKey` are validated non-empty semantic keys from
configuration, never generated session/request identities.
`McpClientOpenRequest` captures the resolved endpoint revision and complete
execution identity, agent/session correlation, operation, and security-profile
context through `ProtectedSemanticOperationContext`. Opening or resuming a
connection never rediscovers a mutable current agent or silently substitutes a
newer endpoint or security profile. `McpCapabilityProfile` is owned by the MCP
package: it maps a neutral `AgentCapabilityReference` to MCP-owned endpoint keys
without exposing those keys in an agent definition. The reference uses the
stable `McpCapabilityIds.Client` capability ID; the catalog rejects any other ID
rather than treating a coincidentally equal profile name as MCP configuration.

The session validates lifecycle and negotiated capability before writing a
frame, validates that each request carries the same immutable identity and
authorization binding captured when the connection was opened, then validates
the MCP grant against the exact request. Its stdio or HTTP transport separately
obtains and enforces a process or network grant; an MCP grant cannot be replayed
at that lower boundary.

## First-party classes and dependencies

| Package class                                                           | Role and injected dependencies                                                                                                                                                                                                   |
| ----------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `McpClientSessionFactory` and `McpClientSession` in AgentKit.Mcp.Client | Protocol lifecycle, correlation, bounds, catalog snapshots, `IIdentifierGenerator<McpSessionId>`, `IIdentifierGenerator<McpRequestId>`, `TimeProvider`, security authority/grant store, audit, and a keyed MCP transport factory |
| `McpToolProvider` / `McpToolInvoker`                                    | Adapt a catalog snapshot to `IToolProvider` / `IToolInvoker`; use the ordinary tool validation, scheduling, security, result, and audit pipeline                                                                                 |
| `McpResourceSource` and `McpPromptSource`                               | Adapt resources or user-selected prompts through retrieval/context trust and security contracts; never inject them directly into history                                                                                         |
| `StdioMcpTransportFactory`                                              | Uses process contracts and a process grant; owns the child, bounded stderr, and streams                                                                                                                                          |
| `HttpMcpTransportFactory`                                               | Uses network contracts and a network grant; credential acquisition stays in the endpoint integration                                                                                                                             |
| `McpServer` and `AgentKitPrimitiveHandler` in AgentKit.Mcp.Server       | Authenticate the peer, map requests to selected AgentKit capabilities, and invoke the shared security authority before exposing any effect                                                                                       |

The client factory owns mechanics rather than policy or agent state:

```csharp
namespace AgentKit.Mcp.Client;

internal sealed record McpClientOptionsSnapshot(
    TimeSpan HandshakeTimeout,
    TimeSpan RequestTimeout,
    TimeSpan ShutdownTimeout,
    int MaximumFrameBytes,
    int MaximumMessageBytes,
    int MaximumInFlightRequests,
    McpUnknownNotificationPolicy UnknownNotificationPolicy);

internal sealed class McpClientSessionFactory(
    IMcpTransportFactoryCatalog transports,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<McpSessionId> sessionIds,
    IIdentifierGenerator<McpRequestId> requestIds,
    TimeProvider timeProvider,
    McpClientOptionsSnapshot options) : IMcpClientSessionFactory
{
    public ValueTask<IMcpClientSession> OpenAsync(
        McpClientOpenRequest request,
        CancellationToken cancellationToken) =>
        McpClientSession.OpenAsync(
            request,
            transports,
            securityAuthorities,
            grants,
            audit,
            sessionIds,
            requestIds,
            timeProvider,
            options,
            cancellationToken);
}
```

The official MCP SDK and JSON-RPC implementation are private dependencies of the
client or server package. `McpClientSession` does not depend on `AgentEngine`;
it depends on narrow services. `McpServer` may use public runner, tool,
retrieval, and output contracts, never a service locator or loop internals.

## Lifetime, concurrency, and ownership

`IMcpClientSessionFactory` and immutable endpoint registrations are normally
singletons and thread-safe. The factory retains no caller context; every
`OpenAsync` invocation receives it through `McpClientOpenRequest` and validates
that its selected authority, endpoint revision, complete execution identity, and
typed operation correlation still match before transport activation. Each result
is one connection session; the caller owns and asynchronously disposes it. A
future pooling implementation must return an explicit lease whose disposal
releases the lease rather than pretending the caller owns the shared connection.

A session permits concurrent correlated requests only when the negotiated
transport and server capabilities allow it. Writes are serialized, request IDs
are unique within the session, and each request completes once. Catalog
snapshots are immutable: notifications atomically publish a newer snapshot and
never mutate one held by an in-flight model request. Cancelling one request does
not close the connection unless transport failure makes the session unusable.
Disposing a session cancels outstanding work, bounds shutdown, closes owned
streams, and reaps an owned stdio child.

One process-level `AgentEngine` may use several MCP endpoints across concurrent
agents and runs. No connection stores mutable `AgentRunContext`; requests carry
only the bounded immutable correlation and authority they need.

## Dependency-injection registration

```csharp
namespace AgentKit.Mcp.Client;

public enum McpUnknownNotificationPolicy
{
    IgnoreAndDiagnose,
    FailSession
}

public sealed class McpClientOptions
{
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaximumFrameBytes { get; set; } = 1_048_576;
    public int MaximumMessageBytes { get; set; } = 4_194_304;
    public int MaximumInFlightRequests { get; set; } = 16;
    public McpUnknownNotificationPolicy UnknownNotificationPolicy { get; set; } =
        McpUnknownNotificationPolicy.IgnoreAndDiagnose;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMcpClient(
            Action<McpClientOptions>? configure = null) =>
            McpClientRegistration.Add(services, configure);

        public IServiceCollection AddMcpStdioEndpoint(
            McpEndpointKey key,
            Action<McpStdioEndpointOptions> configure) =>
            McpClientRegistration.AddStdioEndpoint(
                services,
                key,
                configure);

        public IServiceCollection ReplaceMcpStdioEndpoint(
            McpEndpointKey key,
            Action<McpStdioEndpointOptions> configure) =>
            McpClientRegistration.ReplaceStdioEndpoint(
                services,
                key,
                configure);

        public IServiceCollection AddMcpHttpEndpoint(
            McpEndpointKey key,
            Action<McpHttpEndpointOptions> configure) =>
            McpClientRegistration.AddHttpEndpoint(
                services,
                key,
                configure);

        public IServiceCollection ReplaceMcpHttpEndpoint(
            McpEndpointKey key,
            Action<McpHttpEndpointOptions> configure) =>
            McpClientRegistration.ReplaceHttpEndpoint(
                services,
                key,
                configure);

        public IServiceCollection AddMcpCapabilityProfile(
            CapabilityProfileId profileId,
            Action<McpCapabilityProfileOptions> configure) =>
            McpClientRegistration.AddCapabilityProfile(
                services,
                profileId,
                configure);

        public IServiceCollection ReplaceMcpCapabilityProfile(
            CapabilityProfileId profileId,
            Action<McpCapabilityProfileOptions> configure) =>
            McpClientRegistration.ReplaceCapabilityProfile(
                services,
                profileId,
                configure);

        public IServiceCollection ReplaceMcpClientSessionFactory<TFactory>()
            where TFactory : class, IMcpClientSessionFactory =>
            McpClientRegistration.ReplaceSessionFactory<TFactory>(services);

        public IServiceCollection ReplaceMcpEndpointCatalog<TCatalog>()
            where TCatalog : class, IMcpEndpointCatalog =>
            McpClientRegistration.ReplaceEndpointCatalog<TCatalog>(services);

        public IServiceCollection
            ReplaceMcpCapabilityProfileCatalog<TCatalog>()
            where TCatalog : class, IMcpCapabilityProfileCatalog =>
            McpClientRegistration.ReplaceCapabilityProfileCatalog<TCatalog>(
                services);
    }
}
```

```csharp
namespace AgentKit.Mcp.Server;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMcpServer(
            McpServerKey key,
            Action<McpServerOptions> configure) =>
            McpServerRegistration.Add(services, key, configure);

        public IServiceCollection ReplaceMcpServer(
            McpServerKey key,
            Action<McpServerOptions> configure) =>
            McpServerRegistration.Replace(services, key, configure);
    }
}
```

`AddMcpClient` `TryAdd`s one singular, explicitly replaceable
`IMcpClientSessionFactory`, `IMcpCapabilityProfileCatalog`, and
`IMcpEndpointCatalog`. Endpoint and server registrations are additive and keyed;
keys must be unique, and selection is explicit through those catalogs rather
than `IServiceProvider`. Primitive adapters are additive in deterministic order.
Transport factories are keyed by declared transport profile. Repeating an
identical registration is idempotent; conflicting reuse of a capability profile,
endpoint, transport, or server key is a composition error, never
last-registration-wins. An MCP capability profile maps a definition's neutral
`AgentCapabilityReference` and `CapabilityProfileId` to zero or more MCP-owned
`McpEndpointKey` values; definitions never carry endpoint keys directly. A run
captures the capability-profile, endpoint, and `McpCatalogVersion` revisions
together with `ProtectedSemanticOperationContext` before preparing an MCP
request. `AddMcpClient` validates its mutable binding options and captures one
immutable `McpClientOptionsSnapshot` for the provider lifetime. Stdio and HTTP
endpoint options are named by `McpEndpointKey`, validated, and copied into
immutable versioned `McpEndpoint` records; neither the singleton factory nor an
open session injects unkeyed or monitored endpoint options. The defaults are
finite client-mechanics bounds only. Server commands/URLs, credentials,
authentication audiences, and endpoint selection remain explicit host
configuration; AgentKit never invents them. Validation rejects non-positive
timeouts or limits, a frame limit larger than the message limit, and any
endpoint whose transport or credential reference is incomplete.

## Build validation and unsupported behavior

MCP remains optional. Registering a client endpoint validates its transport,
bounds, authentication reference, security authority/grant store and audit
collaborators, open-request profile/version binding, and the required network or
process implementation. Registering server mode validates its listener
transport, peer-authentication policy, primitive handlers, output bounds, and
shutdown ownership. Stdio without process enforcement and HTTP without network
enforcement fail composition; neither silently falls back to raw
operating-system APIs.

Unsupported negotiated methods return a typed capability result before a frame
is sent. Reverse requests are denied unless an explicit handler, budget, consent
path, and security policy are registered. Unknown required protocol behavior,
missing lifecycle state, duplicate correlation, grant mismatch, or unavailable
required audit fails closed. Optional unknown metadata is preserved as bounded
extension data; no implementation discovers normal unsupported behavior via a
mid-run `NotSupportedException`.

## Related concept specifications

- [MCP integration](../concepts/mcp-integration.md)
- [Tools and toolsets](../concepts/tools-and-toolsets.md)
- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
- [Coding-harness MCP exposure](../concepts/coding-harness-mcp-exposure.md)
