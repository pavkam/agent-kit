---
name: agentkit-evaluation
description:
  "Design, run, or debug AgentKit behavioral evaluations, datasets, evaluators,
  comparisons, and reports. Use for composed-agent quality and regression
  measurement; not component contract conformance."
---

# AgentKit Evaluation

Follow
[Testing and evaluation](../../../docs/architecture/testing-and-evaluation.md)
and its
[normative evaluation contract](../../../docs/concepts/testing-and-evaluation.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Decision guide

1. Evaluate through the public `AgentEngine` surface. The evaluation package may
   consume public results, events, session reads, manifests, and approved
   artifact references, but receives no privileged mutable runtime access.
2. Version cases, fixtures, dataset membership, agent definitions, model and
   tool settings, evaluators, rubrics, and expected criteria.
3. Record the resolved provider and model, configuration and policy versions,
   usage, latency, run identity, evaluator version, and dataset version with
   each result.
4. Prefer deterministic evaluators for schemas, exact state, tool effects,
   citations, and safety. Use model judges only for semantic criteria code
   cannot assess honestly.
5. Give model judges an explicit provider, model, prompt, rubric, repeat count,
   uncertainty treatment, and blinded candidate order where relevant.
6. Isolate or record external effects. Live providers and tools are opt-in,
   credential-aware, and bounded by time, cost, and run budgets.
7. Compare compatible cohorts. Provider, prompt, policy, toolset, dataset, or
   evaluator changes remain visible instead of being blended into one score.
8. Preserve case-level evidence and distinguish quality, refusal, policy halt,
   limit exhaustion, cancellation, evaluator failure, and infrastructure error.
9. Test the runner for versioning, cancellation, partial reports, resume and
   idempotency, redaction, budget enforcement, and stable comparison output.

Use `agentkit-conformance-testing` for interchangeable implementation contracts.
An evaluation score never waives deterministic correctness, security,
persistence, ordering, or cancellation requirements.
