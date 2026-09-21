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

| #   | Workstream                                                            | Depends on | Chunks | Done   |
| --- | --------------------------------------------------------------------- | ---------- | ------ | ------ |
| 1   | [Run envelope and admission](run-envelope-and-admission.md)           | –          | 14     | C1–C14 |
| 2   | [Hook kernel](hook-kernel.md)                                         | –          | 13     | C1–C13 |
| 3   | [Permissions, approvals, audit](permissions-approvals-and-audit.md)   | 2          | 15     | C1–C6a |
| 4   | [Tool runtime](tool-runtime.md)                                       | 2, 3       | 18     | C1, C3 |
| 5   | [Host access](host-access.md)                                         | 3          | 18     | C1     |
| 6   | [MCP tool source](mcp-tool-source.md)                                 | 4, 5       | 13     | C1a    |
| 7   | [Provider runtime](provider-runtime.md)                               | 2          | 15     | C1     |
| 8   | [Structured output](structured-output.md)                             | 2, 7       | 12     | –      |
| 9   | [Context assembly](context-assembly.md)                               | 2          | 8      | C1–C3  |
| 10  | [Context compaction](context-compaction.md)                           | 7, 9       | 8      | –      |
| 11  | [Budgets](budgets.md)                                                 | 8, 10      | 6      | –      |
| 12  | [Durable execution](durable-execution.md)                             | 1, 3       | 14     | –      |
| 13  | [Goals and delegation](goals-and-delegation.md)                       | 1, 12      | 12     | –      |
| 14  | [Memory and retrieval](memory-and-retrieval.md)                       | 7, 9       | 15     | –      |
| 15  | [Artifacts](artifacts.md)                                             | 4, 12      | 9      | –      |
| 16  | [Identity ingress](identity-ingress.md)                               | 1          | 5      | C1–C5  |
| 17  | [Observability](observability.md)                                     | 1          | 9      | –      |
| 18  | [Definition and validation sweep](definition-and-validation-sweep.md) | 1–17       | 11     | –      |
| 19  | [Evaluation](evaluation.md)                                           | 1, 18      | 8      | –      |
| 20  | [Documentation reconciliation](documentation-reconciliation.md)       | 1–19       | 8      | –      |

Approximately 230 chunks in total. WS18-C1 to C3 (`AgentComponentSelection` and
`AgentOptionalCapabilitySelection` as additive records) should be pulled forward
and landed before WS2 so that later workstreams add keys to the final shape
rather than to interim flat properties.

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
  adapters that run one shared conformance suite. Already violated today:
  approval store (no SQLite), artifacts (InMemory only), durability (InMemory
  only, no suite), goals (no store). The relevant workstreams contain explicit
  chunks to close each.
- Every workstream that adds a selectable component adds its key to the agent
  definition and the matching `AgentCompositionValidator` check in the same
  chunk. Land WS18-C1–C3 first so the key goes on `Components` or
  `OptionalCapabilities` directly.
- Every workstream adds the matching `AgentKit.Simple` `With*` sugar. Missing
  today for already-shipped capabilities: tools, hooks, MCP, artifacts,
  observability, permissions policy.
- Breaking changes toward the documented shape are sanctioned when the interim
  type carries a "deliberately reduced stand-in" remark or the documentation
  itself calls the surface interim. Each break is listed in the commit message.
- A contract that has a `HookDispatchContext? hooks` parameter in the
  specification ships without that parameter until WS2-C2 lands, and the
  deviation is recorded in the owning architecture document. WS2 then adds the
  parameter in one sweep.
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
- `AgentComponentSelection` (`composition-and-configuration.md:148-157`) has no
  `Compaction` field, but WS10 expects `Components.Compaction`.
- `composition-and-configuration.md:604-605` requires engine-wide
  `IRandomizerFactory` and `IContentHasher`; no workstream or `AGENTS.md` rule
  owns them.
- Evaluation result stores: `testing-and-evaluation.md:366-369` names InMemory
  and SQLite only; the cross-cutting rule requires `.Json` too.
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
