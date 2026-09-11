# AgentKit.IO

Provide input-promotion policy, a broker for bounded human questions, and the
internal bounded run-event hub.

Use these services when coordinating queued input or asking a human for
information. Complete admission, publisher integration, channel adapters, and
final publication are part of the wider IO architecture still under
implementation.

## Use this project

Start with `AddInputPromotionPolicy`, `AddHumanQuestionBroker` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Run-event fan-out

`RunEventHub` is an internal mechanism for one accepted run. It captures
recipients and checks strictly increasing event sequences under the same lock.
Registration starts buffering immediately; enumeration starts delivery later.
The validated default limits are 32 registered subscriptions and 256 queued
events per subscription. The publisher must separately enforce payload-byte
bounds before handing events to the hub.

A full queue disconnects that subscriber with a typed failure identifying the
first unavailable sequence. Healthy subscribers continue, and the producer does
not wait for a slow UI. Cancellation, abandoned enumeration and disposal release
the subscriber's buffer and registration without cancelling the run. Normal
completion allows consumers to drain queued events; premature hub disposal
produces an explicit delivery failure.

The hub accepts immutable, already sequenced events. It does not allocate
durable sequence ranges, persist events, capture a replay snapshot, authorize
subscribers, deliver required sinks, or settle runs. Those publisher and session
dependencies remain open. The implementation is not registered as a public
publisher until that complete contract can be composed.

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
