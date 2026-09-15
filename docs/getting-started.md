# Getting started

This walkthrough builds and runs the smallest complete AgentKit agent: one
OpenAI model, in-memory session and security state, and no tools. You will see
every registration the agent needs, send it a message, and learn where tools,
durable storage, and other providers plug in.

The finished program lives in the repository as
[`examples/QuickStart`](../examples/QuickStart/README.md), so it always compiles
against the current libraries.

## Current status

AgentKit targets .NET 10 and is currently versioned `0.1.0-alpha.1` in
[Directory.Build.props](../Directory.Build.props). Work from a source checkout
when evaluating these instructions. The repository's version number does not
establish availability on a public package feed.

The composition below is the direct, in-process path: one conversation with one
agent, driven through `AgentKit.Conversations`. The `AgentEngine` facade that
hosts a catalog of several agents and queue-backed input admission exists but
its complete runnable graph is still tracked in the
[implementation ledger](implementation-progress.md#component-coverage). Durable
execution, cross-session memory/retrieval, and evaluation remain architectural
requirements without their full runtime packages.

## Set up the repository

Install the SDK selected by [global.json](../global.json), currently .NET
10.0.203 with compatible patch roll-forward. Node.js 22 or later and GNU Make
are needed for repository tooling.

```sh
git clone https://github.com/pavkam/agent-kit.git
cd agent-kit
make restore
make build
```

## Run the quick start

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

The program prints the assistant's reply. Credentials are never fabricated as
defaults: without the variable the program stops before composing anything.

## Read the composition

Everything below is in
[`QuickStartAgent.cs`](../examples/QuickStart/QuickStartAgent.cs). Each block is
one concern, and every call is a public `IServiceCollection` extension you can
replace.

Start with the identities that pin the agent's configuration. Keep them stable
across restarts so persisted sessions and security evidence keep matching the
agent that created them:

```csharp
var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
var definitionRevision = new AgentDefinitionRevision(1);
var configurationVersion = new ConfigurationVersion(1);
var securityProfileKey = new SecurityProfileKey("quickstart-security");
var authorityKey = new ComponentKey<ISecurityAuthority>("quickstart-authority");
var alias = new ModelAlias("assistant");
var modelId = new ModelId("gpt-4o-mini");

var services = new ServiceCollection();
```

Security comes first. A grant store records single-use authorizations, the
standalone profile binds a security authority to this agent, and at least one
`ISecurityPolicy` decides what is allowed. `AddAllowAllSecurityPolicy` is only
appropriate for a local, single-tenant tool; production hosts register their own
policies and an audit sink so delivery can stay `Required`:

```csharp
services.AddInMemorySecurityGrantStore();
services.AddStandaloneSecurityProfile(
    agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
    configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
services.AddAllowAllSecurityPolicy();
```

Session state needs the coordinator plus an explicitly selected store and
directory. `AddAgentSession` never installs a store on its own; the in-memory
pair below forgets everything when the process exits:

```csharp
services.AddAgentSession();
services.AddInMemorySessionStore();
services.AddInMemorySessionDirectory(new ComponentId("quickstart.session"));
```

The turn loop and its collaborators assemble context, run the model, validate
output, and invoke tools. No tool packages are registered yet, so the catalog is
empty:

```csharp
services.AddAgentContext();
services.AddAgentOutput();
services.AddAgentLoop();
services.AddAgentTools();
```

The model is registered under one alias. The agent selects the alias, never the
vendor. `AddOpenAIKnownLlmModel` registers both the concrete OpenAI adapter and
the provider-neutral catalog descriptor for it, taking the model's context
window, output limit, capabilities, and list prices from the
[known-model catalog](../src/AgentKit.Providers/README.md#known-model-catalog)
bundled in `AgentKit.Providers`:

```csharp
services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAIKnownLlmModel(alias, modelId);
```

A model the catalog does not know is registered explicitly instead: call
`AddOpenAILlmModel(alias, modelId, capabilities, limits)` and publish a matching
`ModelDescriptor` through `AddModelDescriptors`. For any other provider, look
the model up with `KnownModelCatalog.Default.TryFind`, build the descriptor with
`KnownModel.ToDescriptor(alias, apiFamily, baselineCapabilities)` using that
provider package's defaults, and register it the same way.

Finally, one conversation with one agent. The options carry the identities from
above, the session profile that selects the in-memory store, the model alias,
the system instruction, and the run limits:

```csharp
services.AddConversationSession(options =>
{
    options.AgentId = agentId;
    options.Identity = identity;
    options.SecurityProfileKey = securityProfileKey;
    options.AgentDefinitionRevision = definitionRevision;
    options.ConfigurationVersion = configurationVersion;
    options.SessionProfile = InMemorySessionProfile();
    options.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
    options.Instructions.Add(SystemMessage(agentId, "You are a concise assistant."));
    options.MaxTurns = 4;
    options.AttemptTimeout = TimeSpan.FromMinutes(1);
});
```

`identity` is the authenticated `ExecutionIdentity` of the person or workload
running the agent, and `InMemorySessionProfile()` builds a
`SessionProfileSnapshot` whose store key is `agentkit.in-memory`. Both helpers
are a few lines in the example file.

## Use the agent

Build the provider with validation on so a missing or ambiguous registration
fails here rather than in the middle of a run, then resolve the conversation:

```csharp
await using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
var conversation = provider.GetRequiredService<IConversationSession>();

var result = await conversation.SendAsync("In one sentence, what is AgentKit?", cancellationToken);

foreach (var text in result.Events.OfType<ConversationAssistantTextEvent>())
{
    Console.WriteLine(text.Text);
}
```

One `SendAsync` call creates the session on first use, admits the user message,
runs the agent loop until it completes or hits a limit, and returns the
committed events. Call it again on the same instance to continue the
conversation. `result.Succeeded` is `false` when the run stopped for any reason
other than a final assistant message; the reason is described in the assistant
text using only safe fields.

For live output while the turn runs, pass an `IConversationEventObserver` to the
observing `SendAsync` overload. It receives text and reasoning deltas,
correlated tool starts and results, and usage updates before the call returns.

## Grow the agent

- **Give it tools.** Register a tool package and the host boundary it needs, for
  example `AddReadTool()` with `AddSandboxedFileSystem(workspaceRoot)`, then add
  the tool definitions to `ConversationSessionOptions.Tools`. Registering a tool
  does not grant access to its resources; your `ISecurityPolicy` still decides.
  The [CodingAgent](../examples/CodingAgent/README.md) example composes eight
  tools this way.
- **Keep conversations across restarts.** Swap the two in-memory session
  registrations for `AddSqliteSessionStore` and `AddSqliteSessionDirectory`, set
  the profile's store key to `agentkit.sqlite`, and mark it
  `requiresDurableStore: true`. `OpenAsync(sessionId)` then resumes a persisted
  conversation.
- **Change providers.** Replace the `AgentKit.Providers.OpenAI` registrations
  with another [provider package](packages/index.md#model-providers). The alias,
  descriptor shape, and everything downstream stay the same.
- **Host several agents.** The [composition guide](guides/composition.md)
  explains the `AgentEngine` facade, its lifetimes, and the collaborators a
  complete engine composition requires.

To see the behavior this walkthrough relies on under test, run:

```sh
dotnet test --project tests/AgentKit.Conversations.Tests/AgentKit.Conversations.Tests.csproj --configuration Release --timeout 300s
```
