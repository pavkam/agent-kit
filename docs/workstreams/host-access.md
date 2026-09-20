# WS5: Host access completion

Goal: file-system, network, and process boundaries expose the specified
capability contracts (bounded reads, explicit write dispositions, handle-based
processes with streamed output and termination certainty, route-partitioned
network pooling with egress evidence), each keyed by profile, each enforcing
grants and required audit at the host boundary, each covered by a shared
conformance suite.

Owning documents: [File system](../architecture/file-system.md),
[Network](../architecture/network.md),
[Process execution](../architecture/process-execution.md), and the matching
concept specifications for access bounds, egress, and sandboxing.

## Progress

- [x] WS5-C1 arm64 Linux `O_NOFOLLOW`/`O_DIRECTORY`
- [ ] WS5-C2 file-system value types
- [ ] WS5-C3 file-system capability contracts
- [ ] WS5-C4a `OperatingSystemFileReader`
- [ ] WS5-C4b `OperatingSystemFileWriter`
- [ ] WS5-C5 InMemory reader/writer/metadata/directory-create
- [ ] WS5-C6 file-system conformance suite
- [ ] WS5-C7 Read/Write tools migrate
- [ ] WS5-C8 List/Glob/Search/Edit/Patch onto keyed profiles
- [ ] WS5-C9 network contract additions
- [ ] WS5-C10a route-partitioned pooling
- [ ] WS5-C10b network audit, keyed profiles, stream upload
- [ ] WS5-C11 network conformance suite
- [ ] WS5-C12 process contracts (handle-based)
- [ ] WS5-C13a `OperatingSystemProcessExecutor` start and streamed output
- [ ] WS5-C13b terminate, exit status, audit, reevaluation
- [ ] WS5-C14 scripted executor and process conformance suite
- [ ] WS5-C15 `CommandTool` onto `IProcessExecutor`
- [ ] WS5-C16 `WebFetchTool` onto split bounds
- [ ] WS5-C17 documentation

## Verified current state

Abstractions live under `src/AgentKit.Abstractions/Host/`.

### File system

| Member                                                                                                                                                                                                                                                                                                                                                                                                                         | Existing                                                                                                                                                                                                  | Spec                                                                                                                                                                               |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IFileSystem.ReadAsync/WriteAsync` (text only)                                                                                                                                                                                                                                                                                                                                                                                 | EXISTS-AND-USED `Host/IFileSystem.cs:21-39`; users `ReadFileTool.cs:72`, `WriteFileTool.cs:50`, `WorkspaceScopedFileAccessPolicy.cs`; impls `SandboxedFileSystem.cs:25-33`, `InMemoryFileSystem.cs:39-47` | not in spec; plan keeps as obsolete adapter                                                                                                                                        |
| `FileReadRequest(FileSystemPath, SecurityGrant)`, `FileWriteRequest`, `FileWriteMode {CreateOrOverwrite, CreateNew, Append, ReplaceExisting}`                                                                                                                                                                                                                                                                                  | EXISTS                                                                                                                                                                                                    | spec `FileReadRequest` has a different shape (`file-system.md:136-142`); `FileWriteDisposition` (`:213-219`)                                                                       |
| `IDirectoryReader`, `IFileGlobber`, `IFileContentSearcher`, `IFileSnapshotReader`, `IAtomicFileReplacer`, `IWorkspacePatchApplier`                                                                                                                                                                                                                                                                                             | EXISTS-AND-USED by List/Glob/Search/Edit/Patch/Resource/Skill                                                                                                                                             | `IDirectoryReader` shape differs (`:171-176`)                                                                                                                                      |
| `FileTarget`, `ResolvedFileTarget`, `AuthorizedFileRead/Write`, `FileWriteContent`, `FileReadBounds`, `FileWriteDisposition`, `FileWriteOutcomeKind`, `FileWriteSuccess`, `IFileReader`, `IFileReadHandle`, `IFileWriter`, `IFileMetadataReader`, `IFileChangeSource`, `ITemporaryFileStore`, `IFileSystemSelector`, `FileSystemCapabilities`, `FileSystemProfileKey`, `IDirectoryCreator`, `OperatingSystemFileReader/Writer` | MISSING                                                                                                                                                                                                   | `:127-235,385-425`                                                                                                                                                                 |
| keyed registration `AddOperatingSystemFileSystem(FileSystemProfileKey, …)`, `AddInMemoryFileSystem(key, …)`                                                                                                                                                                                                                                                                                                                    | MISSING; un-keyed `AddSandboxedFileSystem(root, …)`, `AddInMemoryFileSystem(Action?)`                                                                                                                     | `:465-486`                                                                                                                                                                         |
| audit in FS/Network/Processes                                                                                                                                                                                                                                                                                                                                                                                                  | MISSING (zero `ISecurityAuditDispatcher` references)                                                                                                                                                      | `:407-408`                                                                                                                                                                         |
| arm64 constants                                                                                                                                                                                                                                                                                                                                                                                                                | EXISTS (WS5-C1)                                                                                                                                                                                           | `PosixOpenFlags` selects aarch64 `O_DIRECTORY=0x4000` / `O_NOFOLLOW=0x8000`; macOS and other Linux arches keep their previous constants. `SandboxedFileSystem` open flags call it. |

Test doubles: `IFileSystem` 4 (`ReplacementFileSystem` ×2, `FakeFileSystem` ×2),
`IDirectoryReader` 1, `IFileGlobber` 1, `IFileContentSearcher` 1,
`IFileSnapshotReader` 4, `IAtomicFileReplacer` 1, `IWorkspacePatchApplier` 1.

### Network

`INetworkTransport.SendAsync`, `INetworkNameResolver.ResolveAsync`,
`INetworkResponse`, `NetworkRequest` match `network.md:76-124`.
`NetworkRequestContent` is bytes only (`:30`); `NetworkBounds` is one record
(`:33-57`); pooling is disabled (`DefaultNetworkTransport.cs:60-74`,
`PooledConnectionLifetime = Zero`); proxy/TLS/decompression descriptors, profile
keys, sent-bytes evidence, and audit are MISSING (prose only
`network.md:122-149,196-217`). Zero test fakes; tests use
`ScriptedNetworkTransport`.

### Processes

`IProcessRunner.RunAsync` is run-to-completion (`Host/IProcessRunner.cs:7-17`;
impls `OperatingSystemProcessRunner.cs`, `ScriptedProcessRunner.cs`; consumer
`CommandTool.cs:59`). `IProcessIntentResolver` and
`IProcessSandboxProvider.ProfileId/PrepareAsync` exist; spec wants
`IExecutableResolver` (`process-execution.md:129-134`) and
`Descriptor/CreateAsync` (`:136-143`). `ProcessSideEffectCertainty` duplicates
the shared `SideEffectCertainty`. Permits and SIGTERM/SIGKILL tree kill exist
internally (`OperatingSystemProcessRunner.cs:15,99,386-460`). All handle-based
contracts (`IProcessExecutor`, `IProcessHandle`, `ProcessStartRequest`,
`ProcessExitResult`, `ProcessOutputEvent`, termination, `ProcessExecutorKey`,
selectors, keyed `AddAgentProcesses`) are MISSING (`:99-180,213-256,300-328`).
Test doubles: `IProcessRunner` 1, `IProcessIntentResolver` 3,
`IProcessSandboxProvider` 1.

### Language services

`ILanguageIntelligenceService.QueryAsync` exists with one scripted impl and one
consumer; no normative C# block anywhere. No WS5 chunk; leave as is.

Conformance suites for all three boundaries are MISSING.

## Hidden prerequisites

1. Name collisions: `FileReadRequest`, `IDirectoryReader`, and
   `IProcessSandboxProvider` member shapes are taken by reduced types still in
   use; each needs a rename-to-legacy step before the spec type can appear.
2. About fifteen value types are named in the spec without bodies
   (`NormalizedRelativePath`, `FilePathPolicy`, `FileSystemEntry`,
   `FileMetadata`, `AuthorizedDirectoryEnumeration`,
   `AuthorizedFileMetadataRead`, `AuthorizedFileWatch`,
   `ProcessExecutableReference`, `ProcessArgument`, `EnvironmentProjection`,
   `ProcessInput`, `ProcessEffectClass`, `ResolvedExecutable`,
   `EnvironmentFingerprint`); each needs a C# block.
3. Required-audit semantics come from WS3's dispatcher; the boundaries can
   consume `ISecurityAuditDispatcher` now.
4. WS6's stdio transport is blocked on C12/C13a.
5. Leaves reference only Abstractions and Observability; keep it so.

## Spec coverage

| Contract                                                                    | Spec                                          |
| --------------------------------------------------------------------------- | --------------------------------------------- |
| FS values and capability interfaces                                         | `file-system.md:124-235`                      |
| `OperatingSystemFileReader` deps, options snapshot; FS DI                   | `file-system.md:393-425,458-508`              |
| `IDirectoryCreator`, `FileSystemCapabilities` body, `FileReadBounds` body   | NO-SPEC (prose `:269-277,536-555`)            |
| network contracts; network DI (un-keyed)                                    | `network.md:76-124,221-244`                   |
| stream upload, split bounds, proxy/TLS, sent-bytes, pooling, keyed profiles | NO-SPEC C# (prose `:122-149,196-217`)         |
| process contracts; executor deps; process DI                                | `process-execution.md:99-180,213-256,300-328` |
| `ProcessOutputEvent`, `ProcessStartResult`, `ProcessExitResult` bodies      | NO-SPEC (prose `:195-200`)                    |
| language services                                                           | NO-SPEC                                       |

## Chunks

### WS5-C1: arm64 Linux `O_NOFOLLOW` and `O_DIRECTORY`

- Depends on: –. Risk: DENSE-MODIFY `SandboxedFileSystem.cs:979-987`. Size: S.
- Deliverables: `RuntimeInformation.ProcessArchitecture` branch (aarch64 Linux:
  `O_DIRECTORY=0x4000`, `O_NOFOLLOW=0x8000`); behavior tests per OS/arch in
  `tests/AgentKit.FileSystem.Tests`.
- Landed: `PosixOpenFlags` is the single flag table. macOS wins over arm64.
  Linux arm64 uses `0x4000`/`0x8000`; other Linux architectures keep the
  historical x86_64 constants, which also match riscv64. Tests cover each branch
  without requiring that host. Existing symlink-rejection tests still exercise
  the live flags on the current OS.

### WS5-C2: File-system value types

- Depends on: –. Risk: ADDITIVE plus rename of legacy `FileReadRequest` to
  `LegacyFileReadRequest` `[Obsolete]` (touches `ReadFileTool`,
  `SandboxedFileSystem`, `InMemoryFileSystem`,
  `WorkspaceScopedFileAccessPolicy`, 4 fakes). Size: M.
- Deliverables: `Abstractions/Host/FileOperationId`, `FileRootId`,
  `FileSystemProfileKey`, `FileSystemProfileVersion`, `FileTarget`,
  `ResolvedFileTarget`, `FileReadBounds`, `FileWriteDisposition`,
  `FileWriteOutcomeKind`, `FileWriteContent`, `FileWriteSuccess` and closed
  `FileWriteResult` variants, `AuthorizedFileRead`, `AuthorizedFileWrite`,
  `FileMetadata`. Snapshots: Abstractions, FileSystem, FileSystem.InMemory,
  Tools.Read, Permissions.
- Open: alias or replace `FileWriteMode`.

### WS5-C3: File-system capability contracts

- Depends on: C2. Risk: ADDITIVE plus `IDirectoryReader` collision. Size: M.
- Deliverables: `IFileReader`, `IFileReadHandle`, `IFileWriter`,
  `IFileMetadataReader`, `IFileChangeSource`, `ITemporaryFileStore`,
  `IFileSystemSelector`, `FileSystemCapabilities`, `FileSystemCapability`,
  `FileSystemSelectionResult`, `IDirectoryCreator` with
  `AuthorizedDirectoryCreate`; `IFileSystem` marked obsolete. Snapshot:
  Abstractions.

### WS5-C4a: `OperatingSystemFileReader`

- Depends on: C2, C3. Risk: ADDITIVE. Size: M.
- Deliverables: `src/AgentKit.FileSystem/OperatingSystemFileReader.cs`, options
  and snapshot, registration, keyed
  `AddOperatingSystemFileSystem(FileSystemProfileKey, …)`; extract P/Invoke
  helpers into internal `PosixFileOperations`; grant consumption and
  required-audit fail-closed; tests for fragment-boundary bounded reads,
  growth-after-stat truncation evidence, symlink rejection.

### WS5-C4b: `OperatingSystemFileWriter`

- Depends on: C4a. Risk: ADDITIVE. Size: M.
- Deliverables: four dispositions, expected-state fingerprint precondition,
  payload and final `ContentHash`, atomic replace reusing
  `SandboxedFileSystem.Edit.cs` mechanics; disposition × precondition matrix
  tests.
- Open: append atomicity wording on POSIX.

### WS5-C5: InMemory reader/writer/metadata/directory-create

- Depends on: C3. Risk: ADDITIVE. Size: M.
- Deliverables: new contracts on `InMemoryFileSystem*`, keyed
  `AddInMemoryFileSystem(FileSystemProfileKey, …)`. Snapshot:
  FileSystem.InMemory.

### WS5-C6: File-system conformance suite

- Depends on: C4, C5. Risk: ADDITIVE. Size: M.
- Deliverables: `tests/AgentKit.Conformance/IFileSystemConformanceFixture.cs`,
  `FileSystemConformanceTests.cs` (bounds, dispositions, symlink where declared,
  directory-create denied without its own grant, audit-required closure);
  fixtures in both FS test projects.

### WS5-C7: Read/Write tools migrate

- Depends on: C4, C5. Risk: DENSE-MODIFY `ReadFileTool.cs:71-76`,
  `WriteFileTool.cs:49-53`. Size: M.
- Deliverables: tools request `FileRead`/`FileWrite` authority for a
  `FileTarget` and declare disposition; fake reader/writer in their tests.
  Snapshots: Tools.Read, Tools.Write.

### WS5-C8: List/Glob/Search/Edit/Patch onto keyed profiles

- Depends on: C3, C7. Risk: DENSE-MODIFY five tools. Size: M.
- Deliverables: spec `IDirectoryReader` in List; others resolve through
  `IFileSystemSelector`; delete legacy `IFileSystem` and `LegacyFileReadRequest`
  at the end. Snapshots: five packages, Abstractions.

### WS5-C9: Network contract additions

- Depends on: –. Risk: ADDITIVE (ctor additions). Size: M.
- Deliverables: streaming `NetworkRequestContent` variant with staged
  fingerprint; split `NetworkBounds` into resolution/request/response bounds;
  `NetworkProxyDescriptor`, `NetworkTlsPolicy`, `NetworkDecompressionPolicy`,
  `NetworkProfileKey`; sent-bytes and egress fingerprint on responses.
  Snapshots: Abstractions, Network, Network.InMemory, Tools.Web.

### WS5-C10a: Route-partitioned pooling

- Depends on: C9. Risk: DENSE-MODIFY `DefaultNetworkTransport.cs:60-74`. Size:
  M.
- Deliverables: per-(route, peer, proxy, TLS/audience) `SocketsHttpHandler`
  partitions; pre-send peer verification on reuse (`network.md:196-208`);
  loopback tests proving reuse only within a partition.

### WS5-C10b: Network audit, keyed profiles, stream upload

- Depends on: C9, C10a. Risk: ADDITIVE plus DENSE-MODIFY
  `Network/ServiceExtensions.cs:19-50`. Size: M.
- Deliverables: `ISecurityAuditDispatcher` in resolver and transport; keyed
  `AddAgentNetwork(NetworkProfileKey, …)` and `INetworkProfileSelector` (NO-SPEC
  overload; write block); staged-spool upload with fingerprint enforcement.

### WS5-C11: Network conformance suite

- Depends on: C9, C10. Risk: ADDITIVE. Size: M.
- Deliverables: `INetworkTransportConformanceFixture`,
  `NetworkTransportConformanceTests` (redirect re-auth, oversize, timeout,
  fragmentation, upload fingerprint, denial before connect); fixtures for the
  loopback OS transport and `ScriptedNetworkTransport`.

### WS5-C12: Process contracts

- Depends on: –. Risk: ADDITIVE plus one CONTRACT-BREAK
  (`ProcessSideEffectCertainty` → shared `SideEffectCertainty`;
  `ProcessRunResult`, both runners, `CommandTool`, two fakes). Size: M.
- Deliverables: `ProcessExecutorKey`, `ProcessExecutorVersion`,
  `ProcessStartRequest`, `ResolvedProcessStart`, `ProcessStartResult`,
  `ProcessExitResult` (Exited/Signalled/Cancelled/TimedOut/Killed/Unknown),
  `ProcessOutputEvent` hierarchy, `ProcessTerminationRequest/Result`,
  `IExecutableResolver`, `IProcessExecutor`, `IProcessHandle`,
  `IProcessExecutorSelector`, `IProcessSandboxSelector`, `SandboxDescriptor`,
  `ProcessSandboxRequest`; `IProcessRunner` obsolete. Snapshots: Abstractions,
  Processes, Processes.Scripted, Tools.Command.

### WS5-C13a: `OperatingSystemProcessExecutor` start and streamed output

- Depends on: C12. Risk: ADDITIVE (reuses runner internals). Size: L (scoped to
  start, `ReadOutputAsync`, `Completion`).
- Deliverables: `OperatingSystemProcessExecutor.cs`,
  `OperatingSystemProcessHandle.cs`, `OperatingSystemExecutableResolver.cs`,
  keyed `AddAgentProcesses(ProcessExecutorKey, …)`; `IProcessRunner.RunAsync`
  re-implemented as an adapter; tests for fragment-boundary streaming, loss
  markers, permit exhaustion.

### WS5-C13b: Terminate, exit status, audit, authority reevaluation

- Depends on: C13a. Risk: ADDITIVE. Size: M.
- Deliverables: `TerminateAsync` (grace → SIGTERM → SIGKILL, termination
  certainty), exited vs signalled, `ISecurityAuditDispatcher`, reevaluation
  through `ISecurityAuthoritySelector` when executable or sandbox facts differ
  (`process-execution.md:271-274`), sandbox scope intersection; platform sandbox
  tests skip explicitly when unavailable.

### WS5-C14: Scripted executor and process conformance suite

- Depends on: C12, C13. Risk: ADDITIVE. Size: M.
- Deliverables: `ScriptedProcessExecutor`, `ScriptedExecutableResolver`, keyed
  `AddScriptedProcesses`; `IProcessExecutorConformanceFixture`,
  `ProcessExecutorConformanceTests`. Snapshot: Processes.Scripted.

### WS5-C15: `CommandTool` onto `IProcessExecutor`

- Depends on: C13, C14. Risk: DENSE-MODIFY `CommandTool.cs:57-64` (419 lines).
  Size: M.
- Deliverables: handle-based run with bounded tail and artifact spill; fakes
  updated. Snapshot: Tools.Command.

### WS5-C16: `WebFetchTool` onto split bounds

- Depends on: C9. Risk: DENSE-MODIFY `WebFetchTool.cs:44-51`. Size: S–M.

### WS5-C17: Documentation

- Depends on: all. Size: S.
- Deliverables: `guides/file-system.md`, the three architecture documents'
  acceptance lists, host-access skill, READMEs.

## Totals

S 3, M 14, L 1. Confidence medium: contract shapes are well specified but about
fifteen referenced value types have no bodies, and two name collisions add
rename steps.
