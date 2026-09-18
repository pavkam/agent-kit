# Local private assistant on Ollama

A lawyer, a clinician, or anyone with documents that must not leave the machine
wants an assistant that reads a folder of notes and answers questions about
them. There is no API key, no cloud provider, and no tolerance for an accidental
outbound request: the model runs locally through Ollama, the files stay in a
sandbox, and the agent has no network tool at all.

This page is also the pattern for using any provider other than OpenAI: the
sugar has `UseOpenAI`, and every other provider goes through `builder.Services`
followed by `UseModel(alias)`.

## What the agent needs

| Need                                | AgentKit part                                                                                  |
| ----------------------------------- | ---------------------------------------------------------------------------------------------- |
| A local model                       | `AddOllama`, `AddOllamaApiKeyCredential`, `AddOllamaLlmModel` from `AgentKit.Providers.Ollama` |
| Catalog entry the selector can pick | `AddModelDescriptors` with a `ModelDescriptor` for the same alias, then `UseModel(alias)`      |
| Read the notes, nothing else        | `UseWorkspace(notesRoot)` plus a policy that denies writes, processes, and network             |
| Conversations kept locally          | `UseSqliteSessions` on a path under the user's profile                                         |

## Compose the engine

```csharp
static AgentEngine CreatePrivateAssistant(string notesRoot, string sessionsPath)
{
    var alias = new ModelAlias("local");
    var modelId = new ModelId("llama3.1:8b");

    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseWorkspace(notesRoot)
        .UseSqliteSessions(sessionsPath)
        .UseModel(alias)
        .WithInstructions("You answer questions using only the notes in this folder. Cite the file you used.");

    // The provider block: adapter, credential, model, and catalog descriptor.
    builder.Services.AddAgentProviders();
    builder.Services.AddOllama(o => o.BaseAddress = new Uri("http://127.0.0.1:11434"));
    builder.Services.AddOllamaApiKeyCredential("ollama");   // local Ollama ignores it; the slot must not be empty
    builder.Services.AddOllamaLlmModel(alias, modelId, OllamaProviderDefaults.DefaultCapabilities, OllamaProviderDefaults.DefaultLimits);
    builder.Services.AddModelDescriptors(
        new ModelDescriptorSourceId("private-assistant"),
        [
            new ModelDescriptor(
                alias,
                OllamaProviderDefaults.ProviderId,
                OllamaProviderDefaults.ApiFamily,
                modelId,
                deploymentId: null,
                OllamaProviderDefaults.DefaultCapabilities,
                OllamaProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty)
        ]);

    builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();

    return builder.Build();
}
```

Two registrations are needed for the model because they answer different
questions. `AddOllamaLlmModel` says _how to call it_; `AddModelDescriptors` says
_what it is_ (capabilities, limits, price) so the selector can match it against
the agent's requirements. `UseOpenAI` does both for you; other providers leave
the descriptor to you. When the model is in the bundled known-model catalog,
`KnownModelCatalog.Default.TryFind(providerId, modelId, out var known)` and
`known.ToDescriptor(alias, apiFamily, baselineCapabilities)` fill it in.

`ReadOnlyWorkspacePolicy` is the one from
[Read-only code review in CI](code-review-in-ci.md); it denies `FileWrite`,
`DirectoryCreate`, `Process`, and `Network`.

## Use it

```csharp
var home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
await using var engine = CreatePrivateAssistant(
    notesRoot: "/Users/me/Notes",
    sessionsPath: Path.Combine(home, "private-assistant", "sessions.db"));

while (Console.ReadLine() is { } line)
{
    Console.WriteLine(await engine.AskAsync(line));
}
```

Because sessions are in SQLite, the next launch can list and reopen yesterday's
conversation with `engine.Conversation.ListAsync` and `OpenAsync`, as
[Storing conversations](../guides/storage.md) shows.

## What the framework guarantees

- **No egress path exists.** No network tool is registered, the policy denies
  `Network` anyway, and the sandboxed process runner is not registered, so a
  command cannot be run either. The only outbound connection is the one you
  configured to `127.0.0.1:11434`.
- **Capabilities are declared, not assumed.** Ollama speaks an OpenAI-compatible
  wire format, but the descriptor decides what the selector believes about tool
  calls, streaming, and reasoning. If a model cannot call tools, set
  `SupportsToolCalls = false` on its capabilities and a definition that needs
  tools gets a typed `NoCompatibleModel` selection outcome instead of a provider
  error in the middle of a turn.
- **Usage stays honest.** When the local server does not report token counts,
  `ModelUsage.ReportState` says so and the numbers stay `null`; they are never
  reported as zero.

## Switching providers later

The same shape works for `AgentKit.Providers.Anthropic`, `.OpenRouter`,
`.AzureOpenAI`, `.GoogleGemini`, and the rest of the
[provider catalog](../packages/index.md#model-providers): register the
provider's `Add<Provider>`, credential, and `Add<Provider>LlmModel`, publish a
descriptor for the alias, and call `UseModel`. Nothing in the agent definition
changes.

## What lives where

| Concern                              | Package                                                                    |
| ------------------------------------ | -------------------------------------------------------------------------- |
| Ollama adapter                       | [AgentKit.Providers.Ollama](../../src/AgentKit.Providers.Ollama/README.md) |
| Catalog, selection, known models     | [AgentKit.Providers](../../src/AgentKit.Providers/README.md)               |
| Provider wire reference              | [Ollama](../providers/ollama.md)                                           |
| Sandboxed file system and read tools | [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md)             |
| SQLite sessions                      | [AgentKit.Session.Sqlite](../../src/AgentKit.Session.Sqlite/README.md)     |

Next: [Web research assistant](web-research-assistant.md) ·
[Composing an application](../guides/composition.md)
