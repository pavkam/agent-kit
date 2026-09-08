# AgentKit.IO

Provide input-promotion policy and a broker for bounded human questions.

Use these services when coordinating queued input or asking a human for
information. Complete admission, channel fan-out, and final publication are part
of the wider IO architecture still under implementation.

## Use this project

Start with `AddInputPromotionPolicy`, `AddHumanQuestionBroker` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools.Question](../AgentKit.Tools.Question/README.md) — ask a human
  a bounded question through the configured question broker.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.IO.Tests](../../tests/AgentKit.IO.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/input-and-output.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
