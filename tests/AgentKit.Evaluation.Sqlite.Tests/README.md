# AgentKit.Evaluation.Sqlite.Tests

Focused tests for
[AgentKit.Evaluation.Sqlite](../../src/AgentKit.Evaluation.Sqlite/README.md).

**Component purpose:** persist evaluation results in one SQLite database.

Use this suite when changing the adapter, its shared planner source, or its
registration. It is a non-packable .NET 10 test project using xUnit v3 and
Shouldly, and it runs the shared result-store conformance suite.

## Run this project

From the repository root:

```bash
dotnet test --project tests/AgentKit.Evaluation.Sqlite.Tests -c Release
```
