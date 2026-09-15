# AgentKit.Simple.Tests

Verifies the `AgentEngineBuilder` extensions and the `AgentEngine` conversation
extensions: every argument guard with its exact `ParamName`, the diagnostics
`Build()` raises when a model or identity is missing (and the engine's own
composition diagnostic when storage and security are), chaining in any order,
tool advertising from `Services` into both the model request and the published
`AgentDefinition`, instruction ordering and request settings, `UseModel` over a
provider registered directly, a pinned `AgentId`, and complete turns against a
loopback OpenAI stub including turn-limit and transport failures that must not
leak the API key.

Run: `dotnet test --project tests/AgentKit.Simple.Tests --configuration Release`

[Source project](../../src/AgentKit.Simple/README.md) ·
[Project catalog](../../docs/packages/index.md)
