# Workstreams

This directory is the execution plan for taking AgentKit from its current
partially wired state to the design in
[`docs/architecture`](../architecture/index.md) and the normative
[concept specifications](../concepts/index.md). Those documents remain the
design authority. Each workstream file here is a source-verified work breakdown:
what exists today (with `file:line` evidence), what is missing, which
prerequisites are hidden behind each step, and an ordered list of commit-sized
chunks.

Every chunk was sized so that one focused session lands it as one green commit
(`make format lint build test` passing). Chunk IDs (`WS4-C3`) are stable and are
referenced from commit messages. Progress is tracked by the checkbox list at the
top of each workstream file; there is no separate ledger.

## Order and status

Dependencies are on other workstreams as a whole unless a chunk names a specific
chunk. Sizes count chunks after splitting: S under one hour, M one to three
hours, L three to six hours.

| #   | Workstream                                                            | Depends on  | Chunk IDs | Items | Done            |
| --- | --------------------------------------------------------------------- | ----------- | --------- | ----- | --------------- |
| 1   | [Run envelope and admission](run-envelope-and-admission.md)           | –           | 14        | 14    | C1–C14 (14/14)  |
| 2   | [Hook kernel](hook-kernel.md)                                         | –           | 13        | 14    | C1–C13 (14/14)  |
| 3   | [Permissions, approvals, audit](permissions-approvals-and-audit.md)   | 2           | 15        | 20    | C1–C15 (20/20)  |
| 4   | [Tool runtime](tool-runtime.md)                                       | 2, 3        | 14        | 20    | C1–C14 (20/20)  |
| 5   | [Host access](host-access.md)                                         | 3           | 18        | 21    | C1–C18 (21/21)  |
| 6   | [MCP tool source](mcp-tool-source.md)                                 | 4, 5        | 13        | 15    | C1a–C13 (15/15) |
| 7   | [Provider runtime](provider-runtime.md)                               | 2, 5        | 11        | 15    | C1–C11 (15/15)  |
| 8   | [Structured output](structured-output.md)                             | 2, 7        | 8         | 12    | C1–C8 (12/12)   |
| 9   | [Context assembly](context-assembly.md)                               | 2           | 8         | 8     | C1–C8 (8/8)     |
| 10  | [Context compaction](context-compaction.md)                           | 7, 9        | 8         | 8     | C1–C8 (8/8)     |
| 11  | [Budgets](budgets.md)                                                 | 8, 10       | 6         | 6     | C1–C6 (6/6)     |
| 12  | [Durable execution](durable-execution.md)                             | 1, 3        | 13        | 14    | C1–C13 (14/14)  |
| 13  | [Goals and delegation](goals-and-delegation.md)                       | 1, 12       | 12        | 12    | C1–C12 (12/12)  |
| 14  | [Memory and retrieval](memory-and-retrieval.md)                       | 7, 9        | 13        | 15    | C1–C13 (15/15)  |
| 15  | [Artifacts](artifacts.md)                                             | 4, 12       | 9         | 9     | C1–C9 (9/9)     |
| 16  | [Identity ingress](identity-ingress.md)                               | 1           | 5         | 5     | C1–C5 (5/5)     |
| 17  | [Observability](observability.md)                                     | 1           | 8         | 9     | C1–C8 (9/9)     |
| 18  | [Definition and validation sweep](definition-and-validation-sweep.md) | 1–17        | 11        | 11    | C1–C11 (11/11)  |
| 19  | [Evaluation](evaluation.md)                                           | 1, 18       | 8         | 8     | C1–C8 (8/8)     |
| 20  | [Documentation reconciliation](documentation-reconciliation.md)       | 1–19, 21    | 8         | 8     | C1–C8 (8/8)     |
| 21  | [Obsolete and compatibility code removal](obsolete-code-removal.md)   | 3, 4, 5, 18 | 8         | 10    | C1–C8 (10/10)   |

A **chunk ID** (`WS4-C3`) is a stable commit-message reference; an **item** is
one line of a workstream's Progress list. They differ where a chunk was split
during execution into lettered sub-chunks that landed separately (`C4a`/`C4b`),
or where one line records a set (`C10a/b/c`, `C9b/c`). The Done column gives the
range of chunk IDs landed and the item count. Totals: 222 chunk IDs and 253
Progress items, plus six WS1 prerequisite commits that predate its chunk list.
Earlier revisions of this table counted splits inconsistently (WS7 listed 15
chunks and C1–C15 for 10 IDs; WS8, WS14, WS17, and WS21 listed items in the
Chunks column but IDs in Done); the columns now mean one thing each.

WS21 ran before WS20 so that the documentation sweep reconciles prose against
code with no legacy paths left. WS18-C1 to C3 (`AgentComponentSelection` and
`AgentOptionalCapabilitySelection` as additive records) were pulled forward and
landed before WS2 so that later workstreams added keys to the final shape rather
than to interim flat properties.

## Conventions used in every workstream file

**Verified state markers.** `EXISTS-AND-USED` (production callers listed),
`EXISTS-UNWIRED` (zero production callers), `EXISTS-AS-REDUCED-STAND-IN` (the
source remark is quoted), `MISSING`.

**Risk classes.** `ADDITIVE` adds files or members and changes no existing test.
`CONTRACT-BREAK` changes a public signature; the file lists every test project
and fake that breaks. `DENSE-MODIFY` edits existing complex control flow; the
file names the method and approximate line count. Dense-modify chunks are never
combined with another chunk in one session.

**Spec coverage.** Every public contract to be added is either `SPEC` (a C#
block exists in `docs/architecture` or `docs/concepts` at the cited line) or
`NO-SPEC` (named in prose only, or not at all). A `NO-SPEC` contract needs a
short C# block added to the owning architecture document before the chunk that
introduces it; that documentation edit is part of the chunk.

**Done-when.** A verifiable statement, normally a named test or a grep that
returns zero matches.

## Cross-cutting rules

- Every persistent store family ships `.InMemory`, `.Sqlite`, and `.Json`
  adapters that run one shared conformance suite. This held at WS20 for
  sessions, permissions, budgets, artifacts (plus a `.FileSystem` leaf),
  durability, goals, memory, and evaluation; the sweep verified each family's
  three leaves exist.
- Every workstream that adds a selectable component adds its key to the agent
  definition and the matching `AgentCompositionValidator` check in the same
  chunk. Land WS18-C1–C3 first so the key goes on `Components` or
  `OptionalCapabilities` directly.
- Every workstream adds the matching `AgentKit.Simple` `With*` sugar. The sugar
  shipped for tools (`WithTools`), MCP, artifacts, memory, durability,
  compaction, budgets, delegation, host access, output, and permissions policy.
  Hooks and observability have none: they are configured on `builder.Services`
  through their own registrations.
- AgentKit has no consumers, so there is no backwards compatibility to keep.
  Breaking changes toward the documented shape are always sanctioned. A chunk
  that supersedes a surface replaces it in place and updates every caller; it
  never adds `[Obsolete]`, a `Legacy*` rename, a forwarder, an adapter over the
  old shape, or a nullable fallback for old callers. Persisted formats change
  without migrations or readers for earlier layouts. Each break is listed in the
  commit message.
- A contract that has a `HookDispatchContext? hooks` parameter in the
  specification shipped without that parameter until WS2-C2, which added it in
  one sweep; any remaining deviation is recorded in the owning architecture
  document.
- Public API snapshots
  (`bash tests/AgentKit.Compatibility.Tests/update-snapshots.sh`) are
  regenerated in every chunk that touches a public surface. New packable
  projects need a snapshot file and an `AgentKit.slnx` entry.
- `AgentKit.Providers`, `.Output`, `.Context`, `.Context.Compaction`,
  `.Budgets`, `.Loop`, `.Hooks`, `.Tools`, `.Permissions`, `.Goals`,
  `.Durability`, `.Memory`, `.Artifacts`, and the `AgentKit` facade may
  reference only `AgentKit.Abstractions` and `AgentKit.Observability`
  (`tests/AgentKit.Architecture.Tests/ProjectReferenceGraph.cs:16-22,153,163`).
  Anything a runtime package needs from another runtime package must be an
  Abstractions contract.

## Known specification conflicts to resolve

These were found while verifying and should be settled in the owning
architecture document before the chunk that hits them:

- ~~`HookDispatchContext` is defined per dispatch (`extensions.md:102-105`) but
  passed once per run in `agent-runtime.md:181`,
  `composition-and-configuration.md:491`, `context.md:238`, and
  `permissions-and-human-control.md:294`. WS2-C2 proposes a run-scoped
  `HookActivationScope` that creates per-dispatch contexts.~~ Resolved in WS2:
  the loop holds `HookActivationScope` per run and mints per-dispatch contexts;
  see `extensions.md` implemented points and the reconciliation note in
  `agent-runtime.md`.
- ~~`AgentComponentSelection` (`composition-and-configuration.md:148-157`) has
  no `Compaction` field, but WS10 expects `Components.Compaction`.~~ Resolved in
  WS18: compaction is optional and is selected by
  `AgentOptionalCapabilitySelection.CompactionProfile`.
- ~~`composition-and-configuration.md:604-605` requires engine-wide
  `IRandomizerFactory` and `IContentHasher`; no workstream or `AGENTS.md` rule
  owns them.~~ Resolved in WS18-C7: owned by `AgentKit.Abstractions`, installed
  by `AddAgentKit`, replaceable, and required by composition validation;
  `AGENTS.md` now lists both.
- ~~Evaluation result stores: `testing-and-evaluation.md:366-369` names InMemory
  and SQLite only; the cross-cutting rule requires `.Json` too.~~ Resolved in
  WS19: the cross-cutting rule wins; `AgentKit.Evaluation.InMemory`, `.Sqlite`,
  and `.Json` run one result-store conformance suite, and the architecture and
  concept documents name all three.
- ~~Loop-level `AgentKit.AgentRunRequest` (Abstractions) collides with the
  facade `AgentRunRequest` in `composition-and-configuration.md:277-284`; one
  must be renamed before WS1-C8.~~ Fixed in WS1-C8: the loop-level type is
  renamed to `AgentLoopRunRequest`; `AgentRunRequest` now names only the new
  facade record.
- ~~`RunLimitFailure` requires a `BudgetLimitFailure`, but a turn limit has no
  budget scope; WS1-C7 needs either a synthetic builder or a second
  constructor.~~ Resolved in WS1-C7 without widening `RunLimitFailure`: a turn
  limit is the loop's own configured policy, not a budget-authority decision, so
  it maps to `RunPolicyHalted` instead; a provider length ceiling maps to
  `RunFailed`. `RunLimitReached`/`RunLimitFailure` stay budget-authority-scoped
  only. See WS1-C7's Landed note in `run-envelope-and-admission.md` for the full
  rationale.
- ~~`ContextAssemblyRequest` and `ModelRequestContext` in `context.md` described
  shapes the code never had (`Agent`, `Hooks`, `Budget` fields and a
  `ModelRequestContext` result), while the implementation carried an
  evidence-bearing request and an `LlmRequestContext` result.~~ Resolved in
  WS20: `context.md` documents the shipped request (one constructor over atomic
  `ContextAssemblyEvidence`, no evidence-less form, `Output` init property) and
  `LlmRequestContext` as the provider-facing result; the assembler keeps hooks
  and budget in its keyed services rather than on the request.
- ~~`ComponentRegistrationSnapshot.RepresentsCompleteRunnableGraph` stays false
  for first-party compositions (WS18); should first-party registrations publish
  declarations?~~ Resolved in WS20: no. Declarations cannot observe a later
  `Replace`/`RemoveAll`, 14 of the 30 required addresses are opaque factories
  with package-internal graphs, and the storage-owned addresses are registered
  by leaves the runtime cannot name. `composition-and-configuration.md` records
  the reasoning and the contract change that would be needed.

No specification conflict is open. WS20 found three implementation gaps while
reconciling prose with code. None is a conflict, because the owning documents
now state the shipped surface; all three have since closed:

- ~~Tools: `IToolExecutionPolicy`, `IToolExecutionPolicySelector`,
  `IToolCallRecorder`, and `IToolEventSink` are specified and were never
  built.~~ Resolved by
  [WS4-C14](tool-runtime.md#ws4-c14-recorder-execution-policy-and-event-sink):
  every contract and registration helper ships, accepted calls are recorded in
  the session before invocation, and `tools.md` records the deviations.
- ~~Compaction: the keyed per-collaborator `Add*`/`Replace*` helpers and a
  registration that consumes `CompactionProfileOptions` are specified and were
  never built.~~ Resolved by
  [WS10-C9](context-compaction.md#ws10-c9-keyed-collaborator-registration-and-profile-consumption):
  every helper ships, profiles compile to a policy the engine validates and the
  loop attaches, and `context-compaction.md` states the shipped surface.
- ~~Providers: first-party adapters send through an injected `HttpClient`
  instead of `INetworkTransport` and a provider-egress grant, which
  `model-and-embedding-providers.md` specifies for every adapter.~~ Resolved by
  [WS7-C11](provider-runtime.md#ws7-c11-provider-egress-through-inetworktransport):
  every first-party adapter sends through `ProviderEgress` under a per-attempt
  provider-egress grant plus resolution and send grants, and
  `model-and-embedding-providers.md` records the deviations. The credential-read
  grant and lease remain unshipped there.
