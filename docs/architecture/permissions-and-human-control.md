# Security, permissions, and human control

**Role:** Decide whether any protected operation has authority to proceed and
obtain a bound human decision when policy requires one.

Security is built into the runtime spine. It is not a tool middleware and it is
not optional merely because an application has no interactive approval UI.
AgentKit.Permissions contains the first-party security authority, ordered policy
evaluation, approval coordination, grant lifecycle, and human-control services.
Their provider-neutral contracts and values live in AgentKit.Abstractions.

AddAgentPermissions registers the authority, policy pipeline, approval broker,
and fail-closed defaults. Every component receives the security authority
through dependency injection and may request authorization for a protected
operation.

`AgentEngine` may host many agents concurrently. Security decisions are never
engine-global ambient state: each authorization context and request binds typed
`AgentId`, optional `SessionId`, and `OperationCorrelation`. The correlation's
before-run, in-run, and after-run variants never fabricate a `RunId`. A policy
selected for one agent cannot leak into another agent merely because both share
the same process or singleton authority. An immutable effective-policy snapshot
may be shared where the selected configuration explicitly uses the same policy
set; the snapshot never supplies another operation's scope or identity.

## Normative minimal contract shape

The neutral deferral values are defined by
[deferred operations](../concepts/deferred-and-human-in-the-loop.md).
`ProtectedOperation` captures normalized effect/resource evidence;
`SecurityDecisionReference` points to an audited decision and policy snapshot.
Neither is a grant. `DeferredOperationRequest` explicitly distinguishes runtime
ownership from external handoff, records whether execution started, and retains
the exact input fingerprint for later validation. The session owner persists
requests and resolutions; the security authority revalidates permission at the
effecting boundary.

These C# 14 shapes are normative and minimal, not an exhaustive API listing.
Each named type belongs in its own file in AgentKit.Abstractions. Shared
`AgentId`, `SessionId`, `OperationCorrelation`, and
`IIdentifierGenerator<TIdentifier>` contracts are reused; security-specific
identity is not represented with strings:

```csharp
namespace AgentKit;

public readonly record struct SecurityRequestId(Guid Value);

public readonly record struct GrantId(Guid Value);

public readonly record struct ApprovalRequestId(Guid Value);

public readonly record struct ApprovalResponseId(Guid Value);

public readonly record struct SecurityAuditRecordId(Guid Value);

public readonly record struct SecurityPolicySnapshotId(Guid Value);

public readonly record struct SecurityPolicyId(string Value);

public readonly record struct SecurityProfileKey(string Value);

public readonly record struct SecurityProfileVersion(long Value);

public readonly record struct SecurityRevocationVersion(long Value);

public readonly record struct ApprovalChannelId(string Value);

public readonly record struct ApprovalAuthenticationEvidenceId(string Value);

public enum SecurityRevocationTrigger
{
    Explicit,
    PolicySnapshotRetired,
    PrincipalDisabled,
    SessionClosed,
    HostGenerationRetired
}
```

Request, approval, grant, and policy-snapshot IDs are created by injected
generators. Snapshot fingerprints are computed from the immutable effective
policy set. Times and expiry use injected `TimeProvider`; no security decision
depends on ambient clock or static identity generation.

```csharp
namespace AgentKit;

public sealed record SecurityPolicySnapshotReference(
    SecurityPolicySnapshotId Id,
    SecurityPolicyVersion Version,
    ContentHash Fingerprint);

public sealed record SecurityAuthorizationScope(
    AgentId AgentId,
    SessionId? SessionId,
    OperationCorrelation Correlation);

public sealed record SecurityAuthorizationContext(
    SecurityProfileKey ProfileKey,
    SecurityProfileVersion ProfileVersion,
    SecurityPolicySnapshotReference PolicySnapshot,
    ComponentKey<ISecurityAuthority> AuthorityKey,
    AgentDefinitionRevision AgentDefinitionRevision,
    ConfigurationVersion ConfigurationVersion,
    SecurityAuthorizationScope Scope,
    ExecutionIdentity Identity);

public sealed record SecurityRequest(
    SecurityRequestId Id,
    SecurityAuthorizationScope Scope,
    ToolCallId? ToolCallId,
    SecurityAuthorizationContext Authorization,
    ComponentId ComponentId,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<ProtectedResource> Resources,
    SecurityDestination? Destination,
    InputFingerprint InputFingerprint,
    SecurityRequestLifetime RequestedLifetime,
    SecurityVersionSet Versions,
    DateTimeOffset Deadline,
    OperationId? CausalParentId,
    ExtensionData Details);

public abstract record SecurityDecision(
    SecurityRequestId RequestId,
    SecurityPolicyVersion PolicyVersion);

public sealed record SecurityAllowed(
    SecurityRequestId RequestId,
    SecurityPolicyVersion PolicyVersion,
    SecurityGrant Grant) : SecurityDecision(RequestId, PolicyVersion);

public sealed record SecurityDenied(
    SecurityRequestId RequestId,
    SecurityPolicyVersion PolicyVersion,
    SecurityDenial Denial) : SecurityDecision(RequestId, PolicyVersion);

public sealed record SecurityApprovalRequired(
    SecurityRequestId RequestId,
    SecurityPolicyVersion PolicyVersion,
    ApprovalRequest Approval) : SecurityDecision(RequestId, PolicyVersion);

public sealed record SecurityGrant(
    GrantId Id,
    SecurityRequestId RequestId,
    SecurityAuthorizationScope Scope,
    SecurityAuthorizationContext Authorization,
    ComponentId Audience,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<ProtectedResource> Resources,
    SecurityDestination? Destination,
    InputFingerprint InputFingerprint,
    SecurityVersionSet Versions,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int AllowedUses,
    SecurityRevocationConstraint Revocation);
```

`SecurityGrant` is immutable evidence. Remaining uses and revocation are
authoritative state in `ISecurityGrantStore`, not a mutable property that two
concurrent operations can decrement locally.

```csharp
namespace AgentKit;

public sealed record ApprovalValidityWindow(
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt);

public sealed record SecurityRevocationConstraint(
    SecurityRevocationVersion Version,
    ImmutableArray<SecurityRevocationTrigger> Triggers);

public sealed record ApprovalAuthenticationEvidence(
    ApprovalAuthenticationEvidenceId Id,
    ApprovalChannelId ChannelId,
    ApprovalRequestId RequestId,
    OperationCorrelation Correlation,
    PrincipalId AuthenticatedPrincipalId,
    ApprovalAuthenticationMethod Method,
    DateTimeOffset AuthenticatedAt,
    ContentHash ChannelBinding);

public sealed record ApprovalScopeBinding(
    SecurityAuthorizationContext Authorization,
    SecurityAuthorizationScope Scope,
    ComponentId Audience,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<ProtectedResource> Resources,
    SecurityDestination? Destination,
    InputFingerprint InputFingerprint,
    SecurityVersionSet Versions,
    ApprovalValidityWindow Validity,
    int AllowedUses,
    SecurityRevocationConstraint Revocation);

public sealed record ApprovalRequest(
    ApprovalRequestId Id,
    SecurityRequest Request,
    ApprovalScopeBinding Binding,
    SecurityPresentation Presentation,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ApprovalResponse(
    ApprovalResponseId Id,
    ApprovalRequestId RequestId,
    ApprovalScopeBinding Binding,
    ApprovalResolution Resolution,
    ApproverIdentity Approver,
    ApprovalAuthenticationEvidence Authentication,
    DateTimeOffset RespondedAt,
    IdempotencyKey IdempotencyKey);

public sealed record SecurityAuditRecord(
    SecurityAuditRecordId Id,
    SecurityAuthorizationScope Scope,
    SecurityRequestId RequestId,
    GrantId? GrantId,
    ApprovalRequestId? ApprovalRequestId,
    SecurityAuditEventKind EventKind,
    SecurityAuditOutcome Outcome,
    SecurityPolicyVersion PolicyVersion,
    ImmutableDictionary<string, RedactedAuditValue> Fields,
    DateTimeOffset OccurredAt);
```

Authority-bearing fields are structural and read-only. `ApprovalScopeBinding`
duplicates the security-relevant scope deliberately so a delayed response can be
matched without consulting a mutable current agent definition. Safe presentation
text cannot substitute for the policy snapshot, profile revision, operation,
resource, destination, input fingerprint, validity window, use count, or
revocation version to which an approval binds. `ApproverIdentity` is display and
audit identity; acceptance also requires typed authenticated-channel evidence
whose principal and channel binding match the pending request.

### Discovery, selection, and policy

```csharp
namespace AgentKit;

public interface ISecurityPolicy
{
    ValueTask<SecurityPolicyResult> EvaluateAsync(
        SecurityRequest request,
        SecurityPolicyContext context,
        CancellationToken cancellationToken);
}

public sealed record SecurityAuthorizationCaptureRequest(
    SecurityAuthorizationScope Scope,
    SecurityProfileKey ProfileKey,
    AgentDefinitionRevision AgentDefinitionRevision,
    ConfigurationVersion ConfigurationVersion,
    ExecutionIdentity Identity);

public interface ISecurityProfileSelector
{
    ValueTask<SecurityAuthorizationContextResult> CaptureAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken);
}

public interface ISecurityPolicyCatalog
{
    ValueTask<SecurityPolicySnapshotResult> ResolveAsync(
        SecurityPolicySnapshotReference reference,
        CancellationToken cancellationToken);
}

public interface ISecurityPolicySelector
{
    ValueTask<SecurityPolicySnapshotResult> SelectAsync(
        SecurityRequest request,
        CancellationToken cancellationToken);
}

public interface ISecurityAuthoritySelector
{
    ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken);
}

public interface ISecurityAuthority
{
    ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);
}
```

**Interim types (WS3-C1).** The following shapes are not shown elsewhere in this
document. They are additive contracts introduced ahead of the chunks that wire
them into `ISecurityPolicy`, the policy catalog, and the authority so that later
chunks add behavior to an existing shape rather than inventing it under schedule
pressure.

```csharp
namespace AgentKit;

/// <summary>Classifies how a responder authenticated to a trusted approval channel before answering.</summary>
public enum ApprovalAuthenticationMethod
{
    Password,
    OneTimePasscode,
    HardwareSecurityKey,
    SingleSignOnAssertion,
    ApiCredential,
}

/// <summary>Carries the immutable evidence a policy needs to evaluate one request. WS3-C3 adds this as a parameter to <see cref="ISecurityPolicy.EvaluateAsync"/>.</summary>
public sealed record SecurityPolicyContext(
    SecurityAuthorizationContext Authorization,
    SecurityRevocationVersion RevocationVersion,
    DateTimeOffset EvaluatedAt);

/// <summary>One policy's optional intersecting bounds for an allow or approval-conditioned proposal. WS3-C3 adds this as <c>SecurityPolicyResult.Constraints</c> and wires intersection into the authority.</summary>
public sealed record SecurityAllowConstraints(
    ImmutableArray<ProtectedResource>? Resources,
    SecurityEffect? Effect,
    DateTimeOffset? NotBefore,
    DateTimeOffset? ExpiresAt,
    int? AllowedUses);

/// <summary>Closed terminal result of resolving or selecting one effective policy snapshot reference through <see cref="ISecurityPolicyCatalog"/> or <see cref="ISecurityPolicySelector"/>.</summary>
public abstract record SecurityPolicySnapshotResult;
public sealed record SecurityPolicySnapshotResolved(SecurityPolicySnapshotReference Reference): SecurityPolicySnapshotResult;
public sealed record SecurityPolicySnapshotStale(SecurityPolicySnapshotReference Reference, string SafeReason): SecurityPolicySnapshotResult;
public sealed record SecurityPolicySnapshotUnavailable(string SafeReason): SecurityPolicySnapshotResult;
```

Policies are additive, ordered contributors. They return typed match, abstain,
deny, require-approval, or bounded-allow proposals; they never issue grants. The
profile selector captures the effective policy snapshot, profile/configuration
versions, authority key, and complete immutable execution identity for the
operation being authorized. Run-plan compilation pins the selected
configuration; it does not create one reusable authorization scope for every
later operation. The policy selector thereafter resolves the exact snapshot
reference carried by the request; a catalog must retain it for the maximum
grant/approval lifetime or return a typed stale-snapshot denial. The authority
selected for `SecurityAuthorizationContext.AuthorityKey` evaluates that
immutable snapshot, applies precedence, records the decision, and alone asks the
grant issuer to create bounded authority. Direct run work receives that selected
authority from the compiled run plan; delayed approval resolution uses
`ISecurityAuthoritySelector` and the persisted binding rather than a keyed
container lookup.

`SecurityAuthorizationScope` is captured with the profile and policy snapshot,
not supplied later as unrelated fields. Requests, approvals, grants, cache keys,
and enforcement compare the entire scope structurally. `SessionId` is optional
only for truthful before-session or sessionless protected operations; when it is
present every later boundary requires exact equality, and policy denies an
operation kind that requires a session when it is absent. No caller fabricates a
session identity to obtain authority.

### Capture, retention, and operation scope

A capture is a coherent selection from one published configuration revision. The
selector resolves the requested profile against the exact agent-definition and
configuration revisions supplied by the caller, then captures its authority key
and immutable effective-policy snapshot together. It MUST NOT combine a profile
from one publication with policies from a later publication, or fall back to the
latest revision when a requested revision cannot be resolved. Unpublished,
retired, missing, or unavailable required bindings return a typed unsuccessful
capture; no partial context is returned. Caller cancellation propagates without
publishing a successful capture.

The captured context is evidence, not authority. Capturing it neither evaluates
an effect nor issues or consumes a grant. A request must still describe its
canonical resources, destination, input fingerprint, audience, and bounded
lifetime before authorization. Possession of a syntactically valid context does
not prove that its referenced profile or policy snapshot exists.

An unchanged operation may reuse its captured context for distinct security
requests, each with its own request identity and bounded grant. A new operation,
changed correlation stage or turn, newly established session, or changed
execution identity requires a fresh capture with that exact scope and identity.
The caller preserves the run's pinned definition and configuration revisions
unless an explicit runtime transition adopts a new configuration. Re-capture is
not permission to mutate an existing grant or to upgrade a before-run grant into
authority for in-run work. Existing effects and their receipts retain the
original evidence.

Publication of a newer configuration does not itself invalidate an older
retained snapshot. Retention and revocation are separate: exact retained
bindings remain resolvable for their declared lifetime, while explicit
retirement or live revocation can stop future grants and consumption. The
security runtime revalidates captured references; effecting boundaries validate
the resulting grant's exact context binding, uses, expiry, and live revocation
immediately before acting. An unavailable validation path fails closed rather
than substituting a newer policy or treating a cached allow as sufficient.

### Selection publication and activation

Security selection reads a previously validated immutable publication. That
publication proves the binding between agent identity and definition revision,
configuration revision, selected profile key/version, authority key, and policy
snapshot reference. Matching a profile key alone is insufficient. Composition
validates candidate publications against captured registration metadata before
making them visible. Runtime capture depends on a read-only publication reader,
not on the agent catalog's publication coordinator, a composition validator, or
the run-plan compiler. Those components may share immutable evidence; they must
not form a constructor cycle by asking one another to publish or validate the
snapshot they are currently resolving.

Persistable policy snapshots contain ordered, versioned policy descriptors and
their configuration evidence. They never contain policy instances, delegates,
service scopes, or container references. A typed activation boundary resolves
the exact retained registrations for one authorization operation. The operation
owns the resulting lifetime lease, keeps scoped collaborators alive through
policy evaluation and settlement, and releases the lease after awaited work
finishes. Releasing a lease disposes its owned scope; it does not directly
dispose a shared singleton policy or authority. Activation failure returns a
typed unsuccessful selection before any grant can be issued.

DI implementation bindings are fixed for a built engine. Profile reload may
select already registered keys and publish new immutable policy/configuration
versions; it cannot replace the implementation behind a captured key in place.
Replacing that implementation requires a new composition. Existing operations
retain their original composition's bindings and scope ownership until they
finish or are explicitly handed off. Delayed work in another composition must
resolve and verify the retained configuration and implementation binding named
by its captured evidence. If that binding cannot be reconstructed or retained,
selection returns a typed stale or unavailable result. Resolving whichever
service currently has the same key is not an equivalent recovery path.

`HookDispatchContext` is an optional non-persisted invocation dependency. An
in-run caller passes its active hook lease; before-run work and delayed approval
resolution pass `null` and still execute the required fail-closed policy path.
The context is never embedded in a request, grant, approval, or audit record.

### Approval execution, grant state, and observation

```csharp
namespace AgentKit;

public interface IApprovalHandler
{
    ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);
}

public interface IApprovalHandlerDispatcher
{
    ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);
}

public interface IApprovalBroker
{
    ValueTask<ApprovalBrokerResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);

    ValueTask<ApprovalResolutionResult> ResolveAsync(
        ApprovalResponse response,
        CancellationToken cancellationToken);
}

public interface IApprovalStore
{
    ValueTask<ApprovalStoreResult> CreateAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);

    ValueTask<ApprovalStoreResult> ResolveAsync(
        ApprovalResponse response,
        CancellationToken cancellationToken);
}

public interface ISecurityGrantStore
{
    ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken);

    ValueTask<GrantRevocationResult> RevokeAsync(
        GrantId grantId,
        RevocationReason reason,
        CancellationToken cancellationToken);
}
```

**Interim types (WS3-C1).** The following shapes are not shown elsewhere in this
document. They are additive contracts that later chunks (C6a grant
issuer/decision store, C7a typed revocation, C8a handler dispatcher, C8b durable
resolution) wire into the authority and broker.

```csharp
namespace AgentKit;

/// <summary>Routes one approval request to the configured additive handlers and returns the first decisive outcome. WS3-C8a wires this into <c>ApprovalBroker</c> in place of a single injected handler.</summary>
public interface IApprovalHandlerDispatcher
{
    ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Mints bounded security grants from an already-decided allow or approved scope. WS3-C6a extracts this from the authority's inline minting logic.</summary>
public interface ISecurityGrantIssuer
{
    ValueTask<SecurityGrant> IssueAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion,
        ApprovalScopeBinding? approvedBinding,
        CancellationToken cancellationToken);
}

/// <summary>Durably records every terminal security decision, independent of grant, approval, and audit-sink storage. WS3-C6a wires this into the authority.</summary>
public interface ISecurityDecisionStore
{
    ValueTask RecordAsync(SecurityDecision decision, CancellationToken cancellationToken);
}

/// <summary>Names the trigger and a non-sensitive explanation for retiring a bounded grant.</summary>
public sealed record RevocationReason(SecurityRevocationTrigger Trigger, string SafeMessage);

/// <summary>Closed terminal outcome of attempting to revoke one bounded security grant through the typed <c>RevokeAsync</c> overload.</summary>
public abstract record GrantRevocationResult
{
    public GrantId GrantId { get; }
}
public sealed record GrantRevoked(GrantId GrantId, RevocationReason Reason): GrantRevocationResult;
public sealed record GrantAlreadyRevoked(GrantId GrantId): GrantRevocationResult;
public sealed record GrantRevocationNotFound(GrantId GrantId): GrantRevocationResult;
public sealed record GrantRevocationUnavailable(GrantId GrantId, string SafeReason): GrantRevocationResult;

/// <summary>Closed terminal result of resolving one durable approval request through the broker's durable resolution path. WS3-C8b adds <c>IApprovalBroker.ResolveAsync(ApprovalResponse, CancellationToken)</c> returning this type.</summary>
public abstract record ApprovalResolutionResult;
public sealed record ApprovalResolved(ApprovalResponse Response): ApprovalResolutionResult;
public sealed record ApprovalAlreadyResolved(ApprovalResponse Response): ApprovalResolutionResult;
public sealed record ApprovalResolutionExpired: ApprovalResolutionResult;
public sealed record ApprovalResolutionConflict(string SafeReason): ApprovalResolutionResult;
public sealed record ApprovalResolutionUnavailable(string SafeReason): ApprovalResolutionResult;
```

`IApprovalHandler` implementations are additive transports such as CLI, UI, or
durable remote control. The broker owns deterministic routing and deferral;
handlers do not evaluate policy. `IApprovalStore` and `ISecurityGrantStore` own
durable concurrency and idempotency. `ISecurityAuditSink` observes redacted
records through the canonical observability contract in
[observability](observability.md); it never decides or enforces. This document
owns `SecurityAuditRecord` semantics but does not redeclare the sink.

Security decision, approval, and grant stores use their own narrow persistence
contracts. They MUST NOT implement durability by calling `ISessionCoordinator`:
session operations already depend on the security authority, and that reverse
edge would create session → security → session. A backend may share a database,
transaction engine, or low-level storage adapter with session storage, but it
does so beneath both coordinator contracts.

`AgentKit.Permissions` owns these contracts and security coordination but no
concrete store. `AgentKit.Permissions.InMemory` supplies explicitly ephemeral
adapters. `AgentKit.Permissions.Sqlite` supplies durable local adapters for the
security state whose consistency requirements SQLite can meet. Hosts configure
and register one leaf explicitly; `AddAgentPermissions` never chooses a store,
connection, database path, or fallback. Both leaves run the same security-store
conformance suites for shared operations, while durable deferral, required-audit
acceptance, multi-process access, and transaction claims are tested only when
the selected adapter advertises them.

The effecting component calls `ValidateAndConsumeAsync` immediately before the
effect using a fresh `SecurityEnforcementRequest`. File, network, process,
memory, session, provider-egress, MCP, and delegation implementations all do
this at their lowest enforceable boundary.

## First-party runtime dependencies

AgentKit.Permissions supplies sealed authority, policy-catalog, selector,
approval-broker, grant-lifecycle, and audit-dispatch classes. Their dependency
shape is explicit and container-neutral:

The authority consumes the immutable [execution identity](identity.md) carried
by the request. It does not parse credentials, resolve an ambient principal, or
call the identity resolver.

```csharp
namespace AgentKit.Permissions;

internal sealed class SecurityAuthority(
    ISecurityPolicySelector policySelector,
    ISecurityGrantIssuer grantIssuer,
    ISecurityDecisionStore decisionStore,
    IHookDispatcher hooks,
    ISecurityAuditDispatcher audit,
    TimeProvider timeProvider,
    IIdentifierGenerator<GrantId> grantIds) : ISecurityAuthority
{
}

internal sealed class ApprovalBroker(
    IApprovalHandlerDispatcher handlers,
    IApprovalStore store,
    ISecurityAuthoritySelector authoritySelector,
    ISecurityAuditDispatcher audit,
    TimeProvider timeProvider) : IApprovalBroker
{
}
```

AgentKit defines no security-policy or approval-handler base class: evaluation
and transport implementations share too little mechanics to justify inheritance.
Direct interface implementation remains the extension path.

## Configuration and dependency injection

Behavior is controlled by typed global ceilings, named security profiles,
ordered policy registrations, and agent-definition profile selection. Agent or
run configuration may tighten managed host policy but cannot override a managed
deny or broaden a host ceiling.

```csharp
namespace AgentKit.Permissions;

public sealed class AgentPermissionOptions
{
    public UnmatchedSecurityRequestBehavior UnmatchedRequestBehavior { get; set; } =
        UnmatchedSecurityRequestBehavior.Deny;
    public HeadlessApprovalBehavior HeadlessApprovalBehavior { get; set; } =
        HeadlessApprovalBehavior.Deny;
    public TimeSpan DefaultGrantLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public int DefaultGrantUses { get; set; } = 1;
    public bool CacheAllowDecisions { get; set; }
    public SecurityAuditDelivery AuditDelivery { get; set; } =
        SecurityAuditDelivery.Required;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentPermissions(
            ComponentKey<ISecurityAuthority> authorityKey,
            Action<AgentPermissionOptions>? configure = null) =>
            PermissionServiceRegistration.AddAgentPermissions(
                services,
                authorityKey,
                configure);

        public IServiceCollection AddSecurityProfile(
            SecurityProfileKey key,
            Action<SecurityProfileOptions> configure) =>
            PermissionServiceRegistration.AddSecurityProfile(
                services,
                key,
                configure);

        public IServiceCollection ReplaceSecurityProfile(
            SecurityProfileKey key,
            Action<SecurityProfileOptions> configure) =>
            PermissionServiceRegistration.ReplaceSecurityProfile(
                services,
                key,
                configure);

        public IServiceCollection AddSecurityPolicy<TPolicy>(
            SecurityPolicyRegistration registration)
            where TPolicy : class, ISecurityPolicy =>
            PermissionServiceRegistration.AddSecurityPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceSecurityPolicy<TPolicy>(
            SecurityPolicyRegistration registration)
            where TPolicy : class, ISecurityPolicy =>
            PermissionServiceRegistration.ReplaceSecurityPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection AddApprovalHandler<THandler>()
            where THandler : class, IApprovalHandler =>
            PermissionServiceRegistration.AddApprovalHandler<THandler>(services);

        public IServiceCollection AddSecurityAuditSink<TSink>(
            SecurityAuditSinkRegistration registration)
            where TSink : class, ISecurityAuditSink =>
            PermissionServiceRegistration.AddSecurityAuditSink<TSink>(
                services,
                registration);

        public IServiceCollection ReplaceSecurityAuthority<TAuthority>(
            ComponentKey<ISecurityAuthority> key)
            where TAuthority : class, ISecurityAuthority =>
            PermissionServiceRegistration.ReplaceSecurityAuthority<TAuthority>(
                services,
                key);

        public IServiceCollection
            ReplaceSecurityProfileSelector<TSelector>()
            where TSelector : class, ISecurityProfileSelector =>
            PermissionServiceRegistration
                .ReplaceSecurityProfileSelector<TSelector>(services);

        public IServiceCollection ReplaceSecurityPolicyCatalog<TCatalog>()
            where TCatalog : class, ISecurityPolicyCatalog =>
            PermissionServiceRegistration.ReplaceSecurityPolicyCatalog<TCatalog>(
                services);

        public IServiceCollection ReplaceSecurityPolicySelector<TSelector>()
            where TSelector : class, ISecurityPolicySelector =>
            PermissionServiceRegistration
                .ReplaceSecurityPolicySelector<TSelector>(services);

        public IServiceCollection
            ReplaceSecurityAuthoritySelector<TSelector>()
            where TSelector : class, ISecurityAuthoritySelector =>
            PermissionServiceRegistration
                .ReplaceSecurityAuthoritySelector<TSelector>(services);

        public IServiceCollection ReplaceApprovalBroker<TBroker>()
            where TBroker : class, IApprovalBroker =>
            PermissionServiceRegistration.ReplaceApprovalBroker<TBroker>(
                services);

        public IServiceCollection
            ReplaceApprovalHandlerDispatcher<TDispatcher>()
            where TDispatcher : class, IApprovalHandlerDispatcher =>
            PermissionServiceRegistration
                .ReplaceApprovalHandlerDispatcher<TDispatcher>(services);

        public IServiceCollection ReplaceApprovalStore<TStore>()
            where TStore : class, IApprovalStore =>
            PermissionServiceRegistration.ReplaceApprovalStore<TStore>(services);

        public IServiceCollection ReplaceSecurityGrantStore<TStore>()
            where TStore : class, ISecurityGrantStore =>
            PermissionServiceRegistration.ReplaceSecurityGrantStore<TStore>(
                services);

        public IServiceCollection ReplaceSecurityDecisionStore<TStore>()
            where TStore : class, ISecurityDecisionStore =>
            PermissionServiceRegistration.ReplaceSecurityDecisionStore<TStore>(
                services);

        public IServiceCollection ReplaceSecurityGrantIssuer<TIssuer>()
            where TIssuer : class, ISecurityGrantIssuer =>
            PermissionServiceRegistration.ReplaceSecurityGrantIssuer<TIssuer>(
                services);

        public IServiceCollection
            ReplaceSecurityAuditDispatcher<TDispatcher>()
            where TDispatcher : class, ISecurityAuditDispatcher =>
            PermissionServiceRegistration
                .ReplaceSecurityAuditDispatcher<TDispatcher>(services);
    }
}
```

The package-internal `PermissionServiceRegistration` helper applies the actual
descriptors without building or resolving a service provider.

`AddAgentPermissions` is idempotent and `TryAddKeyed`s authorities by component
key, then `TryAdd`s the engine-wide policy catalog, policy and authority
selectors, security-profile selector, approval broker, approval-handler
dispatcher, grant issuer, and audit dispatcher. It expects the required storage
contracts through explicit DI composition but registers no concrete approval,
grant, decision, or audit-outbox store. It adds a final fail-closed policy and a
headless broker path; neither is silently removed by additive registration.
Explicit replacement APIs replace singular-per-key axes. Policies, approval
handlers, and audit sinks are additive. Duplicate policy IDs or ordering cycles
fail build unless an explicit replacement names the exact `SecurityPolicyId`.

Security profiles are keyed and versioned, select an authority component key,
and are captured from each agent definition into a
`SecurityAuthorizationContext` with an immutable effective-policy snapshot
reference, exact `SecurityAuthorizationScope`, and `ExecutionIdentity`
authenticated at ingress. Changing any scope, definition, configuration,
identity, or policy field requires a new capture. Required and best-effort audit
sinks are registered with explicit delivery semantics. Runtime components
receive the selected authority or typed catalogs/selectors rather than resolving
keyed services from `IServiceProvider`.

The first-party authority, catalog, selector, and dispatchers are thread-safe
singletons. The approval broker never captures handler instances. Its typed
dispatcher activates the selected handlers inside an approval-operation scope
and disposes that scope after the attempt; singleton handlers are permitted only
when declared thread-safe. Policy lifetimes are likewise declared, and mutable
policy state may not be singleton unless thread-safe. Request/approval operation
state is scoped or durable-store state. Concurrent agents never share a mutable
grant counter. The owning DI container disposes handlers and stores once.
Cancellation stops waiting but cannot erase a persisted approval or undo a
consumed grant.

## Composition validation and unsupported behavior

Security is required for every runnable engine. Build and agent-definition
validation require one effective authority for each selected component key, one
profile selector, policy catalog, policy selector, authority selector, approval
broker, approval and grant stores with atomic idempotency semantics, decision
store, audit dispatcher, fail-closed terminal policy, `TimeProvider`, and the
required ID generators. It validates authority/profile references, effective
policy-snapshot identity/fingerprint/retention, captured profile and
configuration versions, approval scope/validity/use/revocation equality,
authenticated-channel evidence, ordering, lifetimes, maximum grant scope, cache
keys, approval routing, required audit sinks, and durability guarantees
requested by the host.

An in-memory security store may satisfy explicitly process-local grants and
inline approvals, but it cannot satisfy crash-safe grant consumption, durable
deferral, or a required durable audit path. The SQLite leaf must use an atomic
transaction for each grant-state change: registration/idempotency, revocation,
or remaining-use decrement with exact enforcement-intent receipt insertion or
reconciliation. That transaction does not include the later external effect or a
different database. Missing or weaker storage capability rejects composition
before an allow-capable authority becomes runnable.

No approval handler is a valid headless configuration only when the explicit
behavior is deny or durable defer. Unknown operations or effects, missing
context, stale versions, ambiguous resources, handler failure, unavailable
required audit, unavailable grant storage, or missing lower-boundary enforcement
deny before the effect. Unsupported caching simply remains off; unsupported
durable deferral returns a typed unavailable result rather than holding an
in-memory task. A sandbox reduces consequences but never changes a denial into
an allow.

## Trusted infrastructure and the security dependency graph

The authority's decision, grant, approval, and required-audit stores are part of
its trusted control plane. Their persistence must not request a grant from the
same authority whose decision or consumption they are recording. Grant-store
network access cannot depend on that grant store, and exporting an audit record
cannot recursively emit required audit for its own export.

The host supplies these adapters with explicit bootstrap capabilities for fixed
storage, credential, and transport targets before exposing the engine. These
capabilities are bounded by host configuration and platform access controls, are
unavailable to model-selected tools or arbitrary application operations, and
cannot accept caller-selected destinations. Adapters still enforce identity
partitioning, atomic state transitions, bounds, and redaction. Bootstrap failure
prevents readiness or future grant use; it never falls back to an unrestricted
client. A shared low-level transport may implement both paths, but its bootstrap
and ordinary operation capabilities are distinct and cannot substitute for one
another. Audit export health uses non-recursive best-effort diagnostics.

Composition validates this control-plane dependency graph separately from the
ordinary protected-effect graph. Required audit must have a durable acceptance
path before an effect; an asynchronous outbox is sufficient only when the
configured policy explicitly accepts its durability guarantee. Remote exporter
availability cannot be inferred from a successful local log call.

These contracts govern cooperating components. Trusted in-process extension code
can call platform APIs outside AgentKit; DI and a `SecurityGrant` are not a
sandbox for hostile native or managed code. A host running untrusted extensions
must put them behind an enforceable process or remote boundary and expose only
authorized capabilities there.

## Protected operations

The
[security request and grant model](../concepts/permissions-approvals-and-trust.md)
defines the authority carried across each protected boundary.

A security request describes the operation before it starts. It carries the
complete immutable execution identity plus the captured agent, optional session,
run correlation, component, stable operation kind, requested resources, effect
class, normalized input fingerprint, reason, deadline, configuration and policy
versions, and causal parent. An operation kind that requires a session fails
closed when the captured session is absent; callers never invent one.

The request is generic enough for the whole framework but strongly typed at each
caller. Examples include:

- reading, writing, enumerating, or watching files;
- opening a network connection, resolving a host, following a redirect, or
  sending data to a model provider;
- starting a process, selecting its executable, passing environment values, or
  exposing workspace paths;
- invoking local, reflected, remote, provider-native, or MCP tools;
- reading or writing memory, documents, credentials, or cross-tenant session
  state;
- delegating work, spending a shared budget, or enabling a remote capability;
  and
- loading executable hooks, plugins, or dynamic application code.

Pure internal computation does not require a ceremonial approval. Each protected
component documents the boundary at which authorization becomes mandatory.

The authority validates that every request, approval binding, and grant carries
the same `SecurityAuthorizationScope` and `ExecutionIdentity` snapshots as its
`SecurityAuthorizationContext`. Tenant or principal projections are permitted
only for indexing and redacted telemetry; they never replace the evidence,
claims, assurance, delegation chain, or identity version used for policy and
enforcement. A changed or reauthenticated identity requires a newly captured
authorization context and fresh decision.

## Authority and decisions

The security authority evaluates the request through ordered policy and returns
allow, deny, or require approval. Missing policy context, unknown operation,
unknown effect, ambiguous resource, stale configuration, evaluation failure, or
unavailable enforcement fails closed.

Allow produces a bounded security grant. The grant binds the exact captured
scope, principal, resources, effect, input fingerprint, policy version, intended
consumer, issue and expiry time, and remaining uses. A grant is evidence for one
approved scope, not a global permission flag.

The component that performs the effect validates the grant immediately before
execution. A tool cannot authorize a broad file write and then pass a different
path to the file system. Resource, argument, destination, executable, principal,
or effect changes require a new request. Low-level file, network, and process
components enforce grants again so higher-level callers cannot accidentally
bypass policy.

File-write grants bind explicit disposition, expected target evidence, exact
encoded-payload fingerprint, bounds, and atomicity. Parent-directory creation is
a separate effect and grant. Provider credential read/refresh likewise uses a
credential-source grant bound to the captured profile/account/audience and is
distinct from both provider-egress and lower-level network grants; none can be
substituted for another.

### Policy decision algebra and revocation

Evaluation considers every applicable policy in the captured deterministic
order. Any applicable hard denial or evaluation failure denies. Otherwise,
bounded allow and approval-conditioned proposals contribute intersecting scope,
expiry, use-count, and effect constraints. An empty intersection denies; every
required approval must be satisfied before a grant can issue. At least one
policy must explicitly permit the resulting scope, either directly or subject to
approval. Abstention, descriptive metadata, and the absence of a denial are not
permission.

The registered terminal fail-closed policy is a fallback for an undecided
request, not an unconditional denial that overrides every explicit allow.
Ordering determines deterministic evaluation and evidence; it is not a
last-writer-wins authority rule. A policy replacement cannot remove managed
constraints by changing registration order.

Captured policy snapshots preserve interpretation. Live revocation is separate:
the authority checks its current revocation generation at grant issue and
consumption. An emergency deny retires the relevant grants/approval bindings
before the new policy becomes effective. Retaining old snapshots for audit or
recovery never lets a delayed approval resurrect revoked authority.

## Human approval

When a decision cannot complete inline, the
[deferred-operation contract](../concepts/deferred-and-human-in-the-loop.md)
preserves correlation and lets the run settle honestly.

Require approval creates a durable approval request with a bounded, redacted
presentation of the operation, reason, resources, effects, safe input summary,
expiry, and requesting component. The approval broker routes it to one or more
configured handlers such as a command line, desktop UI, web application, remote
control plane, or durable queue.

An approval response binds to the request, operation, principal, resource and
effect scope, input fingerprint, policy version, validity period, allowed uses,
and approving identity. Human-edited inputs are new inputs: they return through
validation and policy instead of inheriting the old approval.

If no handler can answer inline, the request becomes durable deferred work and
the run settles honestly. Headless applications deny or defer according to
explicit policy; they never wait forever for a human who does not exist.

Approval transport is a trusted bootstrap boundary. Resolving an approval must
not recursively require the same unresolved approval. Transport authentication
identifies the responder but does not itself grant the requested operation.

## Hooks and security

The [hook contract](../concepts/extensions-hooks-and-middleware.md) allows
observation and stricter handling without turning extension order into
authority.

Security exposes typed hooks for safe request preparation, presentation
redaction, decision observation, approval resolution observation, and audit.
Hooks may tighten a request, add safe explanation, or request human review. They
cannot allow an operation, widen scope, mint or consume a grant, replace the
principal, or turn failure into success.

If another hook changes a protected operation after authorization, the grant is
invalid and the owning component must request authorization again. Security
evaluation is an architectural boundary, not one more mutable callback.

## Audit and revocation

Every request, winning policy, decision, approval transition, grant issue and
consumption, denial, expiry, revocation, and enforcement failure emits a
redacted audit record. Required audit persistence is part of settlement.

Revocation prevents future grant use but does not claim to undo completed side
effects. Single-use grants are consumed atomically with an enforcement-intent
receipt in the grant store's own consistency boundary. This records permission
to start the exact effect, not its completion. Cached decisions require an
explicit scope and lifetime and include every security-relevant input in their
key.

The security authority and approval broker are independently replaceable. Custom
implementations pass the same fail-closed, binding, concurrency, durability,
hook-isolation, and audit conformance suites.

Exactly one effecting boundary consumes each grant use. Coordinators validate
and forward the grant; they do not spend it before the leaf spends it again.
Separate effects, including directory lookup, store access, credential read,
provider egress, DNS, and network send, use separately issued audience-bound
grants. A consumed receipt is recovery evidence, never a reusable grant.

A non-null enforcement-intent fence binds the immediate effect to an exact
already-acquired distributed ownership generation. Its absence means the
selected action does not require a generation already held by this worker, such
as an authorized evidence read whose access contract permits that or an
acquisition that atomically seeks a new generation. The latter includes
takeover: lease-store expiry/CAS and reconciliation decide it, and a stale prior
fence never starts a new effect. Absence never characterizes storage as local or
widens authority. The selected effect contract declares when existing ownership
is mandatory, and its adapter fails closed before the effect when that fence is
missing or differs. Grant, audience, resource, fingerprint, fresh receipt, and
required-audit validation remain mandatory in both cases.

The consuming boundary records exact attempt, request fingerprint, audience,
grant use, and any required fence before the effect. Consumption and an external
effect generally do not share a transaction. A crash between consumption and
terminal recording leaves the effect unknown unless independent evidence proves
otherwise. Recovery reconciles it under the
[durable execution contract](durable-execution.md#recovery); it does not refund
the use or repeat a non-idempotent effect merely because no result was recorded.

## Related documentation

- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
- [Deferred operations and human-in-the-loop](../concepts/deferred-and-human-in-the-loop.md)
- [Hooks and extensions](extensions.md)
- [Observability and audit](observability.md)
- [File system](file-system.md)
- [Network](network.md)
- [Process execution](process-execution.md)
