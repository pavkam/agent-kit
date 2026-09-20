# Sessions

**Role:** Provide the durable coordination boundary for related runs.

A [session](../concepts/sessions-persistence-and-branching.md) owns an immutable
entry tree, named branches, execution lanes, admitted input, current
orchestration state, usage evidence, configuration/context transitions, and
optimistic concurrency state. It is not an in-memory agent instance. Even a
standalone run uses an ephemeral session with the same ordering and correlation
semantics.

AgentKit.Session contains session coordination, per-lane operation ownership,
the session mutation line, branching, and store usage. Session contracts and
durable values live in AgentKit.Abstractions. AddAgentSession registers the
coordinator but never chooses a storage medium.

`AgentEngine` hosts many agents and sessions concurrently. A session is always
addressed by typed `AgentId` and `SessionId`, while every mutating operation has
an `OperationId` and a typed before-, during-, or after-run correlation. Session
coordination has no ambient current agent, and a bare `SessionId` is never
sufficient to cross an isolation boundary.

## Normative minimal contract shape

The following C# 14 shapes are normative and minimal rather than exhaustive.
Every named type lives in its own file in AgentKit.Abstractions. Shared
`AgentId`, `SessionId`, `RunId`, `OperationId`,
`IIdentifierGenerator<TIdentifier>`, and `IdempotencyKey` contracts are reused.

```csharp
namespace AgentKit;

public readonly record struct SessionEntryId(Guid Value);

public readonly record struct BranchId(Guid Value);

public readonly record struct ExecutionLaneId(Guid Value);

public readonly record struct SessionSnapshotId(Guid Value);

public readonly record struct SessionLeaseId(Guid Value);

public readonly record struct SessionStoreKey(string Value);

public readonly record struct SessionProfileKey(string Value);

public readonly record struct SessionProfileVersion(long Value);

public readonly record struct SessionRetentionProfileKey(string Value);

public readonly record struct SessionVersion(long Value);

public readonly record struct SessionDirectoryRevision(long Value);

public readonly record struct SessionSequence(long Value);

public readonly record struct SessionAddress(
    AgentId AgentId,
    SessionId SessionId);

public sealed record SessionProfileReference(
    SessionProfileKey Key,
    SessionProfileVersion Version);
```

Session, entry, branch, snapshot, lease, run, and operation identities are
allocated through injected generators. Timestamps use `TimeProvider`.

```csharp
namespace AgentKit;

public sealed record SessionDescriptor(
    SessionAddress Address,
    ConversationId? ConversationId,
    TenantId TenantId,
    PrincipalId OwnerId,
    SessionStoreKey StoreKey,
    ImmutableArray<SessionLaneDescriptor> Lanes,
    SessionVersion Version,
    SessionLifecycleState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SchemaVersion SchemaVersion,
    ExtensionData Extensions);

public sealed record SessionLaneDescriptor(
    ExecutionLaneId Id,
    BranchId BranchId,
    OperationId? OpenOperationId,
    RunId? OpenRunId);

public abstract record SessionEntry(
    SessionEntryId Id,
    SessionAddress Address,
    OperationCorrelation Correlation,
    BranchId BranchId,
    SessionSequence Sequence,
    SessionEntryId? CausalParentId,
    DateTimeOffset RecordedAt,
    SchemaVersion SchemaVersion);

public sealed record SessionOperationContext(
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId? ExecutionLaneId,
    OperationCorrelation Correlation,
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    HookDispatchContext? Hooks);

public sealed record SessionProfileSnapshot(
    SessionProfileReference Reference,
    ComponentKey<ISessionCoordinator> CoordinatorKey,
    ComponentKey<ISessionRunCoordinator> RunCoordinatorKey,
    SessionStoreKey DefaultStoreKey,
    SessionRetentionProfileKey RetentionProfile,
    SessionBusyBehavior BusyBehavior,
    int MaximumAppendEntries,
    int MaximumPageSize,
    bool VerifySnapshotHashes,
    bool DeleteOnDispose,
    ContentHash ConfigurationFingerprint);

public sealed record SessionExecutionCapability(
    SessionProfileSnapshot Profile,
    ISessionCoordinator Coordinator,
    ISessionRunCoordinator RunCoordinator);

public sealed record AuthorizedSessionDirectoryRequest<TRequest>(
    TRequest Request,
    SecurityGrant Grant)
    where TRequest : class;

public sealed record AuthorizedSessionStoreRequest<TRequest>(
    TRequest Request,
    SessionStoreKey StoreKey,
    SecurityGrant Grant)
    where TRequest : class;

public sealed record SessionAppendRequest(
    SessionOperationContext Context,
    BranchId BranchId,
    SessionVersion ExpectedVersion,
    IdempotencyKey IdempotencyKey,
    ImmutableArray<SessionEntry> Entries);

public sealed record SessionSnapshot(
    SessionSnapshotId Id,
    SessionAddress Address,
    BranchId BranchId,
    SessionSequence ThroughSequence,
    ContentHash ContentHash,
    ImmutableArray<byte> Payload,
    SchemaVersion SchemaVersion,
    DateTimeOffset CreatedAt);
```

Concrete entry records represent messages, input admission and promotion, tools,
security, goals, checkpoints, and lifecycle facts. The common base exists to
preserve ordering and causality; it is not an `object` payload escape hatch.
Unknown compatible serialized fields are retained in typed extension data.

`SessionSequence` is a per-branch coordinate: it is an entry's 1-based commit
position within its own `BranchId` alone. A store accepts an append only when
its batch's sequences are contiguous starting at that branch's own current tip
plus one; how many entries a sibling branch of the same session has committed is
irrelevant. `SessionVersion` remains the one whole-session
optimistic-concurrency token and advances by exactly one per committed mutation
regardless of which branch it targets, so two branches' own sequence numbering
is independent and may coincide numerically without naming related entries.
`SessionEntryId`, not the pair of branch and sequence, is an entry's stable
cross-branch identity; a fork carries an entry's original ID and sequence
forward unchanged into the new branch's own sequence space.

Coding-harness planning uses the same rule. `PlanSessionEntry` carries a
complete immutable `WorkPlan` revision with stable plan/item identities and an
authenticated author. Replacement and status change append a later revision
under session optimistic concurrency; neither mutates an earlier entry nor keeps
a private mutable plan beside the session record.

`BeforeRunOperationCorrelation` covers creation, admission, and other facts
recorded before a `RunId` exists. `InRunOperationCorrelation` carries the active
run and optional turn. `AfterRunOperationCorrelation` names the settled causal
run for deferred resolutions, late durable observations, and post-run cleanup
without falsely making that run active again. Every variant retains the distinct
`OperationId`; code must pattern-match the correlation rather than infer
lifecycle state from nullable IDs.

`SessionOperationContext.ExecutionLaneId` is present for lane-owned work and
absent only for explicitly session-wide creation, observation, or maintenance. A
lane mutation verifies the lane's installed operation against the correlation,
branch, and expected session version. A missing lane is never permission to
mutate an arbitrary active lane, and a stale operation cannot commit through a
successor's lease.

Snapshot writers copy or transfer payload ownership into `ImmutableArray<byte>`
before publication. Stores may use pooled buffers internally, but no public
snapshot may retain mutable caller-owned memory.

### Store discovery, selection, and state

```csharp
namespace AgentKit;

public sealed record SessionStoreDescriptor(
    SessionStoreKey Key,
    SessionStoreCapabilities Capabilities,
    SessionConsistencyModel Consistency,
    bool Durable,
    bool SupportsDistributedFencing);

public sealed record SessionLocation(
    SessionAddress Address,
    TenantId TenantId,
    SessionStoreKey StoreKey,
    SessionDirectoryRevision DirectoryRevision,
    DateTimeOffset RecordedAt,
    SchemaVersion SchemaVersion);

public sealed record SessionDirectoryWriteRequest(
    SessionOperationContext Context,
    SessionLocation Location,
    IdempotencyKey IdempotencyKey);

public abstract record SessionLocationResult;

public sealed record SessionLocated(SessionLocation Location)
    : SessionLocationResult;

public sealed record SessionLocationNotFound(SessionAddress Address)
    : SessionLocationResult;

public sealed record SessionDirectoryLookupUnavailable(string SafeMessage)
    : SessionLocationResult;

public abstract record SessionDirectoryWriteResult;

public sealed record SessionLocationRecorded(
    SessionLocation Location,
    bool Existing) : SessionDirectoryWriteResult;

public sealed record SessionLocationConflict(
    SessionLocation Existing,
    SessionStoreKey RequestedStoreKey) : SessionDirectoryWriteResult;

public sealed record SessionDirectoryWriteUnavailable(string SafeMessage)
    : SessionDirectoryWriteResult;

public interface ISessionDirectory
{
    bool Durable { get; }

    ValueTask<SessionLocationResult> LocateAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request,
        CancellationToken cancellationToken);

    ValueTask<SessionDirectoryWriteResult> RecordAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request,
        CancellationToken cancellationToken);
}

public interface ISessionStoreCatalog
{
    ImmutableArray<SessionStoreDescriptor> GetDescriptors();
}

public sealed record SessionStoreCreateSelectionRequest(
    SessionCreateRequest Request,
    SessionProfileSnapshot Profile);

public sealed record SessionStoreSelectionRequest(
    SessionOperationContext Context,
    SessionProfileSnapshot Profile,
    SessionLocation Location);

public interface ISessionStoreSelector
{
    ValueTask<SessionStoreSelectionResult> SelectForCreateAsync(
        SessionStoreCreateSelectionRequest request,
        CancellationToken cancellationToken);

    ValueTask<SessionStoreSelectionResult> ResolveExistingAsync(
        SessionStoreSelectionRequest request,
        CancellationToken cancellationToken);
}

public interface ISessionStore
{
    SessionStoreDescriptor Descriptor { get; }

    ValueTask<SessionCreateResult> CreateAsync(
        AuthorizedSessionStoreRequest<SessionCreateRequest> request,
        CancellationToken cancellationToken);

    ValueTask<SessionLoadResult> LoadAsync(
        AuthorizedSessionStoreRequest<SessionOperationContext> request,
        CancellationToken cancellationToken);

    ValueTask<SessionAppendResult> AppendAsync(
        AuthorizedSessionStoreRequest<SessionAppendRequest> request,
        CancellationToken cancellationToken);

    ValueTask<SessionPageResult> ReadAsync(
        AuthorizedSessionStoreRequest<SessionReadRequest> request,
        CancellationToken cancellationToken);

    ValueTask<SessionBranchResult> CreateBranchAsync(
        AuthorizedSessionStoreRequest<SessionBranchRequest> request,
        CancellationToken cancellationToken);

    ValueTask<SessionSnapshotResult> WriteSnapshotAsync(
        AuthorizedSessionStoreRequest<SessionSnapshotWriteRequest> request,
        CancellationToken cancellationToken);

    ValueTask<SessionDeleteResult> DeleteAsync(
        AuthorizedSessionStoreRequest<SessionDeleteRequest> request,
        CancellationToken cancellationToken);
}
```

Forward paging captures an immutable `SessionReadSnapshot` on the first read.
The snapshot contains the exact `SessionAddress`, `BranchId`, `SessionVersion`,
and inclusive `UpperSequence`. Continuation requests echo that value, and stores
return only entries whose sequence is greater than the page cursor and no
greater than `UpperSequence`. Later appends therefore never leak into an
in-progress read. `ThroughSequence` remains only the pagination position; it is
not a session version and may exceed the branch tip for an empty legacy read.
Stores reject future, wrong-address, wrong-branch, and beyond-tip snapshots with
a typed read failure rather than substituting current state. Public construction
does not establish provenance: a continuation is valid only when the selected
store previously issued the equal snapshot. An adapter may retain that
continuation evidence in a bounded transient cache; eviction or adapter restart
fails the continuation explicitly instead of accepting an unproved
version/upper-sequence pair.

Stores are additive, keyed state implementations. Selection occurs when a
session is created. Before creating store state, the coordinator idempotently
records the chosen `SessionStoreKey` in `ISessionDirectory`; retries and crash
recovery must finish creation against that same key. The session descriptor also
persists the key as an integrity check. Existing-session resolution asks the
directory, then the coordinator passes the authoritative `SessionLocation` and
captured `SessionProfileSnapshot` to the selector. The selector only maps that
returned key against its explicitly injected store set; it neither re-queries
the directory nor receives a directory grant. Application callers supply the
normal immutable operation context and never know a store key or access keyed
DI. A selector never scans stores, falls back to a new default, or migrates data
as an incidental read.

The directory is authoritative routing state with conditional, idempotent
writes. It partitions locations by tenant and validates the supplied operation
context and directory-specific bounded grant before reads or writes. Conflicting
attempts to bind one address to another key fail typed. A durable session
requires a durable directory whose availability, consistency, retention,
authorization, and recovery guarantees are compatible with its store. Missing or
unavailable location returns a typed result and never leaks session existence
across an authorization boundary.

Every directory and store is a protected effecting boundary. It validates the
audience, complete execution identity, agent/session, typed lifecycle
correlation, action, and scope of the supplied `SecurityGrant` immediately
before access. A location or selector result is not authority. The coordinator
authorizes directory lookup or recording first, then issues a distinct
store-specific security request after the store key is known. It never reuses a
directory grant for store access, a read grant for a write, or one grant across
two effecting calls. Each grant is short-lived, normally single-use, and bound
to its wrapper's exact request, audience, store key when applicable, and input
fingerprint.

### Coordination, policy, and observation

```csharp
namespace AgentKit;

public interface ISessionCoordinator
{
    ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken);

    ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken);

    ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken);

    ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken);
}

public interface ISessionRunCoordinator
{
    ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken);
}

public interface ISessionRunLease : IAsyncDisposable
{
    SessionLeaseId LeaseId { get; }
    AgentId AgentId { get; }
    SessionId SessionId { get; }
    ExecutionLaneId ExecutionLaneId { get; }
    OperationId OperationId { get; }
    RunId RunId { get; }
}

public interface ISessionRetentionPolicy
{
    ValueTask<SessionRetentionDecision> EvaluateAsync(
        SessionDescriptor session,
        CancellationToken cancellationToken);
}

public interface ISessionEventSink
{
    ValueTask PublishAsync(
        SessionEvent sessionEvent,
        CancellationToken cancellationToken);
}
```

The coordinator owns authorization orchestration, routing, the serialized
session mutation line, optimistic conflict handling, and semantic events. It
does not implement storage. The run coordinator owns process-local
single-active-operation behavior **per execution lane**. Different lanes may
overlap effects while every durable read-decide-write transition still enters
the one session mutation line. A durable execution adapter may replace or
augment lane ownership with fenced distributed leases; the local implementation
never claims cluster safety. Retention policy decides when work is eligible, the
store performs the protected mutation, and event sinks only observe immutable
outcomes.

The session coordinator never invokes the agent loop, input coordinator, context
assembler, or durability coordinator. The engine acquires a session run lease
for the expected lane and operation and then calls the loop; durability
integration decorates the lease/store boundary from above or below through
dedicated contracts. This avoids both session → loop → session and session →
durability → session constructor cycles.

AgentKit.Session supplies sealed coordinator, store catalog and selector, branch
service, and local run-coordinator classes. Its primary dependency shape is
explicit:

```csharp
namespace AgentKit.Session;

internal sealed class SessionCoordinator(
    ISessionDirectory directory,
    ISessionStoreSelector storeSelector,
    ISecurityAuthoritySelector securityAuthorities,
    ISessionRetentionPolicy retentionPolicy,
    IHookDispatcher hooks,
    IEnumerable<ISessionEventSink> eventSinks,
    TimeProvider timeProvider,
    IIdentifierGenerator<OperationId> operationIds) : ISessionCoordinator
{
}
```

The body is intentionally omitted from this constructor/dependency shape; its
observable API is exactly `ISessionCoordinator`. Store location and selection
remain explicit collaborators rather than container lookups.

`SessionCoordinator` selects the authority named by the immutable
`SessionOperationContext.Authorization` through `ISecurityAuthoritySelector`; it
never injects an unkeyed authority. It validates that the operation identity
equals `SecurityAuthorizationContext.Identity`, authorizes a directory grant for
the directory to consume, resolves the returned store key, then authorizes a
separate grant for the selected store to consume. Failure at any step returns a
typed result without probing another store or leaking whether the session
exists.

The coordinator is stateless with respect to session profiles. Every method
receives the immutable profile snapshot from the caller's
`SessionExecutionCapability`, validates its key/version/fingerprint and selected
coordinator key, and passes it to store selection. Multiple agents may therefore
share one coordinator implementation while retaining different stores, bounds,
retention, and concurrency policies. No constructor or factory captures the
first profile registered for a component key.

Accepted-state transactions and run-lease acquisition carry the complete
invocation-only `SessionExecutionCapability`. The selected coordinator rejects a
capability whose `Coordinator` is a different instance, and the selected run
coordinator rejects one whose `RunCoordinator` is different. This preserves the
compiled `CoordinatorKey` and `RunCoordinatorKey` binding without a container
lookup or ambient registry while accepted ownership is revalidated.

AgentKit defines no session-store base class. In-memory, SQLite, and remote
stores have materially different transaction, serialization, and ownership
mechanics; they implement the same interface and conformance suite directly.

## Configuration and dependency injection

Session behavior is captured from global ceilings, a named session profile, and
the agent definition. The agent definition's profile selects its coordinator and
run-coordinator component keys, default `SessionStoreKey`, busy-session policy,
paging bounds, and retention profile. Run overrides may tighten limits but
cannot silently change the store of an existing session.

Mutable option binding is validated and copied into a `SessionProfileSnapshot`
with a positive explicit version and configuration fingerprint when the agent
run plan is compiled. The invocation-only `SessionExecutionCapability` binds
that exact snapshot to its selected coordinator and run coordinator. A
configuration reload creates a later snapshot; it never changes an in-flight run
or reroutes an existing session.

```csharp
namespace AgentKit.Session;

public static class SessionComponentKeys
{
    public static ComponentKey<ISessionCoordinator> Coordinator { get; } =
        new("agentkit.session.coordinator");
    public static ComponentKey<ISessionRunCoordinator> RunCoordinator { get; } =
        new("agentkit.session.run-coordinator");
}

public static class SessionRetentionProfileKeys
{
    public static SessionRetentionProfileKey RetainUntilExplicitlyDeleted
        { get; } = new("agentkit.retain-until-explicit-delete");
}

public sealed class AgentSessionOptions
{
    public int MaximumAppendEntries { get; set; } = 128;
    public int MaximumPageSize { get; set; } = 256;
    public SessionBusyBehavior BusyBehavior { get; set; } =
        SessionBusyBehavior.Reject;
    public bool VerifySnapshotHashes { get; set; } = true;
    public bool DeleteOnDispose { get; set; }
}

public sealed class SessionProfileOptions
{
    public SessionProfileVersion Version { get; set; } = new(1);
    public ComponentKey<ISessionCoordinator> CoordinatorKey { get; set; } =
        SessionComponentKeys.Coordinator;
    public ComponentKey<ISessionRunCoordinator> RunCoordinatorKey { get; set; } =
        SessionComponentKeys.RunCoordinator;
    public SessionStoreKey? DefaultStoreKey { get; set; }
    public SessionRetentionProfileKey RetentionProfile { get; set; } =
        SessionRetentionProfileKeys.RetainUntilExplicitlyDeleted;
    public SessionBusyBehavior? BusyBehavior { get; set; }
    public int? MaximumAppendEntries { get; set; }
    public int? MaximumPageSize { get; set; }
    public bool? VerifySnapshotHashes { get; set; }
    public bool? DeleteOnDispose { get; set; }
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentSession(
            Action<AgentSessionOptions>? configure = null) =>
            SessionServiceRegistration.AddAgentSession(
                services,
                configure);

        public IServiceCollection AddSessionProfile(
            SessionProfileKey key,
            Action<SessionProfileOptions> configure) =>
            SessionServiceRegistration.AddSessionProfile(
                services,
                key,
                configure);

        public IServiceCollection ReplaceSessionProfile(
            SessionProfileKey key,
            Action<SessionProfileOptions> configure) =>
            SessionServiceRegistration.ReplaceSessionProfile(
                services,
                key,
                configure);

        public IServiceCollection AddSessionCoordinator<TCoordinator>(
            ComponentKey<ISessionCoordinator> key)
            where TCoordinator : class, ISessionCoordinator =>
            SessionServiceRegistration.AddSessionCoordinator<TCoordinator>(
                services,
                key);

        public IServiceCollection AddSessionRunCoordinator<TCoordinator>(
            ComponentKey<ISessionRunCoordinator> key)
            where TCoordinator : class, ISessionRunCoordinator =>
            SessionServiceRegistration.AddRunCoordinator<TCoordinator>(
                services,
                key);

        public IServiceCollection AddSessionStore<TStore>(
            SessionStoreKey key)
            where TStore : class, ISessionStore =>
            SessionServiceRegistration.AddSessionStore<TStore>(services, key);

        public IServiceCollection ReplaceSessionStore<TStore>(
            SessionStoreKey key)
            where TStore : class, ISessionStore =>
            SessionServiceRegistration.ReplaceSessionStore<TStore>(
                services,
                key);

        public IServiceCollection AddSessionDirectory<TDirectory>()
            where TDirectory : class, ISessionDirectory =>
            SessionServiceRegistration.AddSessionDirectory<TDirectory>(services);

        public IServiceCollection ReplaceSessionDirectory<TDirectory>()
            where TDirectory : class, ISessionDirectory =>
            SessionServiceRegistration.ReplaceSessionDirectory<TDirectory>(
                services);

        public IServiceCollection ReplaceSessionCoordinator<TCoordinator>(
            ComponentKey<ISessionCoordinator> key)
            where TCoordinator : class, ISessionCoordinator =>
            SessionServiceRegistration.ReplaceSessionCoordinator<TCoordinator>(
                services,
                key);

        public IServiceCollection ReplaceSessionRunCoordinator<TCoordinator>(
            ComponentKey<ISessionRunCoordinator> key)
            where TCoordinator : class, ISessionRunCoordinator =>
            SessionServiceRegistration.ReplaceRunCoordinator<TCoordinator>(
                services,
                key);

        public IServiceCollection ReplaceSessionStoreSelector<TSelector>()
            where TSelector : class, ISessionStoreSelector =>
            SessionServiceRegistration.ReplaceStoreSelector<TSelector>(
                services);

        public IServiceCollection AddSessionRetentionPolicy<TPolicy>(
            SessionRetentionProfileKey profile)
            where TPolicy : class, ISessionRetentionPolicy =>
            SessionServiceRegistration.AddRetentionPolicy<TPolicy>(
                services,
                profile);

        public IServiceCollection ReplaceSessionRetentionPolicy<TPolicy>(
            SessionRetentionProfileKey profile)
            where TPolicy : class, ISessionRetentionPolicy =>
            SessionServiceRegistration.ReplaceRetentionPolicy<TPolicy>(
                services,
                profile);
    }
}
```

The package-internal `SessionServiceRegistration` helper owns the registration
details and never builds or resolves a service provider.

`AddAgentSession` is idempotent and uses `TryAddKeyed` for each selected default
coordinator/run-coordinator key and `TryAdd` for the engine-wide store catalog,
store selector, branch service, snapshot validator, and conservative
retain-until-explicit-delete policy. `AddSessionProfile` publishes one immutable
versioned profile after validation. The directory is an engine-wide singular:
`AddSessionDirectory` rejects a conflicting registration and
`ReplaceSessionDirectory` is its explicit replacement path. Exact `Replace*`
methods replace one named profile, component key, store key, selector, or
retention profile at a time. Stores and event sinks are additive. Duplicate keys
fail build unless the corresponding replacement API names that same axis.

Defaults are useful but do not fabricate persistence: busy sessions reject,
append/page bounds are finite, snapshot hashes are verified, disposal does not
delete, and retention never deletes without an explicit transition. A runnable
profile must explicitly configure `DefaultStoreKey`; credentials, remote
endpoints, durable directories, and storage targets have no synthetic default.

`AddInMemorySessionStore(key)` and `AddSqliteSessionStore(key, configure)` are
explicit leaf registrations. Each adds its store to the additive `ISessionStore`
set: repeating the same leaf registration contributes one store, and a store
registered earlier by another leaf is neither replaced nor hidden, so
registration order never decides which store exists. The in-memory leaf may
`TryAdd` its process-local directory for explicitly ephemeral profiles; a
durable store package may `TryAdd` a compatible durable directory.
Multiple-store or externally managed topologies register or replace one
directory explicitly. `AddAgentSession` never chooses or hides a store or
fabricates durable routing. Runtime code receives `ISessionDirectory` and
`ISessionStoreSelector`, not `IServiceProvider` or a keyed-service locator.

The directory, store catalog, and selector are thread-safe singletons over
immutable routing snapshots; coordinators and thread-safe stores may also be
singletons when their dependency graph permits it. A local run lease and mutable
loaded session view are run-scoped and never retained in a singleton. Directory
and store clients are owned and disposed by the container that created them;
returned leases are owned by the caller and disposed exactly once. Different
session addresses may progress concurrently. Appends and directory records use
expected state and idempotency, and cancellation never reports an unknown commit
as definitely absent or reroutes a retry to another store. Once a store has
returned a typed result, the coordinator returns that result even if the
caller's token was cancelled meanwhile; it does not replace a committed outcome
with `OperationCanceledException`. Post-commit event publication is best-effort
and ignores caller cancellation because the effect it describes already
happened. Cancellation raised before or inside the store propagates as the
original exception.

## Composition validation and unsupported behavior

A runnable engine requires one engine-wide directory, store catalog, and store
selector. Each runnable agent definition resolves exactly one selected session
coordinator, run coordinator, immutable profile version, and store; at least one
store is registered explicitly. Build or agent-definition validation checks
unique keys, positive profile versions, default store/profile references,
configuration fingerprints, scope safety, page and batch bounds, snapshot
hashing, store capabilities, serialization versions, `TimeProvider`, ID
generators, security authority, hook dispatcher, and required event/audit
delivery. It also validates the constructor/factory graph for each selected
profile so a coordinator, selector, retention policy, or store cannot depend
back on its consumer.

Durable sessions cannot select an ephemeral store or a process-local directory.
The directory must be able to locate every existing configured session store
without probing, and selected store/directory durability and consistency must be
compatible. Distributed lane-operation mode cannot select a store without
fencing. Unsupported transactions, branching, snapshots, retention, or migration
return typed capability results before a partial mutation. Cross-agent,
cross-session, cross-tenant, missing-policy, or stale-grant access fails closed
without revealing whether the session exists. No missing store falls back to
process memory, and no local lock is presented as a distributed lease.

## Canonical record

The session record is append-oriented and versioned. It contains messages, input
admission and promotion, lifecycle transitions, model and configuration changes,
tool calls and results, permissions and approval references, compaction, goals,
delegation, and recovery checkpoints.

The first portable codec slice assigns stable wire identities
`agentkit.session/execution-lane-provisioned` and
`agentkit.session/input-promoted` to the corresponding base entry families and
reads and writes their existing exact schema version `1`. Their JSON schemas use
explicit field names and closed correlation discriminants; no CLR type name or
reflection activation enters durable data. Decoders bound nesting and compatible
unknown-field count and bytes before materializing semantic values, reject
duplicate or malformed fields, and retain the original immutable envelope for
unchanged persistence. This slice does not constitute the complete first-party
durable profile: message, admitted-input, and accepted-operation codecs remain
required before a persistent store may claim the five-entry base profile.

Compatible-field byte limits count the exact retained UTF-8 bytes from an
unknown property's opening name quote through the end of its value, including
the original escaped name spelling, colon, and interior whitespace. A nested
unknown subtree is charged once by bytes while every property in that subtree
still counts toward the compatible-field count limit.

The portable accepted-operation codec uses wire identity
`agentkit.session/operation-accepted` and the producer's exact schema version
`1`. It persists the complete accepted run state, including ordered promotion
and materialization identities, retained identity evidence, and security
profile, policy-snapshot, authority-selection, scope, and configuration
evidence. The authorization object explicitly references the single serialized
state identity and correlation; decoding reconstructs immutable evidence and
does not resolve an authority or create a grant. Recovery revalidates the
retained profile, policy, configuration, and authority selections at their live
owners before use. This third codec still does not complete the mandatory
durable base profile because admitted-input and message codecs remain required.

AgentKit.IO coordinates admission and promotion through these contracts. It does
not keep a private queue beside the session. An in-memory store may keep the
record in process, while a durable store must commit admission atomically with
the session version used to accept it.

Every entry has stable identity, monotonic sequence, causal linkage, timestamp,
and schema version. Appends use an expected version and idempotency identity so
concurrent writers cannot silently overwrite one another and retries cannot
duplicate facts.

A durable store proves every proposed entry has a registered codec before it
mutates any state. An entry the captured codec catalog cannot encode is reported
through the operation's typed failure result (for example `SessionAppendFailed`
or `SessionRunStartRejected`) with the session version and branch unchanged; it
never surfaces as a serialization exception after bookkeeping has advanced.

## Branching and compaction

Branches name a committed parent and create a new leaf without changing the
original path. Editing an earlier message, changing direction, or reverting
creates branch state rather than rewriting history. Tool effects remain causal
to their original calls; moving the conversation pointer backward does not undo
the outside world.

A fork point is validated against the named parent branch, not the session-wide
sequence allocator. `SessionBranchRequest.AtSequence` is zero for an empty fork
or the sequence of an entry committed on the parent branch; a sequence that
belongs to a sibling branch or lies beyond the parent tip returns
`SessionBranchParentNotFound` without allocating a branch or advancing the
session version.

[Compaction](../concepts/context-compaction.md) appends a versioned summary and
structured checkpoint over a complete semantic range while preserving the
covered entries. The active request view uses the applicable summary plus the
exact suffix. Failed or stale compaction never deletes the previous path.

Snapshots may accelerate loading, but each names an exact sequence and content
hash. A stale or corrupt snapshot is ignored in favor of verified log replay.

## Store boundary

The session store defines authorization, create and load, conditional append,
pagination, branches, snapshots, consistency, transactions, retention, archival,
deletion, migration, and failure behavior. Serialization is provider- neutral
and preserves compatible unknown fields.

Storage client types stay in leaf packages. The runtime interacts only with the
session contract and does not keep session state forever in a shared singleton.

AgentKit.Session.InMemory supplies deterministic ephemeral storage for tests,
examples, and short-lived applications. AgentKit.Session.Sqlite supplies the
first durable local implementation. Future stores follow
AgentKit.Session.ProviderName. A store is registered separately and composition
fails when none is present; there is no hidden production default.

AgentKit.Session.Sqlite persists one row per session, branch, entry, lane,
admission, and idempotency receipt in a relational schema, not one whole-store
JSON blob. A durable read or mutation loads and (for writes, atomically commits)
only the rows the operation actually needs — typically one session's own
metadata, one branch's cached tip, and the exact bounded page of entries a read
requested — inside a SQLite transaction: a deferred (read) transaction for reads
and a non-deferred (`BEGIN IMMEDIATE`) transaction for mutations, so SQLite's
own write lock, not a process-local gate, arbitrates writers and makes the
optimistic-concurrency check against `SessionVersion` atomic with the commit.
Branching copies a parent branch's committed rows up to the fork point without
decoding them. Because ordinary appends, admissions, lane provisioning, and run
acceptance never decode a previously committed entry payload, and forking never
decodes at all, a corrupt or unreadable entry can only affect a paged read that
actually names the row containing it — never a scan across the whole store. This
is a deliberate breaking on-disk format change from the single-row-blob shape
used before this schema existed; the format was never released, so
`SqliteSchemaMode.ApplyKnownMigrations` creates the relational schema fresh
rather than reading or reinterpreting an older blob-shaped database, and
`SqliteSchemaMode.ValidateExact` against one fails with a typed exception
instead of silently reinterpreting it.

## Mutation boundaries and idempotency

The mutation coordinator never holds its session critical section while waiting
for a provider, tool, approval, child run, hook, or remote event sink. It
captures the branch tip, expected version, lane, and operation identity,
releases the critical section for that work, then rechecks all commit
preconditions on return. A stale result may be recorded as evidence for its
original operation; it cannot append to whichever branch is current or repeat
its effect to resolve an optimistic conflict. A stable loaded snapshot remains
immutable while other lanes append. Read-your-writes uses an explicit committed
cursor, not a mutable singleton session view.

Every mutating retry first reconciles the idempotency identity, then compares
expected state. An equivalent already-committed append returns its original
receipt even if the current session version is newer. Reuse of the identity with
different canonical content is a conflict. Lookup and conflict handling remain
authorized and tenant-scoped; they cannot disclose another caller's receipt.

## Execution-lane ownership

The default coordinator permits one active operation per execution lane. New
work for a busy lane must explicitly join, queue, wait, or fail. Operations on
different lanes may overlap provider and tool effects, including lanes in one
session, while their durable mutations serialize and validate expected session,
branch, lane, and operation versions. A one-lane-per-session host is an explicit
compatibility profile, not a session invariant. A local lock provides
process-local coordination only. Cross-process ownership requires the durable
execution component's leases and fencing.

Lane bookkeeping in the first-party stores follows the branch tip. Provisioning
binds a lane cursor to one branch, admission and acceptance move it as they
append, and an ordinary append whose context names that lane and targets the
lane's branch advances the cursor to the last appended entry without changing
the lane revision. Session-wide appends without a lane do not move any lane
cursor. An occupied lane answers every later start with `SessionRunStartBusy`
naming the installed operation and run until `ISessionStore.ReleaseRunAsync`
explicitly clears it.

`ReleaseRunAsync` atomically clears a lane's installed accepted run state so a
later `AcceptRunAsync` for the same lane no longer observes
`SessionRunStartBusy`. The release request names the exact operation, run, and
total-state revision it owns; a store clears the lane only when its installed
accepted state's correlation and revision match that evidence, so a stale caller
— for example a lease left over from a superseded attempt — can never clear a
different, newer occupant (`SessionRunReleaseRejected` with kind `Fenced`). A
missing session or lane, and a cross-tenant caller, are all masked as kind
`LaneNotFound`, the same masking every other protected session operation uses. A
lane holding no accepted run is kind `NoAcceptedRun`, and a stale expected
whole-session version is kind `SessionVersion`. Release appends no session entry
and does not move any branch tip; it only clears the lane's durable ownership
marker and advances the canonical whole-session version by one. It carries an
idempotency key so a retried release after a lost response returns the original
`SessionRunReleased` receipt rather than a second commit.

`AbortRunAsync` is the durable cancel marker for one exact accepted run. It does
not widen `SessionAcceptedRunState`. A later `LoadRunStateAsync` returns
`SessionRunStateLoaded` with `AbortRequested` true only when that marker is
committed. The same commit prunes pending admissions for that run and advances
the lane revision and the operation-state revision. Rejection — a different
run, a stale expected revision, a missing session or lane, or an authorization
failure — appends nothing and prunes nothing. An equivalent idempotent retry
returns `SessionRunAbortRecorded` without a second revision advance.

```csharp
public abstract record SessionRunAbortResult;
public sealed record SessionRunAbortRecorded(
    SessionVersion NewVersion,
    SessionLaneRevision LaneRevision,
    OperationStateRevision StateRevision,
    bool Existing) : SessionRunAbortResult;
public sealed record SessionRunAbortRejected(
    SessionRunAbortRejectionKind Kind,
    string SafeReason) : SessionRunAbortResult;

public enum SessionRunAbortRejectionKind
{
    Unsupported,
    LaneNotFound,
    NoAcceptedRun,
    Fenced,
    SessionVersion,
    Idempotency,
}

public sealed record SessionRunAbortRequest(
    SessionOperationContext Context,
    OperationStateRevision ExpectedStateRevision,
    SessionLaneRevision ExpectedLaneRevision,
    SessionVersion ExpectedVersion,
    IdempotencyKey IdempotencyKey);

public sealed record SessionRunStateLoaded(
    SessionAcceptedRunState State,
    bool AbortRequested = false) : SessionRunStateResult;
```

`AbortRequested` defaults to false. True means a durable cancel marker is
committed for that accepted run and pending admissions for that run were pruned
in the same commit. Messages on `SessionRunAbortRejected` are content-free.
The hierarchy is closed: callers match `SessionRunAbortRecorded` or
`SessionRunAbortRejected` and do not invent a third outcome.

`ISessionRunLease` exposes this as an explicit `ReleaseAsync` member distinct
from ordinary `DisposeAsync`. Plain disposal intentionally releases only the
process-local lease so that a crash or handoff can still recover and reacquire
the identical accepted operation (`DefaultSessionRunCoordinator` revalidates
canonical accepted state before granting reacquisition); it never touches
durable store state. `ReleaseAsync` is the explicit alternative for final
settlement: it durably releases the lane through the protected session
coordinator, using the lease's own retained context and total-state revision
plus a freshly loaded session version, and then performs the same local release
as `DisposeAsync`. A failure releasing durable state — a stale version, a store
outage, or cancellation — is logged and swallowed rather than thrown, matching
the no-throw contract expected of a disposal-adjacent operation; local ownership
is always released regardless. A failed durable release leaves the lane busy
until a later successful `ReleaseAsync` call or store-level recovery.

Conversation history belongs here. Durable memory across sessions belongs to the
memory component. The working provider context belongs to the context component.
Combining them into one cheerful bucket called memory would destroy their policy
and consistency boundaries.

Both the in-memory and SQLite stores run through the same session-store
conformance suite for ordering, idempotency, optimistic concurrency, concurrent
appends, branching, pagination and snapshot continuation, tenant masking, input
admission and promotion, and cancellation. SQLite additionally proves committed
state survives close and reopen and that a disposed store fails closed without
touching persisted state. It advertises durable local transactions, not
distributed lane fencing or atomicity with a security, budget, artifact, or
provider store; profiles requiring those guarantees select a capable backend.

## Related concept specifications

- [Sessions, persistence, and branching](../concepts/sessions-persistence-and-branching.md)
- [Session execution lanes](../concepts/session-execution-lanes.md)
- [Context compaction](../concepts/context-compaction.md)
- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
