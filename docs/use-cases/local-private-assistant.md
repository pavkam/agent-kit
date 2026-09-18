# Local private assistant on Ollama

A lawyer, a clinician, or anyone with documents that must not leave the machine
wants an assistant that reads a folder of notes and answers questions about
them. There is no API key, no cloud provider, and no tolerance for an accidental
outbound request: the model runs locally through Ollama, the files stay in a
sandbox, and the agent has no network tool at all.

This page is also the pattern for using any provider other than OpenAI: the
builder has `UseAnthropic`, `UseOllama`, `UseOpenRouter`, and `UseAzureOpenAI`
next to `UseOpenAI`, and every other provider goes through `builder.Services`
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
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseWorkspace(notesRoot)
        .UseSqliteSessions(sessionsPath)
        .UseOllama("llama3.1:8b", configure: o => o.BaseAddress = new Uri("http://127.0.0.1:11434/v1/"))
        .WithInstructions("You answer questions using only the notes in this folder. Cite the file you used.");

    builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();

    return builder.Build();
}
```

`UseOllama` registers the adapter, a placeholder bearer token a local server
ignores (pass a real key as the second argument for a remote server that
enforces one), the model, and a catalog descriptor. Ollama models are not in the
bundled known-model catalog, so that descriptor carries the adapter's default
capabilities and no context-window limit or prices. When you need the selector
to know more, for example that a small model cannot call tools, write the two
registrations yourself and pick the alias with `UseModel`:

```csharp
var alias = new ModelAlias("local");
var descriptor = new ModelDescriptor(
    alias,
    OllamaProviderDefaults.ProviderId,
    OllamaProviderDefaults.ApiFamily,
    new ModelId("llama3.1:8b"),
    deploymentId: null,
    OllamaProviderDefaults.DefaultCapabilities with { SupportsToolCalls = false },
    new ModelLimits(maxContextTokens: 8_192, maxOutputTokens: null),
    pricing: null,
    ExtensionData.Empty);

builder.Services.AddOllama(o => o.BaseAddress = new Uri("http://127.0.0.1:11434/v1/"));
builder.Services.AddOllamaApiKeyCredential("ollama");
builder.Services.AddOllamaLlmModel(descriptor);
builder.Services.AddModelDescriptors(new ModelDescriptorSourceId("private-assistant"), [descriptor]);
builder.UseModel(alias);
```

The two registrations answer different questions. `AddOllamaLlmModel` says _how
to call it_; `AddModelDescriptors` says _what it is_ so the selector can match
it against the agent's requirements. For providers the catalog covers,
`Add<Provider>KnownLlmModel(alias, modelId)` does both from the published facts.

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

`UseAnthropic(apiKey, modelId)`, `UseOpenRouter(apiKey, modelId)`, and
`UseAzureOpenAI(endpoint, apiKey, deploymentId, modelId)` are one-line swaps for
`UseOllama`. For the rest of the
[provider catalog](../packages/index.md#model-providers), register the
provider's `Add<Provider>`, its credential, and either
`Add<Provider>KnownLlmModel(alias, modelId)` when the catalog covers it or
`Add<Provider>LlmModel(descriptor)` plus `AddModelDescriptors`, then call
`UseModel(alias)`. Nothing in the agent definition changes.

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
