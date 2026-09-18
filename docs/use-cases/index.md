# Use cases

Each page takes one application you might build on AgentKit, explains what the
agent has to be able to do, and shows the composition that gives it exactly
those abilities and nothing more. The code is in snippets, not complete
programs: enough to see which registrations carry which responsibility and how
the finished engine is used.

Every page starts from the same working shape as
[Getting started](../getting-started.md) and grows it through
`builder.Services`. Where a use case leans on a package whose integration into
the turn loop is still tracked in the
[implementation ledger](../implementation-progress.md#component-coverage), the
page says so in a **Status** section rather than presenting the design as
finished.

## Worked examples in the repository

Two complete programs ship under `examples/`. These pages explain what each one
composes and why.

| Example                                | What it shows                                                                                                  |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| [QuickStart](quickstart-example.md)    | The smallest complete agent: builder sugar, one OpenAI model, local defaults, one instruction, `AskAsync`.     |
| [CodingAgent](coding-agent-example.md) | A terminal coding assistant written out on a raw `ServiceCollection`: SQLite sessions, approvals, eight tools. |

## Use cases

| Use case                                                                   | You will see                                                                                                        |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| [Customer support assistant in a web API](web-support-assistant.md)        | One engine for every customer: durable sessions, identity per request, resume by session id, streaming over SSE.    |
| [Read-only code review in CI](code-review-in-ci.md)                        | A sandboxed workspace, a policy that denies every mutation, review guidelines as a skill, usage and cost reporting. |
| [Documentation maintainer with approval](docs-maintainer-with-approval.md) | Writes scoped to one directory by inspecting protected resources, and a human approval step before each write.      |
| [Operations runbook executor](ops-runbook-executor.md)                     | Sandboxed shell commands, an executable allow-list, approval before execution, and a question tool for a human.     |
| [Local private assistant on Ollama](local-private-assistant.md)            | Swapping the provider through `builder.Services` and `UseModel`, with no data leaving the machine.                  |
| [Web research assistant](web-research-assistant.md)                        | The `web_fetch` tool behind an egress allow-list, and plugging in your own search provider for `web_search`.        |
| [Background ticket-triage worker](ticket-triage-worker.md)                 | One engine, eight concurrent ticket sessions, typed decisions, cancellation, telemetry, and required audit.         |
| [Order lookup with your own tool](domain-tool-integration.md)              | Writing an `ITool` over an application service, registering it, and allowing only the tools you name.               |
| [Delegating to specialist agents](delegating-to-specialists.md)            | Three agents on one engine: the `task` tool, the delegation broker, and a channel that sends the specialist a turn. |
| [Testing an agent without a live model](testing-agents-offline.md)         | A loopback HTTP handler, an in-memory file system you can seed, and asserting on committed events.                  |

## How to read the snippets

- `AgentEngine.CreateBuilder()` returns the real `AgentEngineBuilder`; the
  `Use*` and `With*` calls come from
  [`AgentKit.Simple`](../../src/AgentKit.Simple/README.md) and are sugar over
  registrations you could write by hand. `builder.Services` is the same
  `IServiceCollection` every registration lands on.
- `UseLocalDevelopmentDefaults()` appears in most pages because it is the
  working baseline: in-memory state, an allow-all policy, the process user as
  identity. Pages that describe a service show what replaces it. Deny always
  wins over allow, so adding a restrictive `ISecurityPolicy` next to the local
  defaults is a real restriction, not a suggestion.
- Snippets omit `using` directives, error handling, and configuration binding.
  Identifier and option names are the real ones from the packages named in each
  page's **What lives where** table.

Return to the [documentation home](../index.md), or read the
[developer guides](../guides/index.md) for the individual features these
compositions combine.
