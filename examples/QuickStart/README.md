# QuickStart

The smallest complete AgentKit agent: one OpenAI model, in-memory session and
security state, and no tools. It is the program the
[getting-started guide](../../docs/getting-started.md) walks through, kept in
the repository so it always compiles against the current libraries.

## Run it

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

The program prints the assistant's reply and exits with `0` when the turn
completed, or `1` when the run stopped for any other reason (the reason is
printed as the assistant text).

## What it composes

[`QuickStartAgent.cs`](QuickStartAgent.cs) registers, in order:

| Concern      | Registrations                                                                                             |
| ------------ | --------------------------------------------------------------------------------------------------------- |
| Security     | `AddInMemorySecurityGrantStore`, `AddStandaloneSecurityProfile`, `AddAllowAllSecurityPolicy`              |
| Session      | `AddAgentSession`, `AddInMemorySessionStore`, `AddInMemorySessionDirectory`                               |
| Turn loop    | `AddAgentContext`, `AddAgentOutput`, `AddAgentLoop`, `AddAgentTools`                                      |
| Model        | `AddAgentProviders`, `AddOpenAI`, `AddOpenAIApiKeyCredential`, `AddOpenAILlmModel`, `AddModelDescriptors` |
| Conversation | `AddConversationSession`                                                                                  |

`IConversationSession.SendAsync` then creates the session on first use, admits
the user message, runs the agent loop, and returns the committed events.

## Grow it

- Give the agent tools: add a tool package such as `AgentKit.Tools.Read`
  together with its host boundary (`AddSandboxedFileSystem`), then pass the tool
  definitions to `ConversationSessionOptions.Tools`. The
  [CodingAgent](../CodingAgent/README.md) example shows eight tools composed
  this way.
- Keep conversations across restarts: replace the two in-memory session
  registrations with `AddSqliteSessionStore` and `AddSqliteSessionDirectory` and
  mark the session profile `requiresDurableStore: true`.
- Replace `AddAllowAllSecurityPolicy` with your own `ISecurityPolicy`
  implementations and register an audit sink so audit delivery can stay
  `Required`.
- Switch providers by swapping the `AgentKit.Providers.OpenAI` registrations for
  another [provider package](../../docs/packages/index.md#model-providers); the
  alias the agent selects stays the same.

Target: **.NET 10**.
