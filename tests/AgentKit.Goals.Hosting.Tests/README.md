# AgentKit.Goals.Hosting.Tests

Focused tests for
[AgentKit.Goals.Hosting](../../src/AgentKit.Goals.Hosting/README.md).

**Component purpose:** run delegated children from durable intents.

Use this suite when changing the worker or investigating a regression. It is a
non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DelegationIntentProcessorTests](DelegationIntentProcessorTests.cs) — claim,
  run, settle, duplicate, deadline, stop, and process-loss behavior.
- [GoalDelegationWorkerTests](GoalDelegationWorkerTests.cs) — signalled and
  scanned drains, lazy start, and one-slot nested delegation.
- [DelegationWorkerSlotsTests](DelegationWorkerSlotsTests.cs) — slot bounds and
  wait parking.
- [EngineDelegationChildRunnerTests](EngineDelegationChildRunnerTests.cs) and
  [EngineAgentMessageChannelTests](EngineAgentMessageChannelTests.cs) — the
  engine-backed adapters over a real engine composition.

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```bash
dotnet test --project tests/AgentKit.Goals.Hosting.Tests -c Release
```
