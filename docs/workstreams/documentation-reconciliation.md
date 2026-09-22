# WS20: Documentation reconciliation

Goal: no document, README, skill, or source remark describes an interim,
reduced, or stand-in state once the preceding workstreams have landed; every new
package appears in the repository map and package catalog; every acceptance list
matches code.

Owning documents: all of `docs/`, `AGENTS.md`, package READMEs,
`.agents/skills/*/SKILL.md`.

## Progress

- [ ] WS20-C1 composition and runtime architecture prose
- [ ] WS20-C2 context, compaction, tools, identity, durability architecture
- [ ] WS20-C3 guides, getting started, index
- [ ] WS20-C4 use cases
- [ ] WS20-C5 package READMEs, examples, root README
- [ ] WS20-C6 package catalog and `AGENTS.md` map
- [ ] WS20-C7 skills
- [ ] WS20-C8 source-remark sweep

## Checklist of interim statements found today

| File                                            | Lines                                                  |
| ----------------------------------------------- | ------------------------------------------------------ |
| `architecture/agent-runtime.md`                 | 54, 60, 72, 83, 104, 286, 298, 301, 304, 305, 363, 376 |
| `architecture/composition-and-configuration.md` | 445, 448, 472 ("Current admission surface")            |
| `architecture/context-compaction.md`            | 540, 571, 586, 1118, 1359                              |
| `architecture/context.md`                       | 347, 351, 356, 361                                     |
| `architecture/tools.md`                         | 471, 500                                               |
| `architecture/input-and-output.md`              | 402                                                    |
| `architecture/identity.md`                      | 146                                                    |
| `architecture/durable-execution.md`             | 335                                                    |
| `concepts/agent-loop-state-machine.md`          | 195                                                    |
| `concepts/extensions-hooks-and-middleware.md`   | 44, 75 (verify; may be intentional)                    |
| `guides/permissions.md`                         | 192                                                    |
| `use-cases/web-support-assistant.md`            | 184                                                    |
| `packages/index.md`                             | 159 ("planned packages not yet present")               |
| `getting-started.md`                            | 21–23 (queue admission pending)                        |
| `src/AgentKit.Loop/README.md`                   | one "reduced"                                          |

Source remarks: `rg "stand-in|not yet" src --type cs` returns 79 hits in 41
files today; provider hits describing wire semantics are legitimate and need
triage, the rest (`LlmRequestContext.cs`, `IToolAuthorizer.cs`,
`IToolCatalog.cs`, `ToolAuthorizationDecision.cs`, `DefaultOutputProcessor.cs`,
`AgentOutputOptions.cs`, `BudgetUnknownCostBehavior.cs`,
`StructuralCompactionCutSelector.cs`, and others) disappear with their
workstreams.

## Chunks

### WS20-C1: Composition and runtime architecture prose

- Depends on: WS1, WS18. Size: M.
- Done when: `rg -i "reduced|remaining steps|Current admission surface"` over
  `composition-and-configuration.md`, `agent-runtime.md`, `input-and-output.md`
  is empty.

### WS20-C2: Context, compaction, tools, identity, durability architecture

- Depends on: WS4, WS9, WS10, WS12, WS16. Size: M.
- Done when: each listed statement is removed or reworded as accurate present
  tense.

### WS20-C3: Guides, getting started, index

- Depends on: WS1, WS4, WS18. Size: M.
- Deliverables: `getting-started.md` current-status section,
  `guides/composition.md`, `guides/storage.md` (11 interim-API hits),
  `guides/permissions.md`, `guides/file-system.md`, `docs/index.md`.

### WS20-C4: Use cases

- Depends on: all. Size: M.
- Deliverables: 13 files; `web-support-assistant.md` (12 hits),
  `testing-agents-offline.md` (7), `quickstart-example.md` (6),
  `coding-agent-example.md` (5), the rest one or two each.
- Done when: no `SendAsync`, `AgentSendRequest`, `LlmToolDefinition`, or
  `IConversationSession` unless still real.

### WS20-C5: Package READMEs, examples, root README

- Depends on: all. Size: M.
- Deliverables: `AgentKit.Conversations` (9 hits), `AgentKit.Simple` (8),
  `AgentKit` (4), `AgentKit.Tools` (3), `AgentKit.Loop`, `AgentKit.Budgets`,
  `examples/CodingAgent` (5), `examples/QuickStart`, root README; new READMEs
  for every added package.

### WS20-C6: Package catalog and `AGENTS.md` map

- Depends on: all. Size: M.
- Deliverables: `docs/packages/index.md:159`; `AGENTS.md` repository map gains
  `AgentKit.Durability`, `.Memory`, `.Goals.Hosting`, `.Context.Project`,
  `.Observability.OpenTelemetry`, `.Evaluation` and their adapters.

### WS20-C7: Skills

- Depends on: all. Size: M.
- Deliverables: every `.agents/skills/*/SKILL.md` touched by WS1–19; at minimum
  architecture, evaluation, agent-loop, tools, context, input-output.

### WS20-C8: Source-remark sweep

- Depends on: all, including WS21. Size: S.
- Deliverables: triage list of the remaining `stand-in`/`not yet` hits; remove
  or reword each that no longer describes reality. `Legacy*` types, `[Obsolete]`
  members, and compatibility branches are deleted by
  [WS21](obsolete-code-removal.md), not reworded here.

## Totals

S 1, M 7.
