# AgentKit.Evaluation.Live.Tests

Opt-in live verification for
[AgentKit.Evaluation](../../src/AgentKit.Evaluation/README.md).

**Component purpose:** prove `ModelJudgeEvaluator` and `ModelRequestJudgeClient`
work against a real model, which the offline suite deliberately never calls.

Every test here is skipped, with the reason reported, unless both
`AGENTKIT_LIVE_TESTS=1` and the provider credential are set. A skip never counts
as the offline conformance suite passing. Tests are time bounded (two minutes)
and cost bounded (a handful of short requests, with the judge's call and token
budgets capped), and they use only the in-memory result store, so nothing is
left behind.

| Variable                     | Purpose                                            |
| ---------------------------- | -------------------------------------------------- |
| `AGENTKIT_LIVE_TESTS`        | Must be `1` to opt in                              |
| `OPENAI_API_KEY`             | OpenAI credential used for the agent and the judge |
| `AGENTKIT_LIVE_OPENAI_MODEL` | Optional model id; defaults to `gpt-4o-mini`       |

## Run this project

From the repository root:

```bash
AGENTKIT_LIVE_TESTS=1 OPENAI_API_KEY=... \
  dotnet test --project tests/AgentKit.Evaluation.Live.Tests -c Release
```

## Related projects and documentation

- [Testing and evaluation](../../docs/architecture/testing-and-evaluation.md) —
  the live-verification rules.
- [Evaluation example](../../examples/Evaluation/README.md) — the offline
  composition this test extends with a judge.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
