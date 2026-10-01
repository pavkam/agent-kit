# AgentKit.Conversations

Drive one ongoing conversation — session creation, message admission, and one
full agent-loop run — through a single `IConversationSession.SendAsync` call.

A conversation turn needs a session created or opened for one identity, the
user's message durably admitted, and one full agent-loop run. The engine's
`Agent` handle owns admission, lane protocol, and settlement; this package adds
the single-session convenience over it: `DefaultConversationSession` binds one
session and branch, sends each message through the `AgentEngine` admission path,
and projects the committed messages as conversation events. It covers the common
case of one long-lived, single-branch conversation against one composed agent.

## Use this project

For the shortest path, [`AgentKit.Simple`](../AgentKit.Simple/README.md) wraps
this package in a builder. When you own the `IServiceCollection`, register
security, session, loop, and provider services exactly as any other AgentKit
composition, then add one conversation:

```csharp
var builder = AgentEngine.CreateBuilder();
var services = builder.Services;

services.AddInMemorySecurityGrantStore();
services.AddInMemoryApprovalStore();
services.AddInMemorySecurityDecisionStore();
services.AddAllowAllSecurityPolicy();
services.AddAgentPermissions(o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
services.AddSecurityAuthority(authorityKey);

services.AddAgentSession();
services.AddInMemorySessionStore();
services.AddInMemorySessionDirectory(new ComponentId("app.session"));

services.AddAgentContext();
services.AddAgentOutput();
services.AddAgentLoop(AgentLoopComponentDefaults.LoopKey);
services.AddAgentIO(
    AgentIOComponentDefaults.InputCoordinatorKey,
    AgentIOComponentDefaults.OutputPublisherKey);
services.AddSessionBackedInputQueue();
services.AddAgentHooks();
services.AddAgentTools();
services.AddAgentBudgets();
services.AddBudgetProfile(AgentBudgetComponentDefaults.ProfileKey, _ => { });
services.AddInMemoryBudgetLedger();

services.AddAgentNetwork();     // provider egress sends through the network boundary
services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAIKnownLlmModel(alias, modelId);

services.AddAgent(definition);
services.AddSecurityProfilePublication(securityPublication);
services.AddAgentRunProfilePublication(runProfilePublication);

services.AddConversationSession(options =>
{
    options.Agent = definition;
    options.Configuration = effectiveConfiguration;
    options.Identity = identity;
    options.SessionProfile = sessionProfile;
});
```

The pinned `AgentDefinition` is the single source of the agent identity,
security profile, model policy, instructions, tool selection, turn limits, and
output contract, so the options carry no second copy of any of them. When the
agent has tools, optionally add a `ConversationToolPresentationBinding` pairing
each exact captured `ToolDescriptor` with its advertised `LlmToolDefinition` so
live tool events carry a bounded presentation.

Resolve `IConversationSession` and call `SendAsync` for each user message:

```csharp
await using var engine = builder.Build();
var conversation = engine.Services.GetRequiredService<IConversationSession>();

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
It receives, on the first observed turn, one `ConversationSessionBoundEvent`
naming the session and branch, then assistant text and reasoning deltas,
correlated tool starts and results, usage updates, and exactly one
`ConversationTurnCompletedEvent` before `SendAsync` completes. Observer failures
are isolated from the run. The returned `Events` remain the committed
projection, so a live UI should use the terminal result for completion and
recovery rather than replaying those events.

The bound session is readable at any time through `SessionId` and `BranchId`
(null until the first send or a successful `OpenAsync`), and every
`ConversationTurnResult` carries the `SessionId` it was recorded against and the
`RunId` allocated for that turn, so a host can hand out a resume token after the
first turn without a discovery round trip.

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

This is not a replacement for `AgentEngine`: it binds exactly one session on one
agent and does not host a catalog of several agent definitions or expose the
`Agent` operations (queued steering and follow-up input, cancel and attach by
`RunId`). It is the direct, in-process composition an application reaches for
when it wants one conversation with one agent — a terminal, desktop, or
single-tenant service host.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, see the
[repository root README](../../README.md) and the
[getting-started guide](../../docs/getting-started.md).

[Project catalog](../../docs/packages/index.md)
