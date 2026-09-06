# Testing and evaluation

**Role:** Prove interchangeable components behave correctly and measure whether
complete agents still do useful work.

Contract tests and agent evaluations answer different questions. Conformance
proves that an implementation preserves a public boundary. Evaluation measures
the behavior of a composed agent against a versioned dataset and rubric. A good
score cannot excuse broken ordering, permissions, cancellation, or persistence.

## Repository test structure

Every source project has a matching xUnit v3 project under tests. The matching
project owns implementation-specific unit, protocol, options, registration, and
disposal tests. AgentKit.Test.Shared contains deterministic fakes and fixtures;
AgentKit.Conformance contains reusable behavioral suites; and
AgentKit.Compatibility.Tests snapshots every packable public API.

Provider packages that reuse OpenAICompatible run both the shared protocol
family suite and their concrete provider suite. The latter verifies capability
claims, credentials, defaults, errors, options, and service registration that
the wire-family package cannot know.

All time-dependent tests replace TimeProvider. IDs, randomness, transports,
queues, and scheduling gates are controllable. Unit and conformance tests never
require live credentials or the developer's real filesystem.

## Agent evaluation

AgentKit.Evaluation is an optional package that runs datasets through the public
AgentEngine surface. It versions cases, fixtures, agent definitions, model
settings, evaluators, and expected criteria, then records results with the exact
provider, model, configuration, usage, latency, and trace identity.

Evaluators are independent capabilities. Deterministic evaluators check schemas,
tool effects, exact state, and safety properties. Model-based judges may assess
semantic quality but must declare their provider, model, prompt, rubric, repeat
count, and uncertainty. Evaluation storage and report exporters remain
replaceable leaf integrations when they need external systems.

The evaluation runner receives no privileged access to internal state. It uses
public results, event streams, session reads, manifests, and approved diagnostic
artifacts. This keeps the same evaluation usable against first-party and custom
implementations.

## Live verification

Live provider tests and evaluations are opt-in, credential-aware, time bounded,
and cost bounded. Missing credentials skip them clearly without weakening the
offline protocol suite. Test resources use isolated principals and are removed
according to the provider's retention contract.

## Related specifications

- [Testing and evaluation](../concepts/testing-and-evaluation.md)
- [Project structure](project-structure.md)
- [Observability](14-observability.md)
