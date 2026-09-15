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
`AddContextCompaction` and replaces its single `ICompactionStrategy` with
`ModelCompactionStrategy`. It resolves the summary model through the engine's
`IModelCatalog`, `IModelSelector`, and `ILlmModelResolver`, exactly as the agent
loop resolves a run's model, so the application must also register
`AgentKit.Providers` and a branded provider package that supplies the selected
alias. `SummaryPrompt` is accepted by both entry points and validated as
non-blank at composition; `SummaryModelPolicy` is required only by the
model-backed variant.

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
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
