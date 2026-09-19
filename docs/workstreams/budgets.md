# WS11: Budgets completion

Goal: budgets resolve from named profiles and policy catalogs rather than raw
limit arrays on the definition, the loop produces a `BudgetExecutionCapability`
that consumers borrow for one invocation, unknown-cost behavior is enforced
before the effect, and output repair, compaction, and context allocation reserve
through the same authority.

Owning documents: [Budgets](../architecture/budgets.md),
[Budgets concept](../concepts/budgets.md).

## Progress

- [ ] WS11-C1 profile and policy catalog contracts and `AddBudgetProfile`
- [ ] WS11-C2 `BudgetScopeRequest` accepts a profile key
- [ ] WS11-C3 capability producer in the loop, `AgentDefinition.BudgetProfile`
- [ ] WS11-C4 unknown-cost enforcement
- [ ] WS11-C5 consumer dimensions and defaults
- [ ] WS11-C6 validator and concurrent aggregate conformance

## Verified current state

| Item                                            | State                                                                                       | Evidence                                                                                                                                                   |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BudgetProfileKey`, `BudgetProfileVersion`      | EXISTS-UNWIRED                                                                              | only `BudgetExecutionCapability.cs:33`                                                                                                                     |
| definition carries raw `BudgetLimits`           | CONFIRMED                                                                                   | `AgentDefinition.cs:222`; loop `DefaultAgentLoop.cs:439,456`                                                                                               |
| `IBudgetProfileCatalog`, `IBudgetPolicyCatalog` | MISSING                                                                                     | remark `BudgetScopeRequest.cs:17-23`                                                                                                                       |
| `BudgetUnknownCostBehavior`                     | EXISTS-UNWIRED                                                                              | captured into `AgentBudgetOptionsSnapshot.cs:54`, passed to `BudgetAuthority.cs:13`, zero reads; README admits it (`src/AgentKit.Budgets/README.md:26-29`) |
| `BudgetExecutionCapability`                     | EXISTS-UNWIRED                                                                              | zero producers or consumers                                                                                                                                |
| `IRunBudget`                                    | EXISTS-UNWIRED                                                                              | zero implementations                                                                                                                                       |
| `BudgetAuthority` ctor                          | `(IBudgetLedger, snapshot, ILoggerFactory?)`                                                | spec has 9 deps (`budgets.md:391-400`)                                                                                                                     |
| loop reservations                               | turns, model requests, post-usage, tool calls                                               | not output repair (`:1130`), compaction (`:2645`), context (`:760`)                                                                                        |
| DI                                              | `AddAgentBudgets`, `AddBudgetDimension`, `ReplaceBudgetDimension`, `ReplaceBudgetAuthority` | `ServiceExtensions.cs:14,69,75,95`                                                                                                                         |
| store adapters                                  | InMemory, Sqlite, Json exist; conformance runs across all three                             | `tests/AgentKit.Conformance/BudgetLedgerConformanceTests.cs:8` inherited by the three ledger test classes                                                  |

`BudgetScopeRequest` has 26 construction sites; changes must be additive.

## Hidden prerequisites

1. `AgentDefinition.Components.BudgetProfile` (WS18); C3 adds an interim
   nullable `BudgetProfile` on the definition.
2. Pre-effect cost estimate source (`ModelDescriptor.Pricing` times a token
   estimate) has no specified formula.
3. `BudgetReservationRequest` has only `decimal Amount`; an "unknown estimate"
   signal is needed (additive flag or reuse of `UsageMeasurementQuality`).

## Spec coverage

| Contract                                                                                                              | Spec                                                            |
| --------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| `BudgetProfileSnapshot`, `IBudgetProfileCatalog`, `IBudgetPolicyCatalog`, `IBudgetPolicy`, `BudgetAuthority` deps, DI | `budgets.md` component and DI sections; `:391-400` for the ctor |
| `BudgetPolicyRequest`, `BudgetPolicyDecision` bodies                                                                  | NO-SPEC                                                         |
| unknown-cost estimate formula                                                                                         | NO-SPEC                                                         |

## Chunks

### WS11-C1: Profile and policy catalog contracts, `AddBudgetProfile`

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: Abstractions `BudgetProfileSnapshot.cs`, `BudgetPolicyKey.cs`,
  `IBudgetProfileCatalog.cs`, `IBudgetPolicyCatalog.cs`, `IBudgetPolicy.cs`,
  `BudgetPolicyRequest.cs`, `BudgetPolicyDecision.cs`; Budgets
  `BudgetProfileOptions.cs`, `InMemoryBudgetProfileCatalog.cs`,
  `InMemoryBudgetPolicyCatalog.cs`, `BudgetRegistration.cs`, `AddBudgetProfile`,
  `ReplaceBudgetProfile`, `ReplaceBudgetProfileCatalog`,
  `ReplaceBudgetPolicyCatalog`, `AddBudgetPolicy`, `ReplaceBudgetPolicy`,
  `ReplaceBudgetLedger`, `ReplaceBudgetDimensionCatalog`; `BudgetAuthority`
  gains optional catalogs. Snapshots: Abstractions, Budgets.

### WS11-C2: `BudgetScopeRequest` accepts a profile key

- Depends on: C1. Risk: ADDITIVE (ctor overload; raw-limits ctor kept as an
  inline profile). Size: S.
- Deliverables: `BudgetProfileKey? Profile`; authority resolves via the catalog
  and fails typed `BudgetScopeCreationFailureKind.ProfileNotFound`; remove the
  remark. Snapshot: Abstractions.

### WS11-C3: Capability producer in the loop

- Depends on: C2. Risk: DENSE-MODIFY `DefaultAgentLoop.cs:439-471`,
  `RunBudget.cs`, `AgentDefinition.cs:222`. Size: M.
- Deliverables: `AgentDefinition.BudgetProfile : BudgetProfileKey?` (raw limits
  retained as inline profile); loop creates the scope via profile when set,
  builds
  `BudgetExecutionCapability(profileKey, version, identity, correlation, scope)`
  in `RunTracking`, offers it to `OutputProcessingRequest.Budget`, the
  compactor, and later the executor; `AgentRunRequest.BudgetProfile?`; test that
  after-run work cannot reuse the capability. Snapshots: Abstractions, Loop.

### WS11-C4: Unknown-cost enforcement

- Depends on: C1. Risk: DENSE-MODIFY `BudgetScope.ReserveAsync`,
  `BudgetAuthority`. Size: S.
- Deliverables: `Reject` blocks a cost reservation with an unknown estimate
  under a scope with a cost limit; `AllowOnlyWithoutCostLimit` permits only when
  no ancestor has a cost limit; additive unknown-estimate signal on
  `BudgetReservationRequest`; `RunBudget.AccountUsageAsync` marks unknown when
  `ModelUsage.EstimatedCost` is null; README updated. Snapshots: Abstractions,
  Budgets.

### WS11-C5: Consumer dimensions and defaults

- Depends on: C3 and WS8-C7, WS10-C4/C6, WS9-C2. Risk: ADDITIVE. Size: S.
- Deliverables: `BudgetDimensions.OutputRepairs`, `CompactionAttempts`,
  `CompactionSummaryTokens`; seeded in `BudgetDimensionCatalogDefaults`. Each
  consumer's reservation is delivered in its own workstream.

### WS11-C6: Validator and concurrent aggregate conformance

- Depends on: C2, C3. Size: S.
- Deliverables: when any definition has a budget profile or limits, require an
  unkeyed `IBudgetAuthority` and a resolvable profile at build time (replaces
  the runtime `AgentRunInvalidState` at `DefaultAgentLoop.cs:441-448`); extend
  `BudgetLedgerConformanceTests` with concurrent-children exactness (runs in all
  three fixtures automatically).

## Totals

S 4, M 2. Confidence high: all claims are grep-verified, and the store family is
already complete.
