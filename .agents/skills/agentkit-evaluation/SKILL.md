---
name: agentkit-evaluation
description:
  "Design, run, or debug AgentKit behavioral evaluations, datasets, evaluators,
  comparisons, and reports. Use for composed-agent quality and regression
  measurement; not for component contract conformance."
---

# AgentKit Evaluation

Read [AGENTS.md](../../../AGENTS.md). Evaluate composed behavior through public
AgentKit APIs. Do not use evaluation scores to waive deterministic correctness,
permission, ordering, persistence, or cancellation requirements.

1. Version each case's input, expected criteria, fixtures, tools, agent
   definition, model settings, evaluator, and dataset membership. Record the
   exact provider, resolved model, configuration version, usage, latency, run
   identity, and evaluator version with every result.
2. Prefer deterministic evaluators for schemas, state, tool effects, citations,
   safety rules, and exact outputs. Use model judges only for semantic criteria
   that code cannot assess honestly.
3. Give model judges a declared provider/model, bounded prompt, explicit rubric,
   blinded candidate order where relevant, repeated trials, and uncertainty.
   Judge failures and refusals are evaluation outcomes, not zero-quality
   answers.
4. Run through `AgentEngine`, public event streams, session reads, and approved
   diagnostics. `AgentKit.Evaluation` receives no friend access to mutable
   runtime internals.
5. Make external effects deterministic or isolate them behind recorded fixtures.
   Live providers and tools are opt-in, credential-aware, time and cost bounded,
   and separated from the required offline suite.
6. Compare compatible cohorts. A provider, model, prompt, toolset, policy,
   dataset, or evaluator change must remain visible instead of being blended
   into one score.
7. Preserve case-level evidence and distinguish pass rate, score distribution,
   variance, error, policy halt, limit exhaustion, and infrastructure failure.
   Do not hide a broken subset behind an average.
8. Test the evaluation runner itself for deterministic evaluator behavior,
   dataset versioning, cancellation, partial reports, resume/idempotency,
   redaction, budget enforcement, and stable comparison output.

Use `agentkit-conformance-testing` when the question is whether two swappable
implementations honor the same public contract. Evaluation asks whether a fully
composed agent did the intended work.
