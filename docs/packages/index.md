# Project catalog

Choose packages by the behavior your application needs. AgentKit targets .NET
10; each source project has a README with its purpose, registration entry
points, related projects, and reference documentation.

These tables describe the projects in the current solution. They are not a
promise that every architectural requirement is implemented or that a package is
published. Start with [Getting started](../getting-started.md), then the
[composition guide](../guides/composition.md).

Each source row links directly to its mirrored test project. Shared test
infrastructure is listed at the end.

## Engine and runtime

| Project                                                                        | Use it for                                                                                | Tests                                                            |
| ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| [AgentKit](../../src/AgentKit/README.md)                                       | Compose a process-level engine that hosts immutable agent definitions and isolated runs.  | [Tests](../../tests/AgentKit.Tests/README.md)                    |
| [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md)             | Implement AgentKit extensions against provider-neutral contracts and typed domain values. | [Tests](../../tests/AgentKit.Abstractions.Tests/README.md)       |
| [AgentKit.Loop](../../src/AgentKit.Loop/README.md)                             | Coordinate turns, context preparation, model attempts, tool calls, and terminal outcomes. | [Tests](../../tests/AgentKit.Loop.Tests/README.md)               |
| [AgentKit.Context](../../src/AgentKit.Context/README.md)                       | Assemble provider-ready context while preserving message trust and tool-call correlation. | [Tests](../../tests/AgentKit.Context.Tests/README.md)            |
| [AgentKit.Context.Compaction](../../src/AgentKit.Context.Compaction/README.md) | Select safe history cuts and produce, validate, and activate compaction checkpoints.      | [Tests](../../tests/AgentKit.Context.Compaction.Tests/README.md) |
| [AgentKit.Output](../../src/AgentKit.Output/README.md)                         | Resolve output definitions and validate, repair, or deserialize terminal candidates.      | [Tests](../../tests/AgentKit.Output.Tests/README.md)             |
| [AgentKit.IO](../../src/AgentKit.IO/README.md)                                 | Provide input-promotion policy and a broker for bounded human questions.                  | [Tests](../../tests/AgentKit.IO.Tests/README.md)                 |
| [AgentKit.Hooks](../../src/AgentKit.Hooks/README.md)                           | Dispatch typed lifecycle hooks with ordering, mutation validation, and failure policy.    | [Tests](../../tests/AgentKit.Hooks.Tests/README.md)              |
| [AgentKit.Identity](../../src/AgentKit.Identity/README.md)                     | Normalize trusted ingress identity and derive constrained delegated identities.           | [Tests](../../tests/AgentKit.Identity.Tests/README.md)           |
| [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)               | Evaluate security requests and manage bounded grants, profiles, and audit dispatch.       | [Tests](../../tests/AgentKit.Permissions.Tests/README.md)        |
| [AgentKit.Budgets](../../src/AgentKit.Budgets/README.md)                       | Reserve and account for capacity across hierarchical budget scopes.                       | [Tests](../../tests/AgentKit.Budgets.Tests/README.md)            |
| [AgentKit.Goals](../../src/AgentKit.Goals/README.md)                           | Coordinate protected task-delegation requests.                                            | [Tests](../../tests/AgentKit.Goals.Tests/README.md)              |
| [AgentKit.Observability](../../src/AgentKit.Observability/README.md)           | Share AgentKit logging, activity, metric, and tag conventions across components.          | [Tests](../../tests/AgentKit.Observability.Tests/README.md)      |

## Sessions and artifacts

| Project                                                                        | Use it for                                                                                | Tests                                                            |
| ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| [AgentKit.Session](../../src/AgentKit.Session/README.md)                       | Coordinate session lifecycle, run ownership, branching, and store routing.                | [Tests](../../tests/AgentKit.Session.Tests/README.md)            |
| [AgentKit.Session.InMemory](../../src/AgentKit.Session.InMemory/README.md)     | Keep session records and directory state in memory for ephemeral applications.            | [Tests](../../tests/AgentKit.Session.InMemory.Tests/README.md)   |
| [AgentKit.Artifacts](../../src/AgentKit.Artifacts/README.md)                   | Coordinate bounded preparation, finalization, and reading of generated or binary content. | [Tests](../../tests/AgentKit.Artifacts.Tests/README.md)          |
| [AgentKit.Artifacts.InMemory](../../src/AgentKit.Artifacts.InMemory/README.md) | Store artifact content in memory with deterministic lifecycle behavior.                   | [Tests](../../tests/AgentKit.Artifacts.InMemory.Tests/README.md) |

## Durable execution

| Project                                                                          | Use it for                                                                                            | Tests                                                             |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| [AgentKit.Durability.InMemory](../../src/AgentKit.Durability.InMemory/README.md) | Coordinate exclusive process-local ownership of durable operations and record their journal evidence. | [Tests](../../tests/AgentKit.Durability.InMemory.Tests/README.md) |

This is currently the only durable-execution package. The provider-neutral
coordinator, checkpoint store, and recovery policy described in
[durable execution](../architecture/durable-execution.md) remain unimplemented.

## Host access and scripted backends

| Project                                                                                      | Use it for                                                                                    | Tests                                                                   |
| -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md)                               | Access files through a root-bounded implementation of the filesystem contract.                | [Tests](../../tests/AgentKit.FileSystem.Tests/README.md)                |
| [AgentKit.FileSystem.InMemory](../../src/AgentKit.FileSystem.InMemory/README.md)             | Access files through a deterministic, disk-free implementation for tests and ephemeral hosts. | [Tests](../../tests/AgentKit.FileSystem.InMemory.Tests/README.md)       |
| [AgentKit.Network](../../src/AgentKit.Network/README.md)                                     | Resolve network destinations and send bounded HTTP requests through security enforcement.     | [Tests](../../tests/AgentKit.Network.Tests/README.md)                   |
| [AgentKit.Network.InMemory](../../src/AgentKit.Network.InMemory/README.md)                   | Run deterministic network-resolution and response scenarios with security checks.             | [Tests](../../tests/AgentKit.Network.InMemory.Tests/README.md)          |
| [AgentKit.Processes](../../src/AgentKit.Processes/README.md)                                 | Resolve and run operating-system processes through a bounded security boundary.               | [Tests](../../tests/AgentKit.Processes.Tests/README.md)                 |
| [AgentKit.Processes.Scripted](../../src/AgentKit.Processes.Scripted/README.md)               | Simulate process resolution and execution with deterministic scenarios.                       | [Tests](../../tests/AgentKit.Processes.Scripted.Tests/README.md)        |
| [AgentKit.LanguageServices.Scripted](../../src/AgentKit.LanguageServices.Scripted/README.md) | Supply deterministic language-intelligence responses for tests and replay.                    | [Tests](../../tests/AgentKit.LanguageServices.Scripted.Tests/README.md) |

## Model providers

| Project                                                                                        | Use it for                                                                                                 | Tests                                                                    |
| ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| [AgentKit.Providers](../../src/AgentKit.Providers/README.md)                                   | Catalog configured models, validate capabilities, and select or resolve model implementations.             | [Tests](../../tests/AgentKit.Providers.Tests/README.md)                  |
| [AgentKit.Providers.Anthropic](../../src/AgentKit.Providers.Anthropic/README.md)               | Connect AgentKit conversational models to Anthropic Claude through Messages.                               | [Tests](../../tests/AgentKit.Providers.Anthropic.Tests/README.md)        |
| [AgentKit.Providers.AwsBedrock](../../src/AgentKit.Providers.AwsBedrock/README.md)             | Connect AgentKit conversational models to Amazon Bedrock through Converse and ConverseStream.              | [Tests](../../tests/AgentKit.Providers.AwsBedrock.Tests/README.md)       |
| [AgentKit.Providers.AzureOpenAI](../../src/AgentKit.Providers.AzureOpenAI/README.md)           | Connect AgentKit conversational models to Azure OpenAI through GA v1 Chat Completions.                     | [Tests](../../tests/AgentKit.Providers.AzureOpenAI.Tests/README.md)      |
| [AgentKit.Providers.Cohere](../../src/AgentKit.Providers.Cohere/README.md)                     | Connect AgentKit conversational models to Cohere through v2 Chat.                                          | [Tests](../../tests/AgentKit.Providers.Cohere.Tests/README.md)           |
| [AgentKit.Providers.DeepSeek](../../src/AgentKit.Providers.DeepSeek/README.md)                 | Connect AgentKit conversational models to DeepSeek through its OpenAI-compatible chat surface.             | [Tests](../../tests/AgentKit.Providers.DeepSeek.Tests/README.md)         |
| [AgentKit.Providers.GoogleGemini](../../src/AgentKit.Providers.GoogleGemini/README.md)         | Connect AgentKit conversational models to Google Gemini through the Developer API GenerateContent surface. | [Tests](../../tests/AgentKit.Providers.GoogleGemini.Tests/README.md)     |
| [AgentKit.Providers.GoogleVertexAI](../../src/AgentKit.Providers.GoogleVertexAI/README.md)     | Connect AgentKit conversational models to Google Vertex AI through its generateContent surface.            | [Tests](../../tests/AgentKit.Providers.GoogleVertexAI.Tests/README.md)   |
| [AgentKit.Providers.Groq](../../src/AgentKit.Providers.Groq/README.md)                         | Connect AgentKit conversational models to Groq through its OpenAI-compatible chat surface.                 | [Tests](../../tests/AgentKit.Providers.Groq.Tests/README.md)             |
| [AgentKit.Providers.MistralAI](../../src/AgentKit.Providers.MistralAI/README.md)               | Connect AgentKit conversational models to Mistral AI through Chat Completions.                             | [Tests](../../tests/AgentKit.Providers.MistralAI.Tests/README.md)        |
| [AgentKit.Providers.MoonshotKimi](../../src/AgentKit.Providers.MoonshotKimi/README.md)         | Connect AgentKit conversational models to Moonshot Kimi through its OpenAI-compatible chat surface.        | [Tests](../../tests/AgentKit.Providers.MoonshotKimi.Tests/README.md)     |
| [AgentKit.Providers.Ollama](../../src/AgentKit.Providers.Ollama/README.md)                     | Connect AgentKit conversational models to Ollama through its OpenAI-compatible endpoint.                   | [Tests](../../tests/AgentKit.Providers.Ollama.Tests/README.md)           |
| [AgentKit.Providers.OpenAI](../../src/AgentKit.Providers.OpenAI/README.md)                     | Connect AgentKit conversational models to OpenAI through Chat Completions.                                 | [Tests](../../tests/AgentKit.Providers.OpenAI.Tests/README.md)           |
| [AgentKit.Providers.OpenAICompatible](../../src/AgentKit.Providers.OpenAICompatible/README.md) | Implement shared OpenAI-compatible request translation, transport, and streaming mechanics.                | [Tests](../../tests/AgentKit.Providers.OpenAICompatible.Tests/README.md) |
| [AgentKit.Providers.OpenRouter](../../src/AgentKit.Providers.OpenRouter/README.md)             | Connect AgentKit conversational models to OpenRouter through its OpenAI-compatible chat surface.           | [Tests](../../tests/AgentKit.Providers.OpenRouter.Tests/README.md)       |
| [AgentKit.Providers.XAI](../../src/AgentKit.Providers.XAI/README.md)                           | Connect AgentKit conversational models to xAI through its OpenAI-compatible chat surface.                  | [Tests](../../tests/AgentKit.Providers.XAI.Tests/README.md)              |
| [AgentKit.Providers.ZAI](../../src/AgentKit.Providers.ZAI/README.md)                           | Connect AgentKit conversational models to Z.ai through its OpenAI-compatible chat surface.                 | [Tests](../../tests/AgentKit.Providers.ZAI.Tests/README.md)              |

## Tools

| Project                                                                  | Use it for                                                                   | Tests                                                         |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------- | ------------------------------------------------------------- |
| [AgentKit.Tools](../../src/AgentKit.Tools/README.md)                     | Catalog, validate, authorize, and invoke application tools.                  | [Tests](../../tests/AgentKit.Tools.Tests/README.md)           |
| [AgentKit.Tools.Command](../../src/AgentKit.Tools.Command/README.md)     | Offer an explicit shell-command tool over the process boundary.              | [Tests](../../tests/AgentKit.Tools.Command.Tests/README.md)   |
| [AgentKit.Tools.Edit](../../src/AgentKit.Tools.Edit/README.md)           | Replace exact text under version conditions and filesystem bounds.           | [Tests](../../tests/AgentKit.Tools.Edit.Tests/README.md)      |
| [AgentKit.Tools.Glob](../../src/AgentKit.Tools.Glob/README.md)           | Match workspace paths using deterministic in-process globbing.               | [Tests](../../tests/AgentKit.Tools.Glob.Tests/README.md)      |
| [AgentKit.Tools.Language](../../src/AgentKit.Tools.Language/README.md)   | Request bounded language-intelligence operations through a selected service. | [Tests](../../tests/AgentKit.Tools.Language.Tests/README.md)  |
| [AgentKit.Tools.List](../../src/AgentKit.Tools.List/README.md)           | List directories with bounded, snapshot-based pagination.                    | [Tests](../../tests/AgentKit.Tools.List.Tests/README.md)      |
| [AgentKit.Tools.Patch](../../src/AgentKit.Tools.Patch/README.md)         | Parse and apply bounded workspace patches through the filesystem boundary.   | [Tests](../../tests/AgentKit.Tools.Patch.Tests/README.md)     |
| [AgentKit.Tools.Plan](../../src/AgentKit.Tools.Plan/README.md)           | Maintain versioned planning state through session-backed tool operations.    | [Tests](../../tests/AgentKit.Tools.Plan.Tests/README.md)      |
| [AgentKit.Tools.Question](../../src/AgentKit.Tools.Question/README.md)   | Ask a human a bounded question through the configured question broker.       | [Tests](../../tests/AgentKit.Tools.Question.Tests/README.md)  |
| [AgentKit.Tools.Read](../../src/AgentKit.Tools.Read/README.md)           | Read bounded file content through the filesystem abstraction.                | [Tests](../../tests/AgentKit.Tools.Read.Tests/README.md)      |
| [AgentKit.Tools.Resource](../../src/AgentKit.Tools.Resource/README.md)   | Load configured resources through bounded, security-aware operations.        | [Tests](../../tests/AgentKit.Tools.Resource.Tests/README.md)  |
| [AgentKit.Tools.Search](../../src/AgentKit.Tools.Search/README.md)       | Search file content deterministically within a bounded workspace scope.      | [Tests](../../tests/AgentKit.Tools.Search.Tests/README.md)    |
| [AgentKit.Tools.Skill](../../src/AgentKit.Tools.Skill/README.md)         | Activate captured skill content through an explicit security boundary.       | [Tests](../../tests/AgentKit.Tools.Skill.Tests/README.md)     |
| [AgentKit.Tools.Task](../../src/AgentKit.Tools.Task/README.md)           | Request bounded task delegation through the delegation broker.               | [Tests](../../tests/AgentKit.Tools.Task.Tests/README.md)      |
| [AgentKit.Tools.Web](../../src/AgentKit.Tools.Web/README.md)             | Fetch web content through bounded network operations and content projection. | [Tests](../../tests/AgentKit.Tools.Web.Tests/README.md)       |
| [AgentKit.Tools.WebSearch](../../src/AgentKit.Tools.WebSearch/README.md) | Search the web through an explicitly configured search provider.             | [Tests](../../tests/AgentKit.Tools.WebSearch.Tests/README.md) |
| [AgentKit.Tools.Write](../../src/AgentKit.Tools.Write/README.md)         | Write file content through an authorized filesystem boundary.                | [Tests](../../tests/AgentKit.Tools.Write.Tests/README.md)     |

## MCP

| Project                                                        | Use it for                                                                                    | Tests                                                    |
| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| [AgentKit.Mcp](../../src/AgentKit.Mcp/README.md)               | Describe reflected MCP tool contracts and keep protocol and tool-version identities explicit. | [Tests](../../tests/AgentKit.Mcp.Tests/README.md)        |
| [AgentKit.Mcp.Client](../../src/AgentKit.Mcp.Client/README.md) | Expose remote MCP tools through reflected, typed client surfaces.                             | [Tests](../../tests/AgentKit.Mcp.Client.Tests/README.md) |
| [AgentKit.Mcp.Server](../../src/AgentKit.Mcp.Server/README.md) | Expose reflected tool classes through an MCP server.                                          | [Tests](../../tests/AgentKit.Mcp.Server.Tests/README.md) |

## Shared tests and repository checks

| Project                                                                            | Purpose                                                         |
| ---------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| [AgentKit.Test.Shared](../../tests/AgentKit.Test.Shared/README.md)                 | Shared identity/security fixtures and activity observation.     |
| [AgentKit.Conformance](../../tests/AgentKit.Conformance/README.md)                 | Reusable behavioral suites exercised by concrete adapter tests. |
| [AgentKit.Architecture.Tests](../../tests/AgentKit.Architecture.Tests/README.md)   | Source project graph and dependency rules.                      |
| [AgentKit.Compatibility.Tests](../../tests/AgentKit.Compatibility.Tests/README.md) | Compiled public API snapshots and extractor checks.             |

The [testing guide](../testing/index.md) explains how to run each layer and what
its results establish. The
[project structure specification](../architecture/project-structure.md) also
includes planned packages that are not yet present in this catalog.

[Documentation home](../index.md) · [Contributing](../../CONTRIBUTING.md)
