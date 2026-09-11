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

## Tool discovery evidence

`ToolDiscoveryRequest` validates matching agent, session, active run, complete
identity, definition revision, and configuration before source discovery. It
retains ordered selections with unique exact toolset keys.
`ToolProviderSnapshot` retains one source publication and rejects foreign
descriptors or repeated exact tool identities. Both values compare complete
ordered evidence structurally.

`ToolCatalogSnapshot` now requires an explicit `SourceVersions` map, including
selected empty sources. Every descriptor source must be present. This is an
intentional constructor change: callers must supply real publication versions.
These immutable values own no live invokers. The canonical capture, acquisition,
and resolver runtime remains under construction.

## Tool outcome evidence

`ToolCallOutcome` requires an exact `ToolTerminalStatus`, effect certainty, and
retry advice alongside its portable kind. `ToOutcomeKind()` provides the closed
status mapping: unknown numeric statuses remain intact and map to failure.
Construction rejects a contradictory kind, undefined certainty, inapplicable
certainty, and failure text on success. Failure and cancellation can retain
partial or uncertain effects; success can truthfully report no target change.

This intentionally replaces the old three-argument constructor and removes
outcome property initializers. Callers must supply evidence at the boundary that
knows it. Retry advice is never authorization to repeat a recorded effect.
Invocation results still require executor-owned normalization, terminal
recording, and policy-bound projection before durable publication.

## Tool-result projection provenance

`ToolResultProjectionInfo` retains the captured projection-policy key/version,
ordered losses, and measured omitted byte and part counts. Positive omissions
require a content-loss marker; status coarsening alone cannot explain missing
content. Reconstructed values compare structurally, including loss order and
repetition.

This value is a prerequisite for durable tool-result messages. The projector
must measure content and enforce the captured policy. Integration into the
canonical `ToolResultPart`, complete tool resolution, and message codecs remain
under construction.

`IToolResultProjectionPolicyCatalog` resolves the exact retained revision into
`ToolResultProjectionPolicyResolved` or `ToolResultProjectionPolicyUnavailable`.
Both outcomes preserve the requested reference; cancellation propagates.
[AgentKit.Tools](../AgentKit.Tools/README.md#retained-projection-policies)
provides the immutable configuration catalog and replaceable registration.

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
