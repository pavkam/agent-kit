# AgentKit.Conversations

Drive one ongoing conversation — session creation, message admission, and one
full agent-loop run — through a single `IConversationSession.SendAsync` call.

Composing that one working conversational turn otherwise requires an application
to call `ISessionCoordinator` to create and load a session, capture
authorization through `ISecurityProfileSelector` twice (once to admit the user's
message, once to authorize the run), append a `MessageSessionEntry` itself,
build an `AgentRunRequest` from its own instructions and tools, and run
`IAgentLoop` directly — all of it bypassing the `AgentEngine`/`Agent` facade,
which has no method to admit a message into a session before starting a run.
`DefaultConversationSession` performs all of that consistently for the common
case of one long-lived, single-branch conversation against one composed agent.

## Use this project

Register security, session, loop, and provider services exactly as any other
AgentKit composition, then add one conversation. This is the complete working
shape from
[`examples/QuickStart`](../../examples/QuickStart/QuickStartAgent.cs):

```csharp
var services = new ServiceCollection();

services.AddInMemorySecurityGrantStore();
services.AddStandaloneSecurityProfile(
    agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
    configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
services.AddAllowAllSecurityPolicy();

services.AddAgentSession();
services.AddInMemorySessionStore();
services.AddInMemorySessionDirectory(new ComponentId("app.session"));

services.AddAgentContext();
services.AddAgentOutput();
services.AddAgentLoop();
services.AddAgentTools();

services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAILlmModel(alias, modelId, OpenAIProviderDefaults.DefaultCapabilities);
services.AddModelDescriptors(new ModelDescriptorSourceId("app"), [descriptor]);

services.AddConversationSession(options =>
{
    options.AgentId = agentId;
    options.Identity = identity;
    options.SecurityProfileKey = securityProfileKey;
    options.AgentDefinitionRevision = definitionRevision;
    options.ConfigurationVersion = configurationVersion;
    options.SessionProfile = sessionProfile;
    options.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
    options.Instructions.Add(systemMessage);
    options.MaxTurns = 4;
});
```

When the agent has tools, add each tool's `LlmToolDefinition` to `options.Tools`
and, optionally, a `ConversationToolPresentationBinding` pairing the exact
captured `ToolDescriptor` with that definition so live tool events carry a
bounded presentation.

Resolve `IConversationSession` and call `SendAsync` for each user message:

```csharp
await using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
var conversation = provider.GetRequiredService<IConversationSession>();

var result = await conversation.SendAsync("List the files here.", cancellationToken);
foreach (var conversationEvent in result.Events)
{
    // ConversationAssistantTextEvent, ConversationToolCallEvent, ConversationToolResultEvent,
    // ConversationReasoningEvent, or ConversationUsageEvent
}
```

When an `IToolPresenter` is composed, live tool events carry its bounded
`ToolPresentation`. Calls and terminal results remain the original
`ToolCallPart` and loss-aware `ToolResultPart` presentation sources; the
conversation never reconstructs an authoritative execution record from a display
summary. Missing or mismatched evidence uses the presenter's generic fallback
path.

For live output, pass an `IConversationEventObserver` to the observing overload.
It receives assistant text and reasoning deltas, correlated tool starts and
results, usage updates, and exactly one `ConversationTurnCompletedEvent` before
`SendAsync` completes. Observer failures are isolated from the run. The returned
`Events` remain the committed projection, so a live UI should use the terminal
result for completion and recovery rather than replaying those events.

To continue a persisted conversation after restart, create a fresh configured
`IConversationSession` and call `OpenAsync(sessionId)` before the first send.
The operation loads through the selected session coordinator, directory, and
store, validates the configured agent and complete tenant/owner identity, and
pins the authoritative active branch. Once a session has been created or opened,
the instance cannot be rebound. `ListAsync(afterSessionId, maximumResults)`
returns stable, bounded directory pages for the same agent and owner. Discovery
does not probe stores and is available only when the explicitly selected
directory implements bounded enumeration.

After a session is opened or created,
`ReadHistoryAsync(afterSequence, maximumEntries)` returns immutable
`AgentMessage` values from `MessageSessionEntry` records on that bound branch
path. Start at `new SessionSequence(0)` and continue with each
`ConversationHistoryPage`'s `NextCursor` until `Complete` is true. The bound
counts scanned session entries, so a page containing operational records can
advance its cursor while returning fewer messages, or none. This keeps reads
finite without pretending non-message records are conversational content.
Authorization is captured for every page, and unavailable or malformed reads
return `ConversationHistoryUnavailable` without exposing stored content or
routing details.

Durability comes from the selected directory and session-store adapters. An
in-memory composition supports the same open/list semantics only within its
process lifetime; it does not become restart-safe merely by using this API.

Standalone compositions can return `OwnedConversationSession` with an
`IDisposable` composition owner, or `AsyncOwnedConversationSession` with an
`IAsyncDisposable` owner. Both wrappers explicitly forward `OpenAsync`,
`ListAsync`, `ReadHistoryAsync`, and both `SendAsync` overloads, including live
observer delivery. They dispose the supplied owner exactly once and reject
operations after disposal begins. The application must first complete or cancel
active conversation operations; disposal does not coordinate with an in-flight
turn.

This is not a replacement for `AgentEngine`: it does not host a catalog of
several agent definitions and does not implement the durable, queue-backed input
admission `AgentKit.IO` provides for multi-writer or distributed hosts. It is
the direct, in-process composition an application reaches for when it owns its
own `IServiceProvider` and wants one conversation with one agent — a terminal,
desktop, or single-tenant service host.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, see the
[repository root README](../../README.md) and the
[getting-started guide](../../docs/getting-started.md).

[Project catalog](../../docs/packages/index.md)
