# AgentKit.Loop

Coordinate turns, context preparation, model attempts, tool calls, and terminal
outcomes.

Use the loop implementation with explicitly selected providers, context, tools,
session coordination, and output processing. Continuation policy is a separate
replaceable service.

## Use this project

Start with `AddAgentLoop`, `AddRunContinuationPolicy` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## History reconstruction from a compaction checkpoint

`DefaultAgentLoop` owns the history read in the reduced composition, so it is
also the component that consumes an activated compaction. When it loads a branch
it looks for the newest `CompactionSessionEntry` whose record is `Active`, and
the history it hands to the context assembler becomes:

1. one `RuntimeMessage` projecting the checkpoint summary, then
2. exactly the entries from the checkpoint's `Manifest.RetainedSuffixStart`
   onward, including the retained suffix that existed at activation and
   everything appended after the checkpoint entry.

Covered entries are neither replayed nor scanned for dangling tool calls; a call
left unsettled inside the covered range is not recovered, because the checkpoint
already stands in for that history. A dangling call inside the retained suffix
is still settled before the first turn.

The projection is synthetic operational evidence, never an instruction. Its
exact shape is documented on `CompactionCheckpointProjection`: a leading header
`TextPart`, the checkpoint's summary parts unchanged, provenance (compaction,
manifest, and entry identities plus covered and retained sequences) in the
message's `Extensions`, and a `MessageId` derived deterministically from the
checkpoint entry so every turn and every run over the same checkpoint sees one
stable identity. It is never appended to the session; the durable truth remains
the `CompactionSessionEntry` in its causal position. Provider translators treat
a `RuntimeMessage` as tagged runtime content, so the summary can never gain
system or developer precedence.

The `HistoryView` cursor still names the real branch tip (version and upper
sequence), so every append the loop performs is guarded by the actual branch
version and rebases exactly as before. When more than one active checkpoint
exists the newest wins; the first-party compactor covers a contiguous prefix, so
a newer record whose covered range does not include an older one is logged as a
warning (event 1092) and still used.

Trade-off: the session read contract pages forward only, so the loop still reads
the branch from its origin. It buffers entries as they arrive and, on
encountering an active checkpoint, discards every buffered entry before the
retained suffix. Peak retention is bounded by the covered range plus the
retained suffix rather than by the number of checkpoints, and the read cost of
the covered range remains until a session read can start at a caller-supplied
sequence. A checkpoint activated concurrently while a run is in progress applies
to the next run's load, not retroactively to the turn in flight.

The reconstruction emits log event 1091 with the run, compaction identity,
checkpoint sequence, covered-entry count, and retained-message count, and tags
the run activity with `agentkit.compaction.id`. Summary text never enters any
signal.

## Related projects

- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Output](../AgentKit.Output/README.md) — resolve output definitions
  and validate, repair, or deserialize terminal candidates.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Loop.Tests](../../tests/AgentKit.Loop.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/agent-runtime.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
