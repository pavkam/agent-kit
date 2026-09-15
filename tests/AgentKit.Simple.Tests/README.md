# AgentKit.Simple.Tests

Verifies `SimpleAgentBuilder` and `SimpleAgent`: every argument guard with its
exact `ParamName`, the named diagnostics `Build()` raises when a model,
identity, or storage/security registration is missing, single-use builder
semantics, tool advertising from `Services`, instruction ordering and request
settings, `UseModel` over a provider registered directly, and complete turns
against a loopback OpenAI stub including turn-limit and transport failures that
must not leak the API key.

Run: `dotnet test --project tests/AgentKit.Simple.Tests --configuration Release`

[Source project](../../src/AgentKit.Simple/README.md) ·
[Project catalog](../../docs/packages/index.md)
