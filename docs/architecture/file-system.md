# File system

**Role:** Make file and directory access replaceable, policy-aware, and
deterministic in tests.

Framework and tool code never calls operating-system file APIs directly. It
depends on provider-neutral file-system contracts from AgentKit.Abstractions.
Applications may replace the implementation globally or within an explicitly
scoped engine composition.

## Contract boundaries

These narrow contracts follow the
[dependency-boundary rules](../concepts/architecture-and-dependency-boundaries.md)
instead of leaking a concrete filesystem into tools or runtime packages.

The abstraction covers the operations the framework actually needs, including
file reads and writes, directory enumeration, metadata, path normalization,
temporary storage, and optional change observation. Consumers depend on narrow
capability contracts rather than receiving authority to every filesystem
operation through one oversized service.

Paths are logical values with an explicit root and comparison policy. The
abstraction defines normalization, symbolic-link behavior, case sensitivity,
atomic replacement, stream ownership, cancellation, and failure categories. Raw
host paths do not become portable identifiers by accident.

## Implementations

AgentKit.FileSystem is the operating-system implementation. It translates
neutral operations to System.IO while enforcing configured roots, bounds, and
ownership. AgentKit.FileSystem.InMemory is a deterministic implementation for
tests, examples, and applications that need an isolated virtual filesystem.

Applications can provide other implementations, such as a workspace sandbox,
remote object store, browser-backed filesystem, or read-only projection, without
changing tools or the loop.

## Tools and security

File-aware tools still follow the
[normal tool-call lifecycle](../concepts/tool-call-lifecycle.md), while the
filesystem re-enforces the resulting bounded grant at the lower-level effect.

AgentKit.Tools.Read, AgentKit.Tools.Write, AgentKit.Tools.Skill, and any other
file-aware package depend only on the file-system abstractions. They do not
reference AgentKit.FileSystem.

The file system does not grant tool authority. A caller first sends the
canonical operation through the security authority, which determines approved
roots, resources, effects, inputs, lifetime, and uses. The implementation
validates and consumes the resulting bounded grant immediately before the
effect, then fails closed when it cannot represent or audit that scope. A
sandbox may further restrict the operation but never replaces authorization.

High-level authorization cannot be replayed for a changed normalized path,
symbolic-link target, operation, content fingerprint, principal, or audience.
Mutation after evaluation requires a fresh security request.

## Time and testing

The in-memory implementation uses the injected TimeProvider for created,
modified, expiry, and watcher timestamps. The operating-system implementation
reports filesystem metadata as external truth while using TimeProvider for
framework-owned deadlines, polling, retries, and event times.

Shared conformance suites run against both implementations. They cover path
normalization, bounds against actual stream bytes, every missing/existing write
disposition, target-state races, replacement and append atomicity, concurrent
non-interleaving append, text encoding/BOM/newline behavior, empty payloads,
content/final fingerprints, enumeration order, stream disposal, cancellation,
metadata, root escape attempts, symbolic links, and concurrent access. Tests do
not touch the developer's real workspace unless they are explicitly marked
integration tests and use an isolated temporary root.

## Contract shape

File access is split by capability. A component that only reads cannot receive a
writer merely because both happen to use System.IO in one implementation.
Representative neutral contracts are:

```csharp
namespace AgentKit;

public readonly record struct FileOperationId(Guid Value);
public readonly record struct FileRootId(string Value);
public readonly record struct FileSystemProfileKey(string Value);
public readonly record struct FileSystemProfileVersion(long Value);

public sealed record FileTarget(
    FileRootId RootId,
    NormalizedRelativePath Path);

public sealed record FileReadRequest(
    FileOperationId Id,
    OperationId CausalOperationId,
    AgentId AgentId,
    RunId? RunId,
    FileTarget Target,
    FileReadBounds Bounds);

public sealed record AuthorizedFileRead(
    FileReadRequest Request,
    ResolvedFileTarget ResolvedTarget,
    SecurityGrant Grant);

public interface IFilePathNormalizer
{
    FilePathNormalizationResult Normalize(
        FilePathInput input,
        FilePathPolicy policy);
}

public interface IFileReader
{
    ValueTask<IFileReadHandle> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken);
}

public interface IFileWriter
{
    ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
        CancellationToken cancellationToken);
}

public interface IDirectoryReader
{
    IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken);
}

public interface IFileMetadataReader
{
    ValueTask<FileMetadataResult> GetMetadataAsync(
        AuthorizedFileMetadataRead operation,
        CancellationToken cancellationToken);
}

public interface IFileChangeSource
{
    IAsyncEnumerable<FileChange> WatchAsync(
        AuthorizedFileWatch operation,
        CancellationToken cancellationToken);
}

public interface ITemporaryFileStore
{
    ValueTask<ITemporaryFileLease> CreateAsync(
        AuthorizedTemporaryFileCreation operation,
        CancellationToken cancellationToken);
}

public interface IFileSystemSelector
{
    ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken);
}

public interface IFileReadHandle : IAsyncDisposable
{
    FileMetadata Metadata { get; }
    Stream Content { get; }
}

public enum FileWriteDisposition
{
    CreateOnly,
    ReplaceExisting,
    CreateOrReplace,
    Append
}

public enum FileWriteOutcomeKind
{
    Created,
    Replaced,
    Appended
}

public sealed record FileWriteSuccess(
    FileWriteOutcomeKind Outcome,
    long PayloadBytes,
    long PreviousBytes,
    long FinalBytes,
    ContentHash PayloadFingerprint,
    ContentHash FinalFingerprint) : FileWriteResult;
```

`FileOperationId`, `FileRootId`, and the shared causal IDs are validated
readonly values with canonical serialization. Callers create new operation IDs
through `IIdentifierGenerator<FileOperationId>`. `FileTarget` cannot contain an
ambient current directory or an unnormalized host path. `ResolvedFileTarget`
includes the root, normalized host target, symbolic-link resolution evidence,
comparison policy, and a fingerprint used by the security request. `FileRootId`
and `FileSystemProfileKey` are validated non-empty semantic keys, not generated
operation IDs. `IFilePathNormalizer` performs only deterministic lexical work;
the effecting implementation re-resolves external link and mount facts under the
grant.

`AuthorizedFileWrite` binds the canonical target, explicit disposition, expected
target state or fingerprint, declared content length and fingerprint, atomicity
mode, effect class, and grant. Declared stream metadata is advisory: the
implementation bounds and fingerprints the bytes it actually consumes and
compares them with the declaration before making the result visible. A length or
fingerprint mismatch is a typed invalid-input or conflict result, never an
excuse to commit different bytes.

`FileWriteResult` and metadata results discriminate success, not found,
conflict, denial, limit, cancellation, unsupported capability, and typed I/O
failure. A successful write says whether it `Created`, `Replaced`, or
`Appended`; reports payload, previous, and final byte counts; and carries both
the consumed-payload fingerprint and the fingerprint of the committed final
file. Expected host outcomes are not encoded as exception strings.

The caller owns the returned read handle and must dispose it. Cancelling
`OpenReadAsync` prevents a new handle from being returned; cancelling later does
not dispose an already returned handle. Enumeration and watching are real
streams: cancellation stops observation and releases the implementation-owned
enumerator resources.

## Read bounds and text interpretation

`FileReadBounds` limits bytes actually obtained from the backing store, not only
a metadata length observed before opening the file. Every returned stream is a
bounded stream: the implementation stops before exposing bytes beyond the
authorized limit and returns typed limit or truncation evidence when the target
grows, metadata is stale, decompression expands content, or a remote source
otherwise exceeds the bound. Reading the whole file and slicing it afterward is
not an implementation of bounded access.

The host boundary owns byte access, target snapshot/version evidence, and
content integrity. Model-facing line, page, or character windows belong to the
consuming tool or context feature. In particular, `AgentKit.Tools.Read` scans a
bounded stream incrementally and does not move its line-offset contract into the
file-system abstraction. A continuation binds the resolved target, snapshot or
version evidence, decoding profile, next byte/line position, and prior window;
if the target changes, continuing returns conflict instead of joining content
from two file versions.

Text access always selects a declared decoding profile. The profile states the
encoding, malformed-input behavior, byte-order-mark interpretation, and newline
projection. It never depends on a process locale or an ambient default encoding.
A leading BOM is encoding evidence rather than ordinary text unless the selected
profile explicitly says otherwise.

## Write dispositions and atomicity

Every write selects one disposition explicitly; there is no destructive default.
The target-state test and commit form one atomic operation with respect to
competing writers:

| Disposition       | Missing target                    | Existing regular file                    |
| ----------------- | --------------------------------- | ---------------------------------------- |
| `CreateOnly`      | Create and report `Created`       | Return conflict without mutation         |
| `ReplaceExisting` | Return not found without mutation | Atomically replace and report `Replaced` |
| `CreateOrReplace` | Create and report `Created`       | Atomically replace and report `Replaced` |
| `Append`          | Return not found without mutation | Append once and report `Appended`        |

`CreateOrReplace` is available only when a caller deliberately requests and is
authorized for both outcomes. Neither a tool schema, registration helper, nor a
host adapter may silently choose it when the disposition is absent. A target
state or fingerprint captured for authorization is a commit precondition; a
racing change produces conflict rather than falling back to another disposition.
An implementation that cannot provide the requested atomic state test and commit
reports unsupported before mutation.

Creating missing parent directories is a separate protected effect. The normal
write contract returns a typed parent-not-found result. A caller that wants
parents created must declare and authorize each directory creation, or use an
explicit compound operation whose directory and file effects remain separately
bound and audited. A file writer never calls an implicit recursive
create-directory operation as a convenience.

Append policy bounds the committed total size, not merely the appended payload.
Under the same concurrency boundary that commits the append, the implementation
checks the actual current size, uses overflow-safe arithmetic, verifies any
expected target fingerprint, and ensures the payload is appended exactly once
without interleaving bytes from another append. The selected capability states
whether cancellation or host failure can expose a partial append. A policy that
requires atomic append rejects an implementation that cannot provide it; it
never reports a clean failure when the side effect is uncertain.

For streamed writes, the implementation consumes at most the authorized bound
through a bounded reader and computes the payload fingerprint over the bytes
actually consumed. Metadata such as a declared length, seekable stream length,
or model-provided size cannot replace this enforcement. Atomic create and
replace stage validated content outside the visible target and publish it in one
commit. Limit, cancellation, encoding, or fingerprint failure before that commit
leaves the prior target unchanged.

Text writes additionally declare encoding, BOM, and newline behavior. The
first-party portable profile is UTF-8 without a BOM and preserves the supplied
newline characters and final-newline state; another profile may deliberately
normalize newlines or emit a BOM. Append never inserts a BOM in the middle of a
file and must reject an incompatible existing text format unless an explicit
conversion operation was authorized. Byte counts, limits, and fingerprints are
computed after encoding, BOM, and newline policy have produced the exact bytes.
Empty and whitespace-only text are valid payloads; missing or null content is
not. No layer silently trims content, calls a line-oriented writer that adds a
terminator, or treats an empty string as an absent argument.

## First-party classes and service dependencies

| Package class                                                                                                                                              | Role and injected dependencies                                                                                                                             |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OperatingSystemFileReader`, `OperatingSystemFileWriter`, `OperatingSystemDirectoryReader`, and `OperatingSystemFileMetadataReader` in AgentKit.FileSystem | System.IO adaptation, configured roots/bounds, `ISecurityGrantStore` revalidation/consumption, required audit, and `TimeProvider` for framework deadlines  |
| `OperatingSystemFileChangeSource`                                                                                                                          | Optional watcher/polling capability with bounded buffers, the same root enforcement, and explicit loss markers                                             |
| `InMemoryFileSystem` in AgentKit.FileSystem.InMemory                                                                                                       | Implements the selected narrow contracts over isolated state using injected `TimeProvider`, deterministic ordering, and configurable case/symlink behavior |
| Custom sandbox, object-store, browser, or read-only implementations                                                                                        | Implement only the capability contracts they actually support and declare those capabilities before selection                                              |

Each operating-system capability is independently replaceable. A representative
implementation has no dependency on a tool or engine facade:

```csharp
namespace AgentKit.FileSystem;

internal sealed record OperatingSystemFileSystemOptionsSnapshot(
    FileSystemProfileKey ProfileKey,
    FileSystemProfileVersion ProfileVersion,
    ImmutableArray<FileRootRegistration> Roots,
    FilePathPolicy PathPolicy,
    FileSystemBounds Bounds,
    FileWritePolicy WritePolicy,
    FileWatchPolicy WatchPolicy);

internal sealed class OperatingSystemFileReader(
    IFilePathNormalizer paths,
    ISecurityGrantStore grants,
    ISecurityAuditDispatcher audit,
    TimeProvider timeProvider,
    OperatingSystemFileSystemOptionsSnapshot options)
    : IFileReader
{
    public ValueTask<IFileReadHandle> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken) =>
        OperatingSystemFileOperations.OpenReadAsync(
            operation,
            paths,
            grants,
            audit,
            timeProvider,
            options,
            cancellationToken);
}
```

Writer, directory, metadata, watch, and temporary-store classes use the same
boundary dependencies only where their operation requires them. Direct interface
implementations remain supported; there is no mandatory filesystem base class.

The operating-system implementation depends on neutral security and audit
contracts, not on tools or `AgentEngine`. Tool packages depend only on the file
contracts. The implementation canonicalizes again at the effect boundary,
matches the exact grant audience, operation, principal, target, link evidence,
and input fingerprint, atomically consumes a use, and emits audit before acting.
If any check cannot be performed, no operating-system call occurs.

## Lifetime, concurrency, and ownership

Stateless operating-system capability services are thread-safe singletons. Each
open handle is operation-owned and never stored on the singleton. The in-memory
implementation may also be singleton within one engine composition, but its
state is isolated to that registration and concurrency-controlled; tests create
a fresh provider or keyed instance when they need a fresh volume.

Concurrent reads are permitted. Write/write and write/read behavior follows the
declared consistency and atomic-replacement policy and is covered by conformance
tests. A service never relies on process current-directory state. Temporary
files, watcher handles, and streams have explicit owners and are removed or
disposed on completion, cancellation, or provider shutdown according to options.
The container disposes singleton implementations; consumers dispose operation
handles.

## Dependency-injection registration

```csharp
namespace AgentKit.FileSystem;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOperatingSystemFileSystem(
            FileSystemProfileKey key,
            Action<OperatingSystemFileSystemOptions> configure) =>
            OperatingSystemFileSystemRegistration.Add(
                services,
                key,
                configure);
    }
}
```

```csharp
namespace AgentKit.FileSystem.InMemory;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInMemoryFileSystem(
            FileSystemProfileKey key,
            Action<InMemoryFileSystemOptions>? configure = null) =>
            InMemoryFileSystemRegistration.Add(services, key, configure);
    }
}
```

```csharp
namespace AgentKit;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddFileSystem<TImplementation>(
            FileSystemProfileKey key,
            FileSystemCapabilities capabilities)
            where TImplementation : class =>
            FileSystemServiceRegistration.Add<TImplementation>(
                services,
                key,
                capabilities);
    }
}
```

The two first-party package methods `TryAddKeyed` one implementation for each
narrow capability they declare under a `FileSystemProfileKey`. Those contracts
are singular per key and explicitly replaceable with ordinary DI; installing the
in-memory package does not silently replace a real implementation under an
existing key. Repeated identical registration is idempotent.

Each profile key owns named `OperatingSystemFileSystemOptions`. Registration
validates and copies them into a package-owned immutable
`OperatingSystemFileSystemOptionsSnapshot` containing the profile key and
`FileSystemProfileVersion`, then supplies that snapshot to every narrow service
under the same key. Effecting services never inject unkeyed `IOptions<T>`. The
default registration captures options for the provider lifetime rather than
monitoring mutable values; a changed profile requires a validated new provider
or explicitly versioned catalog publication, and open handles retain their
original snapshot. Each keyed capability factory closes over the exact
`FileSystemProfileKey`/snapshot pair; it cannot be activated through an unkeyed
constructor path or paired with options belonging to another virtual file
system.

Applications that intentionally expose several virtual filesystems register
complete capability sets under unique keys and select them through an injected,
singular `IFileSystemSelector`. Key collisions and selection of a missing
capability fail validation. There is no additive chain of file executors:
policy, audit, hooks, and observation are additive through their own typed
contracts, not by letting several implementations race to perform one effect.

## Build validation and unsupported behavior

File access is optional until a registered tool, MCP transport, context source,
store, or other component declares that it needs it. Composition then validates
the selected root configuration, path comparison and symlink policy, required
capability set, security authority and grant store, audit delivery, bounds,
service scopes, keyed options/profile-version agreement, and key selection.
Writable configuration requires an effective writer, an explicit disposition
policy, actual-stream bounds, text profiles when text is selected, and the
required create/replace/append atomicity. Parent creation requires its own
directory capability and policy. Watch configuration requires an implementation
that advertises watch semantics and overflow behavior.

Read-only, no-watch, no-symlink, or non-atomic implementations declare those
limits in `FileSystemCapabilities`. Selection rejects an incompatible operation
before an effect. It returns a typed unsupported result when a dynamic request
asks for an undeclared optional capability. It never falls back to raw
System.IO, widens a root, follows an unapproved link, or throws
`NotSupportedException` as normal discovery. Missing, expired, consumed,
mismatched, or unauditable grants fail closed before host access.

## Related architecture

- [Project structure](project-structure.md)
- [Tools](tools.md)
- [Permissions and human control](permissions-and-human-control.md)
- [Coding workspaces and worktrees](../concepts/coding-workspaces-and-worktrees.md)
- [Workspace mutations and code editing](../concepts/workspace-mutations-and-code-editing.md)
- [Workspace snapshots and reversion](../concepts/workspace-snapshots-and-reversion.md)
