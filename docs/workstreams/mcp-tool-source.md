# WS6: MCP as a tool source

Goal: AgentKit owns MCP client sessions, endpoint and capability-profile
catalogs, stdio and HTTP transports over the protected process and network
boundaries, an `IToolProvider` that exposes MCP tools through the normal tool
runtime, resource and prompt sources as context contributors, and an
`IMcpServer` that serves engine tools to peers. Today only a typed
reflection-based client over caller-supplied SDK transports exists.

Owning documents: [MCP](../architecture/mcp.md),
[MCP integration](../concepts/mcp-integration.md).

## Progress

- [x] WS6-C1a identities, endpoint, profile values
- [ ] WS6-C1b session, request, response contracts
- [ ] WS6-C2 client options, catalogs, DI
- [ ] WS6-C3 `McpClientSession` over the SDK client
- [ ] WS6-C4 `HttpMcpTransportFactory` over `INetworkTransport`
- [ ] WS6-C5 `StdioMcpTransportFactory` over `IProcessHandle`
- [ ] WS6-C6 `McpToolProvider` and `McpToolInvoker`
- [ ] WS6-C7 security and audit integration
- [ ] WS6-C8 resource and prompt sources
- [ ] WS6-C9a server runtime and tools primitive
- [ ] WS6-C9b server resources, prompts, peer authentication
- [ ] WS6-C10 Simple `WithMcpServer` and documentation

## Verified current state

| Item                                                                                                                                                                                                                                                                                                                                                                                                                    | State             | Evidence                                                                                                                                                                                                                                                                                          |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| identities, endpoint, and profile values from WS6-C1a (`McpSessionId`, `McpRequestId`, `McpEndpointKey`, `McpServerKey`, revisions, `McpCapabilityIds`, `McpEndpoint`, `McpTransportProfile`, `McpAuthenticationReference`, `McpEndpointBounds`, `McpCapabilityProfile`, `McpClientOpenRequest`) | EXISTS-UNWIRED (WS6-C1a) | `src/AgentKit.Mcp/`; no production consumer |
| remaining client/server/transport/primitive spec types (`IMcpClientSession(Factory)`, catalogs, `McpRequest/Response/Notification`, `McpCatalogSnapshot`, transport factories, `McpToolProvider/Invoker`, sources, `IMcpServer`, `IMcpPrimitiveHandler`, options) | MISSING | grep `src/` |
| `AgentKit.Mcp`                                                                                                                                                                                                                                                                                                                                                                                                          | reflection slice  | `McpCatalogVersion.cs:11`, `McpProtocolEra.cs:15`, `McpProtocolVersion.cs:14`, `McpProtocolVersions.cs:7`, `McpToolAttribute.cs:13`, `McpToolContract<TTools>.cs:15`, `McpToolMethodDescriptor.cs:9`, `McpToolName.cs:7`, `McpMetadataKeys.cs:7`, `AddMcpToolContract<TTools>`                    |
| `AgentKit.Mcp.Client`                                                                                                                                                                                                                                                                                                                                                                                                   | typed client only | `McpToolClientFactory<TTools>.ConnectAsync(IClientTransport, …)` (`:51-52`, caller supplies the raw SDK transport), `McpToolClient<TTools>`, internal `SdkMcpToolCaller.cs:12`, `McpClientToolCatalogSnapshot`, `McpClientVersionPolicy`, `McpRemoteTool(Descriptor)`, `AddMcpToolClient<TTools>` |
| `AgentKit.Mcp.Server`                                                                                                                                                                                                                                                                                                                                                                                                   | SDK builder only  | `AddAgentKitMcpServer(McpServerVersionPolicy?)`, `WithAgentKitTools<TTools>`, `McpServerVersionPolicy`                                                                                                                                                                                            |
| AgentKit-owned transport                                                                                                                                                                                                                                                                                                                                                                                                | NONE              | tests use SDK `StreamClientTransport` over pipes (`tests/AgentKit.Mcp.Client.Tests/SdkIntegrationTests.cs:174-202`)                                                                                                                                                                               |
| `IToolProvider` implementation, `ToolSourceId`                                                                                                                                                                                                                                                                                                                                                                          | NONE              | –                                                                                                                                                                                                                                                                                                 |
| security or audit integration                                                                                                                                                                                                                                                                                                                                                                                           | NONE              | –                                                                                                                                                                                                                                                                                                 |
| package refs                                                                                                                                                                                                                                                                                                                                                                                                            | –                 | Client → Mcp, Observability, `ModelContextProtocol.Core 2.2.0`; Server → Mcp, `ModelContextProtocol 2.2.0`; Simple references no Mcp package                                                                                                                                                      |
| `ProtectedSemanticOperationContext`, `AgentCapabilityReference`, `IOAuthAccessTokenProvider`                                                                                                                                                                                                                                                                                                                            | EXISTS            | but `AgentDefinition` has no `Capabilities` collection                                                                                                                                                                                                                                            |

No `IMcp*` test fakes exist.

## Hidden prerequisites

1. WS5 handle-based process execution before the stdio transport.
2. WS4-C4 spec `IToolInvoker` and WS4-C3/C8 capture coordinator and provider
   registration before `McpToolProvider` can reach a model.
3. `AgentDefinition.Capabilities` (WS18/WS4-C7) for
   `IMcpCapabilityProfileCatalog.ResolveAsync(AgentCapabilityReference)`.
4. Verify that SDK `IClientTransport`/`ITransport` can be implemented without
   `HttpClient` in ModelContextProtocol.Core 2.2.0 before sizing C4.
5. Security kinds for MCP operations may require new `ProtectedResourceKind` or
   `SecurityOperationKind` values (WS3 alignment, NO-SPEC).
6. Mcp packages stay leaves referencing Abstractions, Mcp, Observability; Simple
   adds a reference for `WithMcpServer`.
7. Namespace: the spec block says `namespace AgentKit;` (`mcp.md:162`) but
   `mcp.md:154-157` places these types in MCP packages and the package uses
   `AgentKit.Mcp`; use `AgentKit.Mcp` and note it.

## Spec coverage

| Contract                                                                                                                                                                                                                                                                                                    | Spec                     |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| identities, `McpCapabilityIds`, `McpEndpoint`, `McpCapabilityProfile`, `McpClientOpenRequest`, `McpCatalogSnapshot`, `McpRequest`, `AuthorizedMcpRequest`, session/factory/catalog interfaces, `IMcpServer`, `IMcpPrimitiveHandler`                                                                         | `mcp.md:161-266`         |
| `McpClientOptionsSnapshot`, `McpClientSessionFactory` deps                                                                                                                                                                                                                                                  | `mcp.md:310-347`         |
| `McpUnknownNotificationPolicy`, `McpClientOptions`, client `ServiceExtensions`                                                                                                                                                                                                                              | `mcp.md:382-474`         |
| server `ServiceExtensions`                                                                                                                                                                                                                                                                                  | `mcp.md:476-494`         |
| first-party class table                                                                                                                                                                                                                                                                                     | `mcp.md:296-306` (prose) |
| ~20 value types (`McpResponse`, `McpNotification`, `McpSessionState`, `McpInitializeResult`, descriptors, `McpCapabilitySet`, `McpPeerContext`, `McpServerEndpoint`, transport factory interfaces, endpoint options, resolutions) | NO-SPEC. `McpTransportProfile`, `McpAuthenticationReference`, and `McpEndpointBounds` landed in WS6-C1a; see the interim block below the contract shape in `mcp.md`. |
| Simple `WithMcpServer`, `AddMcpToolSource`                                                                                                                                                                                                                                                                  | NO-SPEC                  |

## Chunks

### WS6-C1a: Identities, endpoint, profile values

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables in `src/AgentKit.Mcp/`: `McpSessionId`, `McpRequestId`,
  `McpEndpointKey`, `McpServerKey`, `McpEndpointRevision`,
  `McpCapabilityProfileRevision`, `McpCapabilityIds`, `McpEndpoint`,
  `McpTransportProfile` (closed Stdio/Http), `McpAuthenticationReference`,
  `McpEndpointBounds`, `McpCapabilityProfile`, `McpClientOpenRequest`; tests.
  Snapshot: Mcp.
- Landed: types live in `AgentKit.Mcp`, not the spec block's `AgentKit`
  namespace, because `mcp.md` places them in the MCP packages. Guid and string
  identities reject empty values. Revisions are positive generations.
  `McpTransportProfile` is closed over stdio (command plus arguments, no
  environment) and HTTP (absolute credential-free HTTP(S) URI).
  `McpAuthenticationReference` stores a credential-profile key and audience,
  never a secret. `McpEndpointBounds` copies the positive limits later owned
  by `McpClientOptions`. Profiles accept only `McpCapabilityIds.Client`.
  `McpClientOpenRequest` rejects an endpoint key that the profile does not
  list. No production consumer yet. The compatibility snapshot is still
  outstanding until the shared solution build is green.

### WS6-C1b: Session, request, response contracts

- Depends on: C1a. Risk: ADDITIVE. Size: M.
- Deliverables: `McpRequest` hierarchy (tools, resources, prompts, roots,
  sampling, elicitation), closed `McpResponse`, `McpNotification`,
  `McpSessionState`, `McpInitializeResult`, `McpCatalogSnapshot`,
  `McpResourceDescriptor`, `McpPromptDescriptor`, `McpCapabilitySet`,
  `AuthorizedMcpRequest`, `IMcpClientSession`, `IMcpClientSessionFactory`,
  `IMcpEndpointCatalog`, `IMcpCapabilityProfileCatalog`, `IMcpTransportFactory`,
  `IMcpTransportFactoryCatalog`, resolutions. Snapshot: Mcp.
- Open: `IMcpTransportFactory` return type (SDK `IClientTransport` vs neutral
  streams).

### WS6-C2: Client options, catalogs, DI

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables in `src/AgentKit.Mcp.Client/`: `McpClientOptions`,
  `McpUnknownNotificationPolicy`, `McpClientOptionsSnapshot`,
  `McpStdioEndpointOptions`, `McpHttpEndpointOptions`,
  `McpCapabilityProfileOptions`, `McpClientRegistration`,
  `DefaultMcpEndpointCatalog`, `DefaultMcpCapabilityProfileCatalog`;
  `AddMcpClient`, `Add/ReplaceMcpStdioEndpoint`, `Add/ReplaceMcpHttpEndpoint`,
  `Add/ReplaceMcpCapabilityProfile`, `ReplaceMcp*Catalog<T>` (`mcp.md:385-473`);
  validation of timeouts, frame/message bounds, transport/credential
  completeness, duplicate keys. Snapshot: Mcp.Client.
- Open: where the "stdio without process enforcement fails" rule
  (`mcp.md:527-529`) is validated.

### WS6-C3: `McpClientSession` over the SDK client

- Depends on: C1b, C2. Risk: ADDITIVE. Size: L (scoped to initialize, negotiate,
  catalog, invoke tools).
- Deliverables: `McpClientSessionFactory.cs` (deps per `mcp.md:322-346`),
  `McpClientSession.cs`, `SdkMcpSessionAdapter.cs` (generalizes
  `SdkMcpToolCaller`), version floor via `McpClientVersionPolicy`, catalog
  snapshot with `McpCatalogVersion` increments on `tools/list_changed`,
  unknown-notification policy, in-flight limit; loopback pipe server tests via a
  test `IMcpTransportFactory`. The typed `McpToolClient<TTools>` remains the
  reflection slice.

### WS6-C4: `HttpMcpTransportFactory` over `INetworkTransport`

- Depends on: C3. Risk: ADDITIVE. Size: M–L.
- Deliverables: SDK `IClientTransport`/`ITransport` implementation mapping
  JSON-RPC frames to `NetworkRequest` POSTs and SSE responses; network grant per
  request or stream through `ISecurityAuthoritySelector`; redirect
  re-authorization; OAuth through `IOAuthAccessTokenProvider` bound to the
  endpoint audience; tests with `ScriptedNetworkTransport`.

### WS6-C5: `StdioMcpTransportFactory` over `IProcessHandle`

- Depends on: C3, WS5-C12, WS5-C13a. Risk: ADDITIVE. Size: M.
- Deliverables: `IProcessExecutor.StartAsync` under a process grant, SDK
  `StreamClientTransport` over the handle's stdin/stdout, bounded stderr drain,
  `TerminateAsync` on dispose or failure; tests with `ScriptedProcessExecutor`.

### WS6-C6: `McpToolProvider` and `McpToolInvoker`

- Depends on: C3, WS4-C4, WS4-C8. Risk: ADDITIVE. Size: M.
- Deliverables: `McpToolProvider : IToolProvider` (independent capture per
  request, `ToolSourceVersion` = `McpCatalogVersion`, no alias inference,
  metadata untrusted), `McpToolInvoker : IToolInvoker` (context → `tools/call`
  with `AuthorizedMcpRequest`; `isError` never decides status),
  `McpToolProviderCapture`, `AddMcpToolSource(ToolSourceId, McpEndpointKey)`;
  catalog change invalidates the capture; reuse `ToolProvider*ConformanceTests`.

### WS6-C7: Security and audit integration

- Depends on: C3. Risk: ADDITIVE. Size: M.
- Deliverables: per-request `SecurityRequest` through the selector, grant
  consumption via `ISecurityGrantStore`, audit records for connect, request,
  notification policy; tests for denial before the first frame and
  required-audit closure.

### WS6-C8: Resource and prompt sources

- Depends on: C3, WS9 contributor contracts. Risk: ADDITIVE. Size: M.
- Deliverables: `McpResourceSource`, `McpPromptSource` as context contributors
  with `RetrievedData` trust and provenance; never injected into history.

### WS6-C9a/C9b: `AgentKit.Mcp.Server`

- Depends on: C1b, WS4-C5a. Risk: ADDITIVE (existing builder extensions remain).
  Size: L split in two.
- C9a: `IMcpServer`, `McpServer`, `AgentKitPrimitiveHandler` mapping
  `tools/call` to `IToolExecutor` under the shared authority,
  `McpServerOptions`, `AddMcpServer(McpServerKey, …)`; loopback client lists and
  calls an engine tool. C9b: resources, prompts, `McpPeerContext`,
  `McpServerEndpoint`, peer authentication (NO-SPEC). Snapshot: Mcp.Server.

### WS6-C10: Simple `WithMcpServer` and documentation

- Depends on: C2, C6. Risk: ADDITIVE. Size: S–M.
- Deliverables: `WithMcpServer(McpEndpointKey, …)` (Simple references
  Mcp.Client); `mcp.md` acceptance list, mcp skill, READMEs.

## Totals

S 1, M 9, L 2 (split). Confidence low-medium: nearly everything is greenfield,
about twenty value types lack normative bodies, and SDK transport bridging
feasibility is unverified.
