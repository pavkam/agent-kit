# WS3: Permissions algebra, approvals, audit

Goal: the security authority evaluates policies through a versioned catalog and
selector, intersects allow constraints deterministically, records request and
decision audit, issues grants through a dedicated issuer and decision store,
supports durable approval deferral and resolution, exposes revocation with typed
results, and every effecting boundary reaches the authority through
`ISecurityAuthoritySelector`.

Owning documents:
[Permissions and human control](../architecture/permissions-and-human-control.md),
[Permissions, approvals, trust](../concepts/permissions-approvals-and-trust.md),
[Deferred and human-in-the-loop](../concepts/deferred-and-human-in-the-loop.md).

## Progress

- [ ] WS3-C1 security contract additions
- [ ] WS3-C2 `ISecurityAuthority` hook-context parameter
- [ ] WS3-C3 policy context and constraint intersection
- [ ] WS3-C4 policy catalog and selector
- [ ] WS3-C5 request/decision audit; audit dispatcher mandatory
- [ ] WS3-C6a grant issuer and decision store
- [ ] WS3-C6b Json and Sqlite decision stores
- [ ] WS3-C7a typed `RevokeAsync` and revocation generation
- [ ] WS3-C7b grant-lifecycle audit in grant stores
- [ ] WS3-C7c flip `RevokeAsync` to abstract
- [ ] WS3-C8a approval handler dispatcher
- [ ] WS3-C8b `ResolveAsync`, durable deferral, `SecurityApprovalRequired`
- [ ] WS3-C8c responder binding and authentication evidence
- [ ] WS3-C9 SQLite approval store
- [ ] WS3-C10a/b/c tools and artifacts move to the selector
- [ ] WS3-C11 bounded infrastructure bootstrap capability
- [ ] WS3-C12 validator and Simple defaults
- [ ] WS3-C13 documentation

## Verified current state

| Item                                                                                                                                                                                                                                                                            | State                | Evidence                                                                                                                                                                                     |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ISecurityPolicyCatalog`, `ISecurityPolicySelector`, `SecurityPolicySnapshotResult`                                                                                                                                                                                             | MISSING              | static substitute `AgentPermissionOptions.PolicySnapshot` compared at `src/AgentKit.Permissions/SecurityAuthority.cs:183-191`                                                                |
| `IApprovalHandlerDispatcher`                                                                                                                                                                                                                                                    | MISSING              | broker holds one `IApprovalHandler` (`DefaultApprovalBroker.cs:10,26`); `DenyApprovalHandler` registered at `ServiceExtensions.cs:45`                                                        |
| `ISecurityGrantIssuer`, `ISecurityDecisionStore`                                                                                                                                                                                                                                | MISSING              | minting inline at `SecurityAuthority.cs:303-354`                                                                                                                                             |
| `RevocationReason`, `GrantRevocationResult`, `SecurityRevocationTrigger`, `SecurityRevocationConstraint`, `ApprovalValidityWindow`, `ApprovalAuthenticationEvidence(+Id)`, `ApprovalChannelId`, `SecurityApprovalRequired`, `SecurityPolicyContext`, `ApprovalResolutionResult` | MISSING              | –                                                                                                                                                                                            |
| `ISecurityAuthority.AuthorizeAsync(SecurityRequest, CT)` without hook context                                                                                                                                                                                                   | VERIFIED             | `Abstractions/Security/ISecurityAuthority.cs`; impls `SecurityAuthority.cs:104`, `DenyAllSecurityAuthority.cs`                                                                               |
| `IApprovalBroker.ResolveAsync`                                                                                                                                                                                                                                                  | MISSING              | `RequestAsync` only                                                                                                                                                                          |
| approval stores                                                                                                                                                                                                                                                                 | InMemory, Json exist | `Permissions.InMemory/InMemoryApprovalStore.cs:8`, `Permissions.Json/JsonApprovalStore.cs:28`; **SQLite missing**; suite `Conformance/ApprovalStoreConformanceTests.cs` has 3 tests          |
| durable deferral                                                                                                                                                                                                                                                                | MISSING              | broker waits inline bounded by `Binding.ExpiresAt` (`:100-123`); `DeferredOperationRequest` exists but is never produced by Permissions                                                      |
| approver identity bound into grants                                                                                                                                                                                                                                             | MISSING              | `SecurityGrant` has no approver or approval field                                                                                                                                            |
| constraint intersection                                                                                                                                                                                                                                                         | MISSING              | `SecurityPolicyResult` is kind/code/message; authority folds booleans (`SecurityAuthority.cs:197-238`)                                                                                       |
| revocation generation                                                                                                                                                                                                                                                           | static               | `_revocationVersion` captured at ctor (`:65`)                                                                                                                                                |
| audit coverage                                                                                                                                                                                                                                                                  | partial              | `GrantIssued` (`:322-352`), `Approval` (`DefaultApprovalBroker.cs:193-222`), enforcement only in session leaves; no `Request`, `Decision`, `GrantLifecycle` emitters; grant stores emit none |
| `ISecurityAuthoritySelector` consumers                                                                                                                                                                                                                                          | session only         | `src/AgentKit.Session/DefaultSessionCoordinator.cs:592`; all 15 tool packages and `DefaultArtifactCoordinator` inject unkeyed `ISecurityAuthority` (25 call sites)                           |
| bootstrap capability                                                                                                                                                                                                                                                            | MISSING              | –                                                                                                                                                                                            |
| `ISecurityGrantStore.RevokeAsync(GrantId) → bool`                                                                                                                                                                                                                               | EXISTS, old shape    | three impls; zero production callers                                                                                                                                                         |
| `IToolAuthorizer`                                                                                                                                                                                                                                                               | reduced stand-in     | `Abstractions/Tools/IToolAuthorizer.cs:11-16`; WS4 removes it                                                                                                                                |
| validator                                                                                                                                                                                                                                                                       | partial              | requires `ISecurityProfileSelector` and `ISecurityGrantStore` only (`AgentCompositionValidator.cs:72,149-150`)                                                                               |
| audit dispatcher gating                                                                                                                                                                                                                                                         | hidden constraint    | `DefaultSecurityAuditDispatcher.cs:89-99`: `Required` delivery with no durable sink for a kind returns unavailable                                                                           |

Test doubles: `ISecurityAuthority` 24 fakes in 21 projects (every
`tests/AgentKit.Tools.*.Tests/TestDoubles.cs`,
`Test.Shared/UninvokedSecurityAuthority.cs`); `ISecurityGrantStore` 36 fakes in
24 projects; `ISecurityAuditDispatcher` 25; `ISecurityPolicy` 6 (plus production
`AllowAllSecurityPolicy`, `WorkspaceScopedFileAccessPolicy`); `IApprovalBroker`
6 (all in `SecurityAuthorityTests.cs`); `IApprovalHandler` 3; `IApprovalStore`
1; `ISecurityAuthoritySelector` 2; `SecurityAuthority` built through one factory
at `SecurityAuthorityTests.cs:570`.

## Hidden prerequisites

1. WS2-C2 must land first for `HookDispatchContext`.
2. Audit-kind gating: once the authority emits `Request`/`Decision` under
   `Required` delivery, any host without a durable sink for those kinds denies
   everything. `UseLocalDevelopmentDefaults` uses `BestEffort`
   (`AgentEngineBuilderExtensions.cs:69`). Decide whether the no-audit authority
   constructor (`SecurityAuthority.cs:37-70`) is removed or kept as explicit
   "audit disabled". `guides/permissions.md:186-195` documents the gap.
3. `SecurityPolicyResult` has no constraint payload; add
   `SecurityAllowConstraints` (NO-SPEC) and the `SecurityPolicyContext`
   parameter (breaks 6 fakes and 2 production policies).
4. The `SecurityGrant` spec (`:145-160`) has no approver field; either extend
   the spec or bind the approver through the decision store.
5. `ApprovalResponse` spec adds `Authentication` and `IdempotencyKey`; the
   change must be additive or coordinate a Json codec version bump
   (`Permissions.Json/JsonApprovalResponse.cs`).
6. New required singular services need fakes in
   `tests/AgentKit.Tests/CompositionTestData.cs` and registrations in
   `UseLocalDevelopmentDefaults` (adds `AddInMemoryApprovalStore()` and a
   decision store).
7. Tool selector migration overlaps WS4; add
   `Test.Shared/FixedSecurityAuthoritySelector` first.
8. Enforcement audit at every boundary touches FileSystem, Network, Processes
   (WS5), Tools (WS4), MCP (WS6), Durability (WS12); a shared record-builder
   helper must live in Abstractions.
9. Dynamic revocation generation needs a source contract
   (`ISecurityRevocationGeneration`, NO-SPEC).
10. SQLite approval store reuses `Permissions.Sqlite` conventions.

## Spec coverage

| Contract                                                                                                                                     | Spec                                         | Status                                                               |
| -------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- | -------------------------------------------------------------------- |
| `SecurityRevocationTrigger`, `ApprovalChannelId`, `ApprovalAuthenticationEvidenceId`                                                         | `permissions-and-human-control.md:67-78`     | SPEC                                                                 |
| `SecurityApprovalRequired`                                                                                                                   | `:140-143`                                   | SPEC, missing                                                        |
| `SecurityGrant` spec shape                                                                                                                   | `:145-160`                                   | SPEC; convergence with current shape is a separate review            |
| `ApprovalValidityWindow`, `SecurityRevocationConstraint`, `ApprovalAuthenticationEvidence`, approval request/response, `SecurityAuditRecord` | `:170-230`                                   | SPEC; `ApprovalAuthenticationMethod`, `SecurityPresentation` NO-SPEC |
| `ISecurityPolicy.EvaluateAsync(request, SecurityPolicyContext, ct)`                                                                          | `:247-253`                                   | SPEC; `SecurityPolicyContext` NO-SPEC                                |
| `ISecurityPolicyCatalog`, `ISecurityPolicySelector`                                                                                          | `:269-281`                                   | SPEC; `SecurityPolicySnapshotResult` NO-SPEC                         |
| `ISecurityAuthority.AuthorizeAsync(request, HookDispatchContext?, ct)`                                                                       | `:290-296`                                   | SPEC                                                                 |
| handler dispatcher, broker `ResolveAsync`, store create/resolve, `RevokeAsync(GrantId, RevocationReason)`                                    | `:404-451`                                   | SPEC; result types NO-SPEC                                           |
| `SecurityAuthority` and `ApprovalBroker` ctors                                                                                               | `:497-515`                                   | SPEC; `ISecurityGrantIssuer`, `ISecurityDecisionStore` NO-SPEC       |
| `AgentPermissionOptions`, `ServiceExtensions`                                                                                                | `:532-664`                                   | SPEC; several option/registration records NO-SPEC                    |
| policy algebra, audit kinds                                                                                                                  | `:829-850,893-897`; concept `:74-82,241-250` | normative prose                                                      |
| `SecurityControlPlaneBootstrap`                                                                                                              | `:733-756`                                   | NO-SPEC                                                              |

## Chunks

### WS3-C1: Security contract additions

- Depends on: WS2-C2. Risk: ADDITIVE. Size: M.
- Deliverables: `Identity/ApprovalChannelId.cs`,
  `ApprovalAuthenticationEvidenceId.cs`;
  `Security/SecurityRevocationTrigger.cs`, `SecurityRevocationConstraint.cs`,
  `ApprovalValidityWindow.cs`, `ApprovalAuthenticationMethod.cs`,
  `ApprovalAuthenticationEvidence.cs`, `SecurityApprovalRequired.cs`,
  `RevocationReason.cs`, `GrantRevocationResult.cs` (+`Revoked`,
  `AlreadyRevoked`, `NotFound`, `Unavailable`),
  `SecurityPolicySnapshotResult.cs` (+3), `SecurityPolicyContext.cs`,
  `ApprovalResolutionResult.cs` (+5), `ISecurityPolicyCatalog.cs`,
  `ISecurityPolicySelector.cs`, `IApprovalHandlerDispatcher.cs`,
  `ISecurityGrantIssuer.cs`, `ISecurityDecisionStore.cs`,
  `SecurityAllowConstraints.cs`; guard tests. Snapshot: Abstractions.

### WS3-C2: `ISecurityAuthority` hook-context parameter

- Depends on: C1. Risk: ADDITIVE via default interface member. Size: S.
- Deliverables: 3-arg `AuthorizeAsync` as DIM forwarding to the 2-arg member;
  `SecurityAuthority` and `DenyAllSecurityAuthority` implement the 3-arg form;
  `DefaultSessionCoordinator.cs:599` passes `null`. The abstract flip (24 fakes,
  25 call sites) is deferred to the end of WS4. Snapshots: Abstractions,
  Permissions.

### WS3-C3: Policy context and constraint intersection

- Depends on: C1. Risk: CONTRACT-BREAK (`AllowAllSecurityPolicy`,
  `WorkspaceScopedFileAccessPolicy`, 5 fakes in `SecurityAuthorityTests`,
  `Simple.Tests:892`). Size: M.
- Deliverables:
  `ISecurityPolicy.EvaluateAsync(request, SecurityPolicyContext, ct)`;
  `SecurityPolicyResult.Constraints`; `AuthorizeCoreAsync` (`:197-306`)
  rewritten: deterministic order, deny precedence, intersection across
  allow/require-approval, empty intersection →
  `security.constraint_intersection_empty`, host ceilings intersected last;
  algebra matrix tests.

### WS3-C4: Policy catalog and selector

- Depends on: C3. Risk: DENSE-MODIFY `SecurityAuthority` ctor (`:37-101`) and
  `:183-191`; `ServiceExtensions.cs:55-73`; test factory `:570`. Size: M.
- Deliverables: `SecurityPolicyCatalog` (snapshots from options and every
  `SecurityProfilePublication`), `DefaultSecurityPolicySelector`; authority
  takes the selector; `ReplaceSecurityPolicyCatalog<T>`,
  `ReplaceSecurityPolicySelector<T>`; denial
  `security.policy_snapshot_unavailable`.

### WS3-C5: Request and decision audit; audit dispatcher mandatory

- Depends on: C4. Risk: DENSE-MODIFY `SecurityAuthority.cs:104-174,320-352`,
  `ServiceExtensions.cs:62-73`. Size: M.
- Deliverables: `Request` audit before evaluation, `Decision` after (decision
  code, winning policy, constraint summary as `RedactedAuditValue`); one
  constructor requiring `ISecurityAuditDispatcher`, `IApprovalBroker`,
  `IIdentifierGenerator<ApprovalRequestId>`; required-audit failure denies
  before any grant. Docs: `guides/permissions.md:186-195`.

### WS3-C6a: Grant issuer and decision store wired

- Depends on: C5. Risk: DENSE-MODIFY `SecurityAuthority.cs:295-354`. Size: M.
- Deliverables: `DefaultSecurityGrantIssuer`, authority ctor converges to
  `:497-505`, decision store written after every decision;
  `InMemorySecurityDecisionStore` and `AddInMemorySecurityDecisionStore()`.
  Snapshots: Permissions, Permissions.InMemory.

### WS3-C6b: Json and Sqlite decision stores

- Depends on: C6a. Risk: ADDITIVE. Size: M.
- Deliverables: two adapters plus `SecurityDecisionStoreConformanceTests`.

### WS3-C7a: Typed `RevokeAsync` and revocation generation

- Depends on: C1. Risk: ADDITIVE. Size: S–M.
- Deliverables: 3-arg `RevokeAsync(GrantId, RevocationReason, CT)` DIM mapping
  to the bool member; three stores implement natively;
  `ISecurityRevocationGeneration` read at issue and consume;
  `InMemorySecurityRevocationGeneration`; extend
  `SecurityGrantStoreConformanceTests`.

### WS3-C7b: Grant-lifecycle audit in grant stores

- Depends on: C7a, C5. Risk: DENSE-MODIFY three stores
  (`SqliteSecurityGrantStore.cs` is 943 lines), three leaf registrations, three
  fixtures. Size: M.
- Deliverables: each store takes an optional `ISecurityAuditDispatcher` and
  emits `GrantLifecycle` for consume/expire/revoke; `Required` failure →
  consumption refused. Snapshots: three leaves.

### WS3-C7c: Flip `RevokeAsync` to abstract

- Depends on: C7b. Risk: CONTRACT-BREAK (36 fakes, 24 projects), mechanical.
  Size: S.

### WS3-C8a: Approval handler dispatcher

- Depends on: C1. Risk: DENSE-MODIFY `DefaultApprovalBroker.cs:9-44,99-128`,
  `ServiceExtensions.cs:45-53`. Size: S.
- Deliverables: `DefaultApprovalHandlerDispatcher` over
  `IEnumerable<IApprovalHandler>` (first decisive wins);
  `AddApprovalHandler<T>()` additive; `DenyApprovalHandler` remains the terminal
  fallback.

### WS3-C8b: `ResolveAsync`, durable deferral, `SecurityApprovalRequired`

- Depends on: C8a, C6a. Risk: CONTRACT-BREAK on `IApprovalBroker` (6 fakes) or
  DIM. Size: M.
- Deliverables: broker `ResolveAsync(ApprovalResponse)`; when no inline answer,
  `HeadlessApprovalBehavior == Defer`, and the store is durable →
  `ApprovalBrokerDeferred`; authority returns `SecurityApprovalRequired` with a
  `DeferredOperationRequest`; `AgentPermissionOptions.HeadlessApprovalBehavior`;
  defer/resolve/replay tests across InMemory and Json. Snapshots: Abstractions,
  Permissions.

### WS3-C8c: Responder binding and authentication evidence

- Depends on: C8b. Risk: CONTRACT-BREAK if `ApprovalResponse` ctor changes;
  prefer an additive overload. Size: M.
- Deliverables: `ApprovalResponse` gains optional `Authentication` and
  `IdempotencyKey`; approver recorded in the decision store; optional
  `ApprovalResponseId? Approval` on `SecurityGrant`; Json codec version bump
  with torn-record tests.

### WS3-C9: SQLite approval store

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `SqliteApprovalStore` and codec/options/settings/target in
  `Permissions.Sqlite`, `AddSqliteApprovalStore`;
  `SqliteApprovalStoreConformanceFixture`; extend the suite with
  read-after-create, resolve idempotency, expiry. Snapshot: Permissions.Sqlite.

### WS3-C10a/b/c: Tools and artifact coordinator move to the selector

- Depends on: C2; coordinate with WS4. Risk: DENSE-MODIFY per package (ctor,
  `AuthorizeAsync` site, `ServiceExtensions`). Size: M each.
- C10a: `Test.Shared/FixedSecurityAuthoritySelector`,
  `Artifacts/DefaultArtifactCoordinator.cs`, Plan and Patch tools. C10b:
  Read/Write/Edit/Glob/Search/List. C10c: Command/Web/WebSearch/Language/
  Question/Task/Resource/Skill. Each selects by
  `ToolExecutionContext.Authorization`.

### WS3-C11: Bounded infrastructure bootstrap capability

- Depends on: C5. Risk: ADDITIVE, NO-SPEC (write a block in
  `permissions-and-human-control.md:733-756` first). Size: M.
- Deliverables: an explicit host bootstrap capability that authorizes security
  control-plane persistence without recursing through grant/audit writes; used
  by the grant, approval, and decision stores at initialization.

### WS3-C12: Validator and Simple defaults

- Depends on: C4, C6a, C8a. Risk: DENSE-MODIFY `AgentCompositionValidator.cs`.
  Size: M.
- Deliverables: require singular `ISecurityAuthoritySelector`,
  `ISecurityPolicyCatalog`, `IApprovalBroker`, `ISecurityAuditDispatcher`,
  `IApprovalStore`, `ISecurityDecisionStore`; per-definition security profile
  resolution; `CompositionTestData` fakes; `UseLocalDevelopmentDefaults`
  registers approval and decision stores; Simple `WithPolicy<T>` sugar.

### WS3-C13: Documentation

- Depends on: all. Size: S.
- Deliverables: `permissions-and-human-control.md` reconciliation,
  `guides/permissions.md`, `src/AgentKit.Permissions*/README.md`, permissions
  skill.

## Totals

S 4, M 13 (C7a counted as M). Confidence medium: the audit-gating decision
(prerequisite 2) and the `SecurityGrant` approver field decision can each
reshape a chunk; the selector migration is mechanical but broad.
