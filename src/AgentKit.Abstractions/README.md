# AgentKit.Abstractions

Implement AgentKit extensions against provider-neutral contracts and typed
domain values.

Reference this package when supplying an alternative implementation or
integration. It defines contracts shared by engines and components without
depending on their implementations.

## Use this project

Start with the contracts for the component you are implementing. The
[foundation specification](../../docs/architecture/foundation-contracts.md) maps
the shared values and extension boundaries. Component implementations provide
their own DI registration methods.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Run usage values

`RunUsage` captures current, revisioned usage for one run. Each charged attempt
has a distinct `UsageEntryId`; `Apply` replaces its prior measurements and
treats an equivalent current revision as a no-op. Earlier snapshots stay
unchanged. `GetAggregate` takes an explicit dimension descriptor and unit, uses
exact quantities, and preserves unknown values and measurement provenance.
Estimated cost requires a pricing source and version; currencies remain
separate.

An omitted dimension contributes unknown unless explicitly not applicable.
`KnownAmount` is only the known portion; a null `Amount` means no complete
aggregate is available. Producers must bound and persist the evidence. These
values do not provide a ledger, budget enforcement, or final-result publication.

## Final results and deferral

`AgentRunResult<TOutput>` separates rejection before admission from
`AgentRunFinished<TOutput>` for accepted work. Finished results preserve
semantic outcome and settlement separately, validate history/usage/handoff
correlation, and report clean success only when both semantics and settlement
succeeded. `IAgentRunStream<TOutput>` exposes incremental events and a
repeatedly awaitable completion task. Disposing a subscription does not abort
the run.

Deferred requests retain explicit continuation ownership, effect-start evidence,
normalized operations, input fingerprints and audited decision references.
Provider suspension keeps the runtime operation open; only external handoffs can
enter a terminal `RunDeferred` outcome. These values and `IOutputPublisher`
define contracts; complete publisher, resolution and settlement implementations
remain under construction.

## Related projects

- [AgentKit](../AgentKit/README.md) — compose a process-level engine that hosts
  immutable agent definitions and isolated runs.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.

This project has no source-project dependencies.

## Tests and reference

- [AgentKit.Abstractions.Tests](../../tests/AgentKit.Abstractions.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/foundation-contracts.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
