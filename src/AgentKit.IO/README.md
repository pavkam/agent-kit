# AgentKit.IO

Provide input-promotion policy, a broker for bounded human questions, the
internal bounded run-event hub, and the run-scoped output publisher over it.

Use these services when coordinating queued input, asking a human for
information, or exposing one run's live events and final envelope. Complete
admission, durable publication, channel adapters, and loop integration are part
of the wider IO architecture still under implementation.

## Use this project

Start with `AddInputCoordinator`, `AddInputPromotionPolicy`, and
`AddHumanQuestionBroker` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

## Input admission

`DefaultInputCoordinator` is the first-party `IInputCoordinator`. It bounds the
payload's part count, allocates the admission identity, captures the canonical
preprocessing manifest and the injected-clock timestamp, and then reports
exactly what the selected `IInputQueue` committed. `AddInputCoordinator`
registers it together with this package's replaceable admission-identity
generator and clock, and binds `InputCoordinatorOptions` through the standard
options pattern with an optional configure delegate; the application must select
the queue, because AgentKit.IO is not a durable store.

```csharp
services.AddInputCoordinator(o => o.MaximumInputParts = 128);
```

The bound options are validated at host start and when first materialized, and
the coordinator captures them once at construction. A host may also call
`services.Configure<InputCoordinatorOptions>(...)` before or after
`AddInputCoordinator`; configure callbacks compose in registration order.

The coordinator applies no preprocessors, so the effective payload is the
original payload and both manifest fingerprints are the same canonical
`InputPayloadFingerprint`. Equal payloads therefore replay against the same
durable admission. It decides no authorization: the request's captured
authorization evidence is carried to the queue's store, which enforces it.
Cancellation, an oversized payload, and a default generated identity all stop
before the queue is touched, and a queue failure propagates rather than becoming
a substituted outcome.

The coordinator bounds part count only. Payload byte bounds, configured
preprocessors, and the session-backed `IInputQueue` itself — in particular the
durable atomic promotion primitive its `PromoteAsync` needs — remain open.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

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
dependencies remain open.

The hub's typed subscription adapter implements `IAgentRunStream<TOutput>`.
Event cancellation, overflow, abandonment, and disposal leave its producer-owned
final-result task independent. The adapter validates the result's exact run
correlation and exposes the same envelope on repeated awaits.

## Run output publication

`AgentRunOutputPublisher` is the first-party `IOutputPublisher` for one accepted
run. It is constructed by the run's owner with that run's immutable correlation,
an injected clock, and `RunOutputPublisherOptions` live bounds. It is not
registered in DI, because a run-scoped publisher requires the keyed run
activation that remains open.

`PublishAsync` rejects a foreign correlation and a sequence that does not
advance strictly beyond the last accepted event, and refuses events once
publication has ended. `CompleteAsync` validates the envelope's run and output
type, exposes exactly one final envelope, treats an equivalent repeat as
idempotent, and rejects a conflicting envelope while retaining the original.
Exposure seals the event stream first, so a consumer that observed the result
can still drain its accepted prefix. `Subscribe<TOutput>` binds the run's single
validated output type and returns a stream whose final-result wait is
independent of event delivery. Disposal before exposure cancels pending waits
instead of fabricating an outcome.

This publisher owns live process-local publication only. It reserves no durable
sequence range, records no publication intent, delivers to no required external
sink, bounds no event payload bytes, and decides no semantic outcome. Those
durable publication responsibilities remain open.

## `DefaultOutputPublisher` and `AddAgentIO`

`DefaultOutputPublisher` is a second, DI-registered `IOutputPublisher`: unlike
`AgentRunOutputPublisher` above, `AddAgentIO` registers it as a keyed, scoped
service resolved inside the run's own scope, built from a `RunScopeIdentity` the
facade populates once the session address, conversation, and run identity are
known. It fans every published `RunEvent` out to the run's `RunEventHub` and to
every registered `IRunEventSink`, in `RunEventSinkRegistration.Order`. A
`RunEventDelivery.Required` sink's `PublishAsync` failure propagates out of
`PublishAsync` itself, so a caller can tell a required sink actually failed
rather than silently losing an event; a `RunEventDelivery.BestEffort` sink's
failure and any backpressure decision short of `Wait` (see
`IOutputBackpressurePolicy`, whose first-party `DefaultOutputBackpressurePolicy`
always returns `Wait` for a required sink and waits-then-drops a best-effort one
past `AgentIOOptions.MaximumBestEffortSinkWait`) are isolated and logged
instead.

```csharp
services.AddAgentIO(inputKey, outputKey, o => o.MaximumBestEffortSinkWait = TimeSpan.FromSeconds(5));
services.AddRunEventSink<MySink>(new RunEventSinkRegistration("my-sink", RunEventDelivery.BestEffort, order: 0));
```

`AddAgentIO` also binds `InputCoordinatorOptions` with this package's defaults,
so it is self-sufficient without a separate `AddInputCoordinator` call, and
validates that a definition's explicit
`InputCoordinatorKey`/`OutputPublisherKey` resolve to a real registration.
`AgentDefinition` leaves both keys unset by default, resolving to
`AgentIOComponentDefaults`. `AgentKit.Loop`'s `DefaultAgentLoop` publishes
through `AgentRunServices.Publisher` when one is composed: a `ContentDeltaEvent`
for each streamed model fragment, and a `MessageCommittedEvent` after each
durable message commit (assistant and tool result); it does not yet publish for
the final settled outcome, and `AgentRunServicesFactory.Compile` resolves both
`IInputCoordinator` and `IOutputPublisher` unkeyed rather than routing through
those per-definition keys — a known interim gap.

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
