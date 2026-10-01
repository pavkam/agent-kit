# AgentKit.Evaluation.InMemory.Tests

Focused tests for
[AgentKit.Evaluation.InMemory](../../src/AgentKit.Evaluation.InMemory/README.md).

**Component purpose:** keep evaluation results in process memory ephemerally.

Use this suite when changing the adapter, its shared planner source, or its
registration. It is a non-packable .NET 10 test project using xUnit v3 and
Shouldly, and it runs the shared result-store conformance suite.

## Run this project

From the repository root:

```bash
dotnet test --project tests/AgentKit.Evaluation.InMemory.Tests -c Release
```
