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

Register the tools, provider, session store, and security policies your agent
needs exactly as any other AgentKit composition, then add:

```csharp
services.AddConversationSession(options =>
{
    options.AgentId = agentId;
    options.Identity = identity;
    options.SecurityProfileKey = securityProfileKey;
    options.AgentDefinitionRevision = definitionRevision;
    options.ConfigurationVersion = configurationVersion;
    options.SessionProfile = sessionProfile;
    options.ModelSelectionPolicy = new ModelSelectionPolicy([modelAlias]);
    options.Instructions.Add(systemMessage);
    options.Tools.AddRange(toolCatalog.Descriptors.ToLlmToolDefinitions());
});
```

Resolve `IConversationSession` and call `SendAsync` for each user message:

```csharp
var conversation = provider.GetRequiredService<IConversationSession>();
var result = await conversation.SendAsync("List the files here.");
foreach (var conversationEvent in result.Events)
{
    // ConversationAssistantTextEvent, ConversationToolCallEvent, ConversationToolResultEvent,
    // or ConversationUsageEvent
}
```

This is not a replacement for `AgentEngine`: it does not host a catalog of
several agent definitions and does not implement the durable, queue-backed input
admission `AgentKit.IO` provides for multi-writer or distributed hosts. It is
the direct, in-process composition an application reaches for when it owns its
own `IServiceProvider` and wants one conversation with one agent — a terminal,
desktop, or single-tenant service host.

Target: **.NET 10**. For a source-checkout setup and a runnable component
overview, see the [repository root README](../../README.md) and the
[getting-started guide](../../docs/getting-started.md).

[Project catalog](../../docs/packages/index.md)
