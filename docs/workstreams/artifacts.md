# WS15: Artifacts completion

Goal: artifact requests carry captured authorization instead of loose
identities, metadata uses the shared classification and external ownership, the
coordinator is keyed with a profile snapshot, store selector, integrity
validator, retention policy, events, and observability, reference-commit intent
and orphan reconciliation are implemented, and Sqlite, Json, and file-system
stores join the InMemory one under an extended conformance suite.

Owning documents: [Artifacts](../architecture/artifacts.md),
[Artifact and content storage](../concepts/artifact-and-content-storage.md).

## Progress

- [x] WS15-C1 missing contracts (additive)
- [x] WS15-C2 metadata, reference, request shape migration
- [x] WS15-C3 conformance suite extension
- [x] WS15-C4 coordinator rewrite
- [x] WS15-C5 reference-commit intent and reconciliation
- [x] WS15-C6 `AgentKit.Artifacts.Sqlite`
- [x] WS15-C7 `AgentKit.Artifacts.Json`
- [x] WS15-C8 `AgentKit.Artifacts.FileSystem`
- [x] WS15-C9 definition key, validator, consumers, Simple, documentation

## Verified current state

| Item                                                                                                                  | State                                                                      | Evidence                                                                                          |
| --------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| two-phase coordinator                                                                                                 | LANDED, keyed, `ISecurityAuthoritySelector`                                | `src/AgentKit.Artifacts/ArtifactCoordinator.cs`; `AddAgentArtifacts(key, profileKey, …)`          |
| `IArtifactStore`                                                                                                      | LANDED, typed store results                                                | `Abstractions/Artifacts/IArtifactStore.cs`, `ArtifactStore*` result families                      |
| `ArtifactPrepareRequest` authorization                                                                                | LANDED, `SecurityAuthorizationContext`                                     | `Abstractions/Artifacts/ArtifactPrepareRequest.cs`                                                |
| `ArtifactMetadata` hash, classification, external ownership                                                           | LANDED, `ContentHash?`, `DataClassification`, `ExternalArtifactOwnership?` | `Abstractions/Artifacts/ArtifactMetadata.cs`                                                      |
| `.InMemory` store and suite                                                                                           | LANDED on shared planner/backend; suite extended                           | `src/AgentKit.Artifacts.InMemory`; `tests/AgentKit.Conformance/ArtifactStoreConformanceTests.cs`  |
| `ArtifactProcessOutputSink`                                                                                           | LANDED, bound per coordinator                                              | `src/AgentKit.Artifacts/ArtifactProcessOutputSink.cs`                                             |
| `.Sqlite`, `.Json`, `.FileSystem` stores                                                                              | LANDED                                                                     | `src/AgentKit.Artifacts.Sqlite`, `.Json`, `.FileSystem` and their test projects                   |
| backend key, selector, profile and options snapshots, validator, retention, events, ownership, intent, reconciliation | LANDED                                                                     | `Abstractions/Artifacts/*`, `src/AgentKit.Artifacts/*`                                            |
| observability                                                                                                         | LANDED                                                                     | `artifact.*` activities, metrics, tags, `ArtifactLog` 29000-29005, `ArtifactStoreLog` 29100-29106 |
| definition key and composition validation                                                                             | LANDED                                                                     | `src/AgentKit/ArtifactCompositionValidator.cs`; `IArtifactCoordinatorCatalog`; `WithArtifacts`    |
| identities                                                                                                            | EXISTS                                                                     | `Abstractions/Identity/Artifact*.cs`                                                              |

Test doubles: `IArtifactStore` 1 fake plus 1 fixture; `IArtifactCoordinator` 1;
`IProcessOutputArtifactSink` 1; `ArtifactReference`/`ArtifactMetadata`
constructed in 23 files.

## Hidden prerequisites

1. Generic `DataClassification` (WS14-C1) before C2, or introduce it in C1 and
   let WS14 reuse it.
2. `AgentKit.Artifacts` adds the Observability reference in C4.
3. `ArtifactBackendKey` placement: Abstractions (leaves reference only
   Abstractions).
4. `SecurityAuthorizationContext` on requests (C2).
5. WS5 write-disposition API for the file-system store (C8).
6. WS1 session export and WS14 `DocumentRecord` consumers (C9).
7. `ArtifactProcessOutputSink` resolves the un-keyed coordinator; C4 binds it to
   one key.

## Spec coverage

| Contract                                                                                                                                                                            | Spec                       |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| identities, enums, retention, integrity, external ownership, metadata, reference, prepare/finalize/abort, `IArtifactStore`, `IArtifactCoordinator`, event sink and dispatcher       | `artifacts.md:56-239`      |
| `ArtifactBackendKey`, store selector, profile snapshot, coordinator ctor, options, profile options, DI                                                                              | `:251-361`                 |
| store result families, event leaves, sink registration, integrity validator, retention policy bodies, commit intent, reconciliation request/result, `ArtifactPin`, selection result | NO-SPEC (prose `:397-427`) |
| tenant, replay, finalize-race rules                                                                                                                                                 | concept `:72-107,151-175`  |

## Chunks

### WS15-C1: Missing contracts (additive)

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Identity/ExternalArtifactResourceId`, `ArtifactEventSinkId`;
  `Artifacts/ExternalArtifactOwnership`, `ArtifactEvent` (+prepared, finalized,
  aborted, deleted, reconciled), `IArtifactEventSink`,
  `IArtifactEventDispatcher`, `ArtifactEventSinkRegistration`,
  `IArtifactIntegrityValidator`, `IArtifactRetentionPolicy`,
  `ArtifactRetentionDecision`, `ArtifactReferenceCommitIntent` and id,
  reconciliation request/result, `ArtifactPin`. Snapshot: Abstractions.
- Landed: every listed contract plus `ArtifactBackendKey`,
  `ArtifactReferenceCommitIntentId`,
  `ArtifactReferenceCommitState/Outcome/Result`, reconciliation disposition and
  pending-reason types, `ArtifactCoordinatorSnapshot`, and
  `IArtifactCoordinatorCatalog`. Abstractions tests pass (7496).

### WS15-C2: Metadata, reference, request shape migration

- Depends on: WS14-C1. Risk: CONTRACT-BREAK (`DefaultArtifactCoordinator`,
  `ArtifactProcessOutputSink`, `AgentArtifactOptions`, `InMemoryArtifactStore`,
  `OperatingSystemProcessRunner.cs:548`, `ArtifactTestDoubles`,
  `ArtifactTestData`, coordinator tests, InMemory tests, conformance suite, 23
  construction files). Size: L.
- Deliverables: `ArtifactMetadata` (`ContentHash? DeclaredContentHash`,
  `DataClassification`, `ExternalArtifactOwnership?`), `ArtifactReference` (+
  external ownership), requests take `SecurityAuthorizationContext`,
  `IArtifactStore` returns store-level results. Snapshots: Abstractions,
  Artifacts, Artifacts.InMemory, Processes, Tools.Command.
- Landed: metadata and reference carry `ContentHash?`, the shared
  `DataClassification`, and `ExternalArtifactOwnership?`; requests carry
  `SecurityAuthorizationContext`; `IArtifactStore` returns typed results.
  `ArtifactDataClassification` was deleted. All callers (Processes, memory
  document, tests) updated in place.

### WS15-C3: Conformance suite extension

- Depends on: C2. Risk: ADDITIVE. Size: S.
- Deliverables: finalize/abort race single winner, equivalent retry same
  reference, tenant isolation, integrity mismatch typed, legal-hold delete
  rejected, external ownership never deletes, tombstone prevents rebind.
- Landed: `IArtifactStoreConformanceFixture`,
  `ArtifactStoreConformanceFixtureBase`, and about 35 cases including
  finalize/abort race, equivalent retry, tenant isolation, integrity mismatch,
  legal hold, external ownership, tombstone, and read verification.

### WS15-C4: Coordinator rewrite

- Depends on: C1, C2. Risk: DENSE-MODIFY `DefaultArtifactCoordinator.cs`
  (replaced), `ServiceExtensions.cs:15-31`, `AgentArtifactOptions.cs`. Size: L.
- Deliverables: `ArtifactBackendKey`, `IArtifactStoreSelector` and default,
  `ArtifactProfileSnapshot`, `ArtifactProfileOptions`,
  `AgentArtifactOptionsSnapshot`, `ArtifactRegistration`,
  `DefaultArtifactIntegrityValidator` (SHA-256),
  `DefaultArtifactRetentionPolicy`, `ArtifactEventDispatcher`, `ArtifactLog`,
  `ArtifactMetrics`, `artifact.*` activity names;
  `AddAgentArtifacts(key, profileKey, …)`, `AddArtifactProfile`,
  `AddArtifactStore<T>(ArtifactBackendKey)`, `AddArtifactEventSink<T>`,
  `Replace*`; authorization through `ISecurityAuthoritySelector`; the process
  output sink bound to one key.
- Landed: internal keyed `ArtifactCoordinator` (the public
  `DefaultArtifactCoordinator` was replaced), profile snapshots with retained
  versions, default selector, integrity validator, retention policy, dispatcher,
  observability, and registration. `tests/AgentKit.Artifacts.Tests` passes
  (145). Deviation: finalize/abort/reconcile probe each backend because the
  requests name no directory.

### WS15-C5: Reference-commit intent and reconciliation

- Depends on: C4. Risk: ADDITIVE. Size: M.
- Deliverables: `IArtifactReferenceCommitIntentStore` plus InMemory impl;
  `ReconcileAsync` per `artifacts.md:397-427` (fence late commit → terminal
  disposition → collect or retain with pending); `OrphanRetention` option;
  fenced-collection tests.
- Landed: `ReconcileAsync` (fence-first) and
  `InMemoryArtifactReferenceCommitIntentStore`. Deviation: SQLite and JSON
  intent store adapters were not built; the intent store is caller-owned.

### WS15-C6: `AgentKit.Artifacts.Sqlite`

- Depends on: C3. Risk: ADDITIVE new project. Size: L.
- Landed: `AgentKit.Artifacts.Sqlite` with exclusive-lock single-writer store;
  77 tests including the shared conformance suite.

### WS15-C7: `AgentKit.Artifacts.Json`

- Depends on: C3. Risk: ADDITIVE new project. Size: M.
- Deliverables: metadata via `JsonRecordLog`, payloads as content-addressed
  files written with `JsonAtomicDocument`.
- Landed: `AgentKit.Artifacts.Json` with record log metadata and
  tenant-hash-partitioned content-addressed payloads; 69 tests including
  conformance.

### WS15-C8: `AgentKit.Artifacts.FileSystem`

- Depends on: C3, WS5. Risk: ADDITIVE new project. Size: M.
- Deliverables: store over the protected file-system contracts with explicit
  dispositions; suite over `AgentKit.FileSystem.InMemory`.
- Landed: `AgentKit.Artifacts.FileSystem` over the protected file-system
  boundary; 61 tests including conformance over `AgentKit.FileSystem.InMemory`.
  Deviation: no delete or enumeration exists on the boundary, so released
  payloads are truncated, not removed, and crash leftovers are not swept.

### WS15-C9: Definition key, validator, consumers, Simple, documentation

- Depends on: C4–C8, WS14-C3. Risk: DENSE-MODIFY definition, validator, Simple.
  Size: M.
- Deliverables: `ComponentKey<IArtifactCoordinator>? ArtifactCoordinator`;
  validator per `artifacts.md:429-440`; session export and memory document
  consumers; `WithArtifacts`; skill.
- Landed: `ArtifactCompositionValidator` wired from `AgentCompositionValidator`,
  `IArtifactCoordinatorCatalog` default implementation, `WithArtifacts` with
  `SimpleArtifactOptions`, memory document consumer update, skill and
  documentation updates, regenerated API snapshots. Deviation: no session-export
  code exists in `src/`, so there was no export consumer to migrate.

## Totals

S 1, M 4, L 4. Confidence high on state and break surface; medium on C4 and C6
sizing.
