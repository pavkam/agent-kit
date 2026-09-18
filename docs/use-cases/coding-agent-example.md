# Example: CodingAgent

`examples/CodingAgent` is a working terminal coding assistant: OpenAI for the
model, real sandboxed file and process tools, SQLite sessions, an approval flow
with three permission modes, a human-question channel, and a
[SharpVision](https://github.com/pavkam/sharp-vision) terminal UI. It exists to
prove AgentKit's production components end to end, and its
[README](../../examples/CodingAgent/README.md) records the gaps it found while
doing so.

Run it with:

```sh
cp examples/CodingAgent/.env.example examples/CodingAgent/.env.local   # set OPENAI_API_KEY
dotnet run --project examples/CodingAgent/CodingAgent.csproj -- /path/to/a/workspace
```

`--smoke-test` runs one headless turn and prints the committed events, which is
the shape the snippets below use.

## What it is for

Use this example as the reference for an agent that changes things: it writes
files, runs commands, and therefore needs durable history, an authorization
policy that can say "ask me first", and a channel back to the human. It is also
the reference for the written-out composition. There is no
`AgentEngine.CreateBuilder()` in it; every registration is on a raw
`ServiceCollection`, which is what a host that owns its own container writes.

## What it composes

`AgentRuntime.Create` builds the whole graph. Read it as blocks, each of which
[Composing an application](../guides/composition.md) explains:

```csharp
var services = new ServiceCollection();

// Security: grant and approval stores, a standalone profile, the policy that
// switches with the permission mode, and the handler/authorizer pair that
// puts the terminal user in the loop.
services.AddInMemorySecurityGrantStore();
services.AddInMemoryApprovalStore();
services.AddStandaloneSecurityProfile(
    agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
    configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
services.AddSingleton<ISecurityPolicy>(new CodingAgentSecurityPolicy(permissions));
services.RemoveAll<IApprovalHandler>();
services.AddSingleton<IApprovalHandler>(new CodingAgentApprovalHandler(approvals, identity, TimeProvider.System));
services.RemoveAll<IApprovalResponderAuthorizer>();
services.AddSingleton<IApprovalResponderAuthorizer>(new CodingAgentApprovalResponderAuthorizer(identity));

// Human questions: the tool's broker plus the channel that renders the prompt.
services.AddSingleton<IHumanQuestionChannel>(new CodingAgentHumanQuestionChannel(questions, identity, TimeProvider.System));
services.AddHumanQuestionBroker();

// Sessions: SQLite, outside the model-writable workspace.
services.AddAgentSession();
var sessionTarget = new SqliteSessionStoreTarget(
    sessionDatabasePath, sessionStoreInstanceId,
    SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
services.AddSqliteSessionStore(sessionTarget);
services.AddSqliteSessionDirectory(new ComponentId("coding-agent.session"), sessionTarget);

// Turn loop.
services.AddAgentContext();
services.AddAgentOutput();
services.AddAgentLoop(AgentLoopComponentDefaults.LoopKey);

// Tools and the host boundaries they act through.
services.AddReadTool();
services.AddWriteTool();
services.AddEditTool();
services.AddGlobTool();
services.AddSearchTool();
services.AddCommandTool(o =>
{
    o.SandboxProfile = PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile;
    o.EnvironmentVariables["PATH"] = CodingAgentHostEnvironment.CommandPath();
});
services.AddPlanTool();
services.AddQuestionTool();
services.AddAgentTools(o => o.AllowAllRegisteredTools = true);
services.AddSandboxedFileSystem(workspaceRoot);
services.AddOperatingSystemProcesses(workspaceRoot, o =>
{
    o.AllowedExecutablePaths.Add("/bin/sh");
    o.AllowedEnvironmentVariableNames.Add("PATH");
});

// Model: adapter, credential, model registration, and its catalog descriptor.
services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAILlmModel(alias, modelId, capabilities);
services.AddModelDescriptors(new ModelDescriptorSourceId("coding-agent"), [descriptor]);

// The conversation: identities, session profile, model policy, instructions, limits.
services.AddConversationSession(o =>
{
    o.AgentId = agentId;
    o.Identity = identity;
    o.SecurityProfileKey = securityProfileKey;
    o.AgentDefinitionRevision = definitionRevision;
    o.ConfigurationVersion = configurationVersion;
    o.SessionProfile = sessionProfile;
    o.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
    o.RequestSettings = LlmRequestSettings.Default with { ReasoningEffort = configuration.ReasoningEffort };
    o.Instructions.Add(SystemMessage(agentId, workspaceRoot));
    o.MaxTurns = configuration.MaximumTurns;
    o.AttemptTimeout = TimeSpan.FromMinutes(3);
});

var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
return new OwnedConversationSession(provider.GetRequiredService<IConversationSession>(), provider);
```

Two details are easy to miss and worth copying:

- **Tools reach the model through options, not magic.** A
  `Configure<IEnumerable<ITool>>` on `ConversationSessionOptions` turns every
  registered `ITool.Descriptor` into an `LlmToolDefinition` and a presentation
  binding. The builder sugar does the same thing for you.
- **The agent identity is derived from the workspace.** `AgentIdForWorkspace`
  hashes the normalized workspace path into an `AgentId`, so session discovery
  and reopening stay isolated per workspace even when several share one SQLite
  file.

## The policy with three modes

`CodingAgentSecurityPolicy` is the whole authorization story. Anything that is
not a workspace mutation or a process execution is allowed; those two are
decided by the current mode:

```csharp
var workspaceMutation = kind is SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate;
var processExecution = kind == SecurityOperationKind.Process && effect == SecurityEffect.Execute;

return !workspaceMutation && !processExecution
    ? new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "coding-agent.observe", "...")
    : mode switch
    {
        PermissionMode.ReadOnly =>
            new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "coding-agent.read-only", "..."),
        PermissionMode.AutoApproveWorkspaceEdits when workspaceMutation =>
            new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "coding-agent.workspace-edit", "..."),
        _ => new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "coding-agent.approval", "..."),
    };
```

Switching the mode at runtime changes the next decision without rebuilding
anything, because the policy reads a controller the UI mutates. Approvals are
bound to one exact request and expire; the handler checks the binding's expiry
before and after asking.

## How it is used

Headless, one turn:

```csharp
using var conversation = AgentRuntime.Create(
    workspaceRoot, apiKey, configuration,
    new AutoApprovePrompt(), new UnavailableHumanQuestionPrompt(), new PermissionModeController());

var result = await conversation.SendAsync(prompt, CancellationToken.None);

foreach (var conversationEvent in result.Events)
{
    Console.WriteLine(conversationEvent switch
    {
        ConversationAssistantTextEvent text => $"[text] {text.Text}",
        ConversationToolCallEvent call => $"[call] {call.ToolName}({call.ArgumentsJson})",
        ConversationToolResultEvent toolResult => $"[result] {toolResult.ToolName} succeeded={toolResult.Succeeded}: {toolResult.Summary}",
        ConversationUsageEvent usage => $"[usage] input={usage.Usage.InputTokens} output={usage.Usage.OutputTokens}",
        _ => conversationEvent.ToString(),
    });
}
```

Interactive, the screen is itself the `IConversationEventObserver`, so text
deltas, reasoning, tool calls, tool results, usage, and the completed event
reach the UI as they are committed:

```csharp
_ = await _conversation.SendAsync(userText, this, _turnCancellation.Token);

public ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken) =>
    Dispatcher!.InvokeAsync(() => AppendLiveEvent(conversationEvent), cancellationToken);
```

Cancelling `_turnCancellation` ends the turn with a
`ConversationTurnCompletedEvent` whose `Outcome` is `"cancelled"`; the session
remains consistent. Listing and resuming go through the same session:

```csharp
var page = (ConversationSessionPage) await conversation.ListAsync(null, 10, ct);
var opened = await conversation.OpenAsync(page.Sessions[0].SessionId, ct);
var history = (ConversationHistoryPage) await conversation.ReadHistoryAsync(new SessionSequence(0), 128, ct);
```

## Where to go next

- [Documentation maintainer with approval](docs-maintainer-with-approval.md) and
  [Operations runbook executor](ops-runbook-executor.md) each take one slice of
  this example and build it on the builder sugar.
- [Permissions and approvals](../guides/permissions.md) explains the policy
  algebra behind the three modes.

Next: [Customer support assistant in a web API](web-support-assistant.md) ·
[Use cases](index.md)
