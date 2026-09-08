# Artifact and content storage

**Role:** Store and resolve bounded durable content referenced by messages,
tools, media, memory sources, checkpoints, and evaluation reports.

The behavioral contract is defined by
[artifact and content storage](../concepts/artifact-and-content-storage.md).
Artifacts are not conversation history, durable memory, or arbitrary filesystem
paths.

## Package model

AgentKit.Abstractions owns artifact identities, references, metadata, requests,
results, backend descriptors, and narrow store contracts. AgentKit.Artifacts
supplies the catalog, selector, coordinator, integrity validation, retention,
and orphan reconciliation.

Backends are leaves: AgentKit.Artifacts.InMemory is deterministic for tests;
AgentKit.Artifacts.Sqlite is the durable local adapter;
AgentKit.Artifacts.FileSystem uses protected AgentKit.FileSystem contracts;
cloud packages use protected network contracts and their vendor SDKs. Consumers
never depend on a concrete backend.

## Implemented baseline

`AgentKit.Artifacts` currently provides a bounded two-phase coordinator for
complete immutable content. Prepare validates the declared byte length and
SHA-256 fingerprint before requesting authority; finalize publishes the portable
reference atomically; abort removes staging only. Committed reads return an
owned asynchronously disposable stream, and deletion is idempotent and fails
before authorization when the captured reference carries a legal hold.

`AgentKit.Artifacts.InMemory` is the first backend leaf. Every prepare,
finalize, abort, read, and delete consumes an exact single-use artifact grant
before state access. State is tenant-partitioned, unpublished staging is never
readable, committed bytes are returned by copy, replay keys cannot silently
change prepare content or policy, and consumed grants are not retained with
stored content. `AddAgentArtifacts` intentionally does not fabricate a backend;
applications select one explicitly, with `AddInMemoryArtifactStore` available
for deterministic compositions and tests. The package also registers
`ArtifactProcessOutputSink`, which converts complete bounded process
stdout/stderr captures into immutable session- or run-owned artifacts. Process
execution remains independent of the artifact runtime and depends only on the
optional `IProcessOutputArtifactSink` abstraction.

The SQLite leaf must preserve staging visibility, immutable committed bytes,
tenant partitioning, replay identity, reference publication, and deletion state
transactionally across close and reopen. It runs the same artifact-store
conformance suite as the in-memory leaf. It advertises durable local content,
not distributed replication, cloud-object retention, or an atomic transaction
with session history. Payload and database targets remain explicit host
configuration; no package invents a database path.

## Normative minimal contract shape

```csharp
namespace AgentKit;

public readonly record struct ArtifactId(Guid Value);
public readonly record struct ArtifactVersion(string Value);
public readonly record struct ArtifactPreparationId(Guid Value);
public readonly record struct ArtifactDirectoryId(string Value);
public readonly record struct ArtifactProfileKey(string Value);
public readonly record struct ArtifactProfileVersion(long Value);
public readonly record struct ArtifactOwnerId(string Value);
public readonly record struct ArtifactRetentionPolicyKey(string Value);
public readonly record struct ExternalArtifactResourceId(string Value);
public readonly record struct ArtifactEventSinkId(string Value);

public enum ArtifactOwnershipKind
{
    Run,
    Session,
    Memory,
    Evaluation,
    External
}

public enum ArtifactMutability
{
    Immutable,
    AppendOnly,
    ExternallyManaged
}

public sealed record ArtifactRetention(
    ArtifactRetentionPolicyKey Policy,
    DateTimeOffset? ExpiresAt,
    bool LegalHold);

public sealed record ArtifactIntegrity(
    ContentHash ContentHash,
    DateTimeOffset VerifiedAt);

public sealed record ExternalArtifactOwnership(
    ExternalArtifactResourceId ResourceId,
    Uri CanonicalUri,
    bool AgentKitMayDelete);

public sealed record ArtifactMetadata(
    ArtifactOwnerId OwnerId,
    string MediaType,
    long DeclaredLength,
    ContentHash? DeclaredContentHash,
    DataClassification Classification,
    ArtifactOwnershipKind Ownership,
    ArtifactMutability Mutability,
    ArtifactRetention Retention,
    ExternalArtifactOwnership? ExternalOwnership);

public sealed record ArtifactReference(
    ArtifactId Id,
    ArtifactVersion Version,
    ArtifactDirectoryId DirectoryId,
    ArtifactProfileKey ProfileKey,
    ArtifactProfileVersion ProfileVersion,
    TenantId TenantId,
    ArtifactOwnerId OwnerId,
    PrincipalId CreatedBy,
    string MediaType,
    long Length,
    ArtifactIntegrity Integrity,
    DataClassification Classification,
    ArtifactOwnershipKind Ownership,
    ArtifactMutability Mutability,
    ArtifactRetention Retention,
    ExternalArtifactOwnership? ExternalOwnership,
    DateTimeOffset CreatedAt);

public sealed record ArtifactPrepareRequest(
    AgentId AgentId,
    SessionId? SessionId,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization,
    ArtifactDirectoryId DirectoryId,
    ArtifactMetadata Metadata,
    Stream Content,
    IdempotencyKey IdempotencyKey);

public abstract record ArtifactPrepareResult;

public sealed record ArtifactPrepared(
    ArtifactPreparationId PreparationId,
    ArtifactId ArtifactId,
    ArtifactVersion Version,
    DateTimeOffset ExpiresAt) : ArtifactPrepareResult;

public sealed record ArtifactPrepareRejected(ArtifactFailure Failure)
    : ArtifactPrepareResult;

public sealed record ArtifactFinalizeRequest(
    ArtifactPreparationId PreparationId,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization,
    IdempotencyKey IdempotencyKey);

public abstract record ArtifactFinalizeResult;

public sealed record ArtifactFinalized(ArtifactReference Reference)
    : ArtifactFinalizeResult;

public sealed record ArtifactFinalizeRejected(ArtifactFailure Failure)
    : ArtifactFinalizeResult;

public sealed record ArtifactAbortRequest(
    ArtifactPreparationId PreparationId,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization,
    ArtifactAbortReason Reason,
    IdempotencyKey IdempotencyKey);

public abstract record ArtifactAbortResult;

public sealed record ArtifactAborted(bool AlreadyAbsent) : ArtifactAbortResult;

public sealed record ArtifactAbortRejected(ArtifactFailure Failure)
    : ArtifactAbortResult;

public interface IArtifactStore
{
    Task<ArtifactStorePrepareResult> PrepareAsync(
        ArtifactStorePrepareRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(
        ArtifactStoreFinalizeRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactStoreAbortResult> AbortAsync(
        ArtifactStoreAbortRequest request,
        CancellationToken cancellationToken = default);

    Task<ArtifactStoreReadResult> ReadAsync(
        ArtifactStoreReadRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactStoreDeleteResult> DeleteAsync(
        ArtifactStoreDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public interface IArtifactCoordinator
{
    Task<ArtifactPrepareResult> PrepareAsync(
        ArtifactPrepareRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactFinalizeResult> FinalizeAsync(
        ArtifactFinalizeRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactAbortResult> AbortAsync(
        ArtifactAbortRequest request,
        CancellationToken cancellationToken = default);

    Task<ArtifactReadResult> ReadAsync(
        ArtifactReadRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactDeleteResult> DeleteAsync(
        ArtifactDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public interface IArtifactEventSink
{
    ValueTask PublishAsync(
        ArtifactEvent artifactEvent,
        CancellationToken cancellationToken = default);
}

public interface IArtifactEventDispatcher
{
    ValueTask PublishAsync(
        ComponentKey<IArtifactCoordinator> coordinatorKey,
        ArtifactEvent artifactEvent,
        CancellationToken cancellationToken = default);
}
```

The coordinator owns prepare/finalize/abort and reconciliation semantics.
`ArtifactPrepared` is a staging receipt, not a readable reference; only a
successful finalize returns an `ArtifactReference`. A returned read handle owns
its stream and is asynchronously disposable. Backend key, location, signed URL,
open stream, or vendor file ID is never portable artifact identity. The
`ExternalArtifactOwnership.CanonicalUri` is stable and unsigned; access still
passes through the selected resolver and security boundary.

## First-party implementation and DI

```csharp
namespace AgentKit.Artifacts;

public readonly record struct ArtifactBackendKey(string Value);

internal interface IArtifactStoreSelector
{
    ValueTask<ArtifactStoreSelectionResult> SelectAsync(
        ArtifactProfileSnapshot profile,
        ArtifactDirectoryId directoryId,
        CancellationToken cancellationToken);
}

internal sealed record ArtifactProfileSnapshot(
    ArtifactProfileKey Key,
    ArtifactProfileVersion Version,
    ArtifactDirectoryId DefaultDirectory,
    ImmutableDictionary<ArtifactDirectoryId, ArtifactBackendKey> Routes,
    ArtifactRetention DefaultRetention,
    ImmutableHashSet<ArtifactMutability> AllowedMutability,
    bool AllowExternalOwnership);

internal sealed class ArtifactCoordinator(
    ComponentKey<IArtifactCoordinator> key,
    ArtifactProfileSnapshot profile,
    IArtifactStoreSelector stores,
    IArtifactIntegrityValidator integrity,
    IArtifactRetentionPolicy retention,
    ISecurityAuthoritySelector securityAuthorities,
    IArtifactEventDispatcher events,
    TimeProvider timeProvider,
    IIdentifierGenerator<ArtifactId> artifactIds,
    AgentArtifactOptionsSnapshot options) : IArtifactCoordinator
{
}

internal sealed record AgentArtifactOptionsSnapshot(
    long MaximumArtifactBytes,
    int CopyBufferBytes,
    TimeSpan OrphanRetention,
    bool RequireDeclaredContentHash);

public sealed class AgentArtifactOptions
{
    public long MaximumArtifactBytes { get; set; } = 16 * 1_024 * 1_024;
    public int CopyBufferBytes { get; set; } = 64 * 1_024;
    public TimeSpan OrphanRetention { get; set; } = TimeSpan.FromHours(24);
    public bool RequireDeclaredContentHash { get; set; } = true;
}

public sealed class ArtifactProfileOptions
{
    public ArtifactProfileVersion Version { get; set; } = new(1);
    public ArtifactDirectoryId? DefaultDirectory { get; set; }
    public Dictionary<ArtifactDirectoryId, ArtifactBackendKey> Routes { get; } = [];
    public ArtifactRetention? DefaultRetention { get; set; }
    public HashSet<ArtifactMutability> AllowedMutability { get; } =
        [ArtifactMutability.Immutable];
    public bool AllowExternalOwnership { get; set; }
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentArtifacts(
            ComponentKey<IArtifactCoordinator> key,
            ArtifactProfileKey profileKey,
            Action<AgentArtifactOptions>? configure = null) =>
            ArtifactRegistration.AddDefault(
                services,
                key,
                profileKey,
                configure);

        public IServiceCollection AddArtifactProfile(
            ArtifactProfileKey key,
            Action<ArtifactProfileOptions> configure) =>
            ArtifactRegistration.AddProfile(services, key, configure);

        public IServiceCollection ReplaceArtifactProfile(
            ArtifactProfileKey key,
            Action<ArtifactProfileOptions> configure) =>
            ArtifactRegistration.ReplaceProfile(services, key, configure);

        public IServiceCollection AddArtifactStore<TStore>(
            ArtifactBackendKey key)
            where TStore : class, IArtifactStore =>
            ArtifactRegistration.AddStore<TStore>(services, key);

        public IServiceCollection ReplaceArtifactStore<TStore>(
            ArtifactBackendKey key)
            where TStore : class, IArtifactStore =>
            ArtifactRegistration.ReplaceStore<TStore>(services, key);

        public IServiceCollection AddArtifactEventSink<TSink>(
            ComponentKey<IArtifactCoordinator> coordinatorKey,
            ArtifactEventSinkRegistration registration)
            where TSink : class, IArtifactEventSink =>
            ArtifactRegistration.AddEventSink<TSink>(
                services,
                coordinatorKey,
                registration);

        public IServiceCollection ReplaceArtifactCoordinator<TCoordinator>(
            ComponentKey<IArtifactCoordinator> key)
            where TCoordinator : class, IArtifactCoordinator =>
            ArtifactRegistration.ReplaceCoordinator<TCoordinator>(services, key);
    }
}
```

The catalog is singleton over immutable descriptors and profile snapshots.
Coordinators are keyed and bound to one logical artifact profile; the internal
selector maps a logical directory to a configured backend. Writes and reads are
operation-owned. Backends may be singleton only when thread-safe and free of
request state. Every real backend revalidates and consumes the grant immediately
before the effect.

`AddAgentArtifacts` is idempotent for one coordinator key and captures validated
mutable binding options into an immutable `AgentArtifactOptionsSnapshot` for
that keyed coordinator. Profile binding is captured separately as an immutable
`ArtifactProfileSnapshot`; it is a logical policy/directory map, and its backend
route never appears in `ArtifactPrepareRequest` or `ArtifactReference`.
`AddAgentArtifacts` registers no implicit profile or store. Profiles, stores,
and coordinators reject conflicting key reuse unless their matching replacement
method is used; event sinks are additive. Sink dispatch is deterministic and
lifetime-aware, so a coordinator never captures a scoped sink. The finite size,
copy-buffer, orphan-retention, and integrity defaults are safe mechanics, not
persistence, paths, credentials, grants, or authority. The caller owns the input
stream until `PrepareAsync` completes; the store does not retain or dispose it
unless a concrete adapter explicitly documents transferred ownership.

## Dependency direction and cycle prevention

AgentKit.Artifacts depends on neutral contracts and shared diagnostic
infrastructure. Tools, messages, memory, sessions, compaction, and evaluation
exchange `ArtifactReference` values or use `IArtifactCoordinator`; the artifact
runtime never calls those components. Backend leaves may depend on FileSystem or
Network, which never depend on Artifacts.

Artifact creation and durable reference commitment are coordinated by the caller
through prepare/finalize or an outbox. The artifact coordinator does not call
`ISessionCoordinator` to append its own reference. That rule prevents the cycle
session → artifact → session and keeps transaction ownership honest.

## Reference commitment and garbage collection

Prepare, finalize, abort, and delete are distinct protected operations with
separate grants and idempotency identities. Finalize is an atomic transition of
one preparation: it returns the same immutable reference on equivalent retry;
abort racing finalize has one winner and cannot delete a finalized version as
though it were unfinished staging. Cancellation or a lost acknowledgement is
reconciled by preparation identity before retrying or aborting.

A finalized reference is resolvable to an authorized holder. Artifact storage
cannot infer whether another store has committed a reference to it. The caller
therefore records an idempotent reference-commit intent before finalization,
finalizes the bytes, then commits the returned reference and intent completion
in its owning session/tool/memory store. A crash leaves an explicit pending
intent for reconciliation. Only complete finalized content enters an ordinary
message; a pending preparation is never exposed as a readable reference.

Finalization retention must cover the entire pending reference-commit window. A
store offering automatic orphan collection must support a durable pin or an
equivalent retention fence covering that intent. Renewal, reference commitment,
and collection race through conditional versioned transitions. Expiry alone
cannot prove that a reference commit failed: collection first fences out a late
commit and establishes its terminal disposition through the caller-owned
reconciliation path. If the stores cannot establish that evidence, collection
retains the object and reports pending reconciliation. Conservative retention is
the default; a time-based orphan sweep is not safe reference accounting.

The artifact runtime never scans or calls session/tool coordinators to prove
reachability. The caller's outbox/reconciler supplies evidence through artifact
contracts. Deduplication shares bytes only under retention and isolation rules;
deleting one logical reference cannot collect content still pinned by another.

## Validation and unsupported behavior

Composition validates store keys, tenant partitioning, maximum sizes, media and
classification support, integrity algorithm, retention policy, security
authority, event delivery, lifetimes, and reconciliation ownership. Unsupported
media, overflow, hash mismatch, stale version, missing authority, partial
upload, backend failure, and retention conflict are typed outcomes.

Registration without an explicit artifact leaf fails when a profile selects
artifact storage. Common in-memory and SQLite behavior is verified by one
reusable suite; adapter-specific tests prove restart durability, transaction
boundaries, streaming limits, and only the capabilities each descriptor claims.

## Related concept specifications

- [Artifact and content storage](../concepts/artifact-and-content-storage.md)
- [File-system access and bounds](../concepts/file-system-access-and-bounds.md)
- [Network access and egress](../concepts/network-access-and-egress.md)

## Related architecture

- [Messages and history](messages-and-history.md)
- [Tools](tools.md)
- [Memory and retrieval](memory-and-retrieval.md)
- [File system](file-system.md)
- [Network access](network.md)
- [Testing and evaluation](testing-and-evaluation.md)
