# AgentKit.Context.Compaction

Select safe history cuts and produce, validate, and activate compaction
checkpoints.

Use compaction when older context needs a bounded representation. Two
first-party strategies ship: a deterministic extractive strategy that needs no
model, and a model-backed strategy that asks a selected conversational model for
a summary under a configurable prompt.

## Use this project

Start with `AddContextCompaction` or `AddModelBackedContextCompaction` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

```csharp
// Deterministic, extractive checkpoints; no provider dependency.
services.AddContextCompaction();

// Model-generated summaries. The summary model is an external fact the
// application names; the prompt defaults to the embedded resource
// Resources/DefaultCompactionSummaryPrompt.txt and can be overridden.
services.AddModelBackedContextCompaction(o =>
{
    o.SummaryModelPolicy = new ModelSelectionPolicy([new ModelAlias("summarizer")]);
    o.SummaryPrompt = "Summarize the transcript faithfully, in plain text, ...";
    o.MaximumSummaryInputCharacters = 120_000; // transcript ceiling (default)
    o.MaximumCheckpointCharacters = 16_000;    // summary ceiling (default)
});
```

`AddModelBackedContextCompaction` registers the same pipeline as
`AddContextCompaction`, adds `ModelCompactionStrategy` and its summary generator
under the default compactor key, and makes the model strategy that compactor's
default. It resolves the summary model through the engine's `IModelCatalog`,
`IModelSelector`, and `ILlmModelResolver`, exactly as the agent loop resolves a
run's model, so the application must also register `AgentKit.Providers` and a
branded provider package that supplies the selected alias. `SummaryPrompt` is
accepted by both entry points and validated as non-blank at composition;
`SummaryModelPolicy` is required only by the model-backed variant.

## Keyed compactors, profiles, and replacement

`AddAgentContextCompaction(key)` registers a compactor under an explicit key.
Strategies, summary generators, and event sinks are additive per compactor key
(`AddCompactionStrategy<T>`, `AddCompactionSummaryGenerator<T>`,
`AddCompactionEventSink<T>`); every other collaborator is singular per key and
has one `Replace*` method. A profile selects a compactor and orders its
strategies, and an agent definition opts in by naming the profile in its
`AgentOptionalCapabilitySelection.CompactionProfile`:

```csharp
public sealed class MyStrategy : ICompactionStrategy
{
    public Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

var key = new ComponentKey<ICompactor>("archive");
services.AddAgentContextCompaction(key, o => o.MinimumRetainedEntries = 4);
services.AddCompactionStrategy<MyStrategy>(key, new CompactionStrategyRegistration(
    myDescriptor, order: 0, before: [], after: [], ServiceLifetime.Singleton));
services.AddCompactionProfile(new CompactionProfileKey("archive"), key, p =>
    p.StrategyOrder = [myDescriptor.Key, CompactionStrategyKeys.Extractive]);
```

Duplicate identities fail with `InvalidOperationException` unless the exact
`Replace*` method is used; an equivalent repeat is a no-op. Strategy, generator,
and sink lifetimes are singleton or transient. Engine composition checks that a
selected profile exists and, when enabled, that its keyed compactor is
registered; the compactor falls back to the next strategy in the profile's order
only when a strategy declines with a typed unsupported result.

## Behavioral guarantees

- `SourceThrough` is an eligibility bound: the compactor reads the branch to its
  tip, hands collaborators only entries at or below the bound, and allocates the
  activated entry's sequence after the real tip.
- Source pages are pinned to one `SessionReadSnapshot`; a request whose
  `SourceVersion` differs from the observed version returns `CompactionConflict`
  before any strategy runs.
- `CompactionId` is reconciled by identity: a replayed request, a racing
  duplicate, or a lost append response resolves to the committed record instead
  of a reused-idempotency-key failure.
- Cancellation returns `CompactionCancelled` with a truthful
  `CompactionCommitState`; a committed record is never rolled back.
- The validator re-derives covered identities, range, retained suffix start, and
  manifest claims from the source before activation.
- The structural cut selector prefers a boundary whose retained suffix begins at
  a user turn and never splits a recorded causal pairing.
- `Deadline` is enforced against the injected `TimeProvider` before any session
  I/O; `TargetInputTokens` is advisory.
- `CompactionFailure.Retryable` is `true` only for a transient source read, a
  drifted continuation snapshot, or a throttled/unavailable/timed-out summary
  model attempt.
- `ModelCompactionStrategy` sends exactly one non-streaming request per attempt:
  the prompt as a `SystemMessage`, the covered transcript as one `UserMessage`,
  no tools, and the request's `Deadline`. System and developer messages found in
  covered history are omitted from the transcript. A length-limited stop, a tool
  request, or an empty response is a non-retryable `StrategyFailure`; a summary
  longer than `MaximumCheckpointCharacters` is bounded with a truncation marker
  and the truncation is recorded in the producer provenance.
- The model's summary is untrusted output. It is stored as a plain `TextPart`
  and never given system or developer precedence; the model, provider, request
  and response identities, usage, and truncation flags are recorded on
  `CompactionProducer.Extensions` under `ModelCompactionProvenanceKeys`.
- No prompt, transcript, or summary text enters logs, activity tags, or
  provenance.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Durable activation

When a `CompactionRequest` carries a `DurabilityProfile` whose profile enables
`agentkit.compaction.activation`, `SessionCompactionActivationCoordinator` runs
the session append inside a recoverable operation. The `CompactionActivated`
checkpoint is committed before the append, so after a crash the record may or
may not be in the session, which is exactly the question the existing
reconciliation read answers; a checkpoint written only on success would make a
crash mid-append indistinguishable from an attempt that never started. A refused
or fenced checkpoint stops the activation before the append, and a journal fault
propagates and is recorded by the compactor as a failed attempt. The manifest
holds identities and counts, never summary text.
`CompactionActivationDurableOperationHandler` owns the operation name, and
`AddAgentContextCompaction` registers it idempotently. Explicit maintenance
compaction has no in-run address and is not journaled.

## Related projects

- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Budgets](../AgentKit.Budgets/README.md) — reserve and account for
  capacity across hierarchical budget scopes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Context.Compaction.Tests](../../tests/AgentKit.Context.Compaction.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/context-compaction.md) —
  intended ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
