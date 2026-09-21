# Execution identity and tenancy

**Role:** Normalize trusted host authentication into an immutable execution
identity and derive narrower identities for delegated work.

The behavioral contract is defined by
[execution identity and tenancy](../concepts/execution-identity-and-tenancy.md).
Identity says who is acting. [Security](permissions-and-human-control.md)
decides what that subject may do.

## Package boundary

AgentKit.Abstractions owns tenant, principal, subject, authentication evidence,
delegation-chain, and identity-validation values. AgentKit.Identity supplies the
first-party issuer catalog, normalization, validation, and derivation policies.
Authentication integrations such as AgentKit.Identity.AspNetCore remain leaves.

The agent loop never parses credentials or reads an ambient current principal.
An ingress adapter authenticates through its host, maps the result through an
identity adapter, and passes the resulting immutable identity with admission or
run input. Credentials remain inside the leaf integration.

## Normative minimal contract shape

```csharp
namespace AgentKit;

public readonly record struct IdentityIssuerId(string Value);
public readonly record struct AuthenticationEvidenceId(string Value);
public readonly record struct AuthenticationEvidenceFingerprint(ContentHash Hash);
public readonly record struct IdentityVersion(long Value);

public enum ExecutionSubjectKind
{
    Human,
    Service,
    Workload,
    Anonymous
}

public sealed record AuthenticationEvidence(
    AuthenticationEvidenceId Id,
    IdentityIssuerId Issuer,
    string Method,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset? ExpiresAt,
    AuthenticationEvidenceFingerprint SafeFingerprint);

public sealed record ExecutionIdentity(
    TenantId TenantId,
    PrincipalId PrincipalId,
    ExecutionSubjectKind SubjectKind,
    AuthenticationEvidence Evidence,
    ImmutableArray<IdentityClaim> Claims,
    ImmutableArray<DelegationIdentityLink> DelegationChain,
    IdentityAssuranceLevel Assurance,
    IdentityVersion Version);

public sealed record IdentityAssertion(
    IdentityIssuerId Issuer,
    string ExternalSubject,
    ImmutableArray<ExternalIdentityClaim> Claims,
    AuthenticationEvidence Evidence);

public abstract record IdentityResolutionResult;

public sealed record IdentityResolved(ExecutionIdentity Identity)
    : IdentityResolutionResult;

public sealed record IdentityRejected(IdentityFailure Failure)
    : IdentityResolutionResult;

public interface IExecutionIdentityResolver
{
    ValueTask<IdentityResolutionResult> ResolveAsync(
        IdentityAssertion assertion,
        CancellationToken cancellationToken = default);
}

public interface IDelegatedIdentityDeriver
{
    ValueTask<IdentityResolutionResult> DeriveAsync(
        DelegatedIdentityRequest request,
        CancellationToken cancellationToken = default);
}
```

`IdentityAssertion` is accepted only from a trusted adapter boundary; it is not
a public wire DTO. The external subject is normalized through a versioned issuer
mapping. Raw tokens, certificates, cookies, and provider credentials never enter
these records.

`TenantId`, `PrincipalId`, and `ContentHash` are canonical shared readonly
values from [composition and configuration](composition-and-configuration.md);
the identity subsystem does not redeclare or weaken them.

## Evidence validation and delegation

The issuer maps a trusted assertion and validates the resulting authentication
evidence. These are separate operations on the issuer contract. The validation
operation accepts `AuthenticationEvidence`, an explicit `DateTimeOffset`
evaluation instant, and cancellation, and returns `IdentityValidationResult`.
The first-party validator requires this operation; missing issuer validation
never means that evidence is valid. Raw credentials remain outside both calls.

Evidence validity is exclusive at its effective end. With an explicit expiry,
clock skew may tolerate the issuer clock difference only until
`ExpiresAt + MaximumClockSkew`; equality is expired. Evidence without an expiry
is bounded by `AuthenticatedAt + MaximumEvidenceLifetime`; equality is expired.
The configured maximum lifetime also bounds explicitly expiring evidence.
Implementations compare time differences safely instead of overflowing timestamp
addition at the representable extremes.

Every normalization stage preserves the captured issuer, authentication
evidence, and mapping version. A missing, unsupported, or malformed collaborator
outcome rejects resolution. The host versions the issuer mapping together with
the configured normalization rules; changing those rules requires a new identity
version. Admission captures the resulting immutable identity. Later protected
work uses the applicable live revocation or reauthentication policy rather than
assuming an earlier validation is permanent authority.

When `AddAgentIdentity` registers `IIdentityValidationPolicy`, the facade
revalidates the supplied `ExecutionIdentity` immediately after pinned-definition
validation and before session creation, run acceptance, or queued-input
admission. Absent that registration, admission does not perform this check.
Rejection uses `AgentErrorCodes.AuthenticationFailed` on typed facade methods
and throws `AgentAdmissionRejectedException` on legacy `SendAsync` before any
session mutation occurs.

```csharp
// AgentEngineRuntime admission (conceptual)
var validation = await identityValidationPolicy.ValidateAsync(identity, cancellationToken);
if (validation is IdentityValidationRejected rejected)
{
    return AgentRunRejected<T>(AgentErrorCodes.AuthenticationFailed, rejected.Failure.SafeMessage);
}
```

Trusted channel adapters may call `Agent.RunAsync<T>` or `Agent.StreamAsync<T>` with an
`IdentityAssertion` instead of a pre-resolved `ExecutionIdentity`. The runtime opens one
scoped service scope, resolves through `IExecutionIdentityResolver`, and only then enters
the shared admission path. Unknown issuers, malformed assertions, and failed validation
map to `AgentRunRejected<T>` or `AgentRunStreamRejected<T>` with
`AgentErrorCodes.AuthenticationFailed` (or `CredentialUnavailable` when identity services
are unavailable). When `AddAgentIdentity` is absent, assertion ingress fails with
`AgentErrorCodes.MissingDependency` before session mutation.

The issuer mapping owns authenticated subject mapping and claim enrichment.
After it returns, `IIdentityNormalizationPolicy` is a narrowing boundary: each
stage may remove claims, lower assurance, or reject the candidate. It preserves
tenant, principal, subject kind, authentication evidence, mapping version, and
the complete delegation chain. Comparison is against the immediately preceding
candidate, so a later stage cannot restore a claim or assurance removed by an
earlier one. Claim renaming, value changes, and changes to issuer provenance are
new claims, not narrowing. The resolver rejects an invalid policy result before
validation or admission; it never silently repairs the result into acceptance.

A host that needs enrichment or subject remapping implements it in the trusted,
versioned issuer mapping. Delegated identities use the separate derivation
contract. This keeps filtering an authenticated identity distinct from creating
new authentication or delegation evidence.

The default delegated-identity deriver revalidates parent evidence, preserves
tenant and principal, retains issuer-provenanced claim subsets, and never raises
assurance. It preserves the complete parent chain and rejects repeated
delegation identities, malformed ancestry, or excessive depth. The identity
value can retain a different historical parent principal from an explicitly
authenticated host impersonation mechanism; the default deriver does not create
that transition. Any required impersonation or delegation authorization remains
a separate security decision.

## Migrating the reduced identity contract

The former four-argument `ExecutionIdentity` constructor carried tenant,
principal, subject kind, and opaque extensions without authentication evidence.
It is intentionally removed. Callers resolve an `IdentityAssertion` through a
trusted ingress or supply every field of the normative identity shape from
already verified host authentication. There is no compatibility constructor that
invents an issuer, evidence, assurance, or version.

Opaque extension data does not automatically become authenticated claims.
Applications retain unrelated metadata separately and map claims only through
trusted, versioned issuer policies. Test fixtures use explicit synthetic test
evidence in the non-packable shared test support project; that helper is never
part of production composition.

## Implementation and DI

```csharp
namespace AgentKit.Identity;

internal sealed class ExecutionIdentityResolver(
    IIdentityIssuerCatalog issuers,
    IEnumerable<IIdentityNormalizationPolicy> normalization,
    IIdentityValidationPolicy validation,
    TimeProvider timeProvider,
    AgentIdentityOptionsSnapshot options) : IExecutionIdentityResolver
{
}

internal sealed record AgentIdentityOptionsSnapshot(
    bool AllowAnonymous,
    int MaximumDelegationDepth,
    TimeSpan MaximumClockSkew,
    TimeSpan MaximumEvidenceLifetime);

public sealed class AgentIdentityOptions
{
    public bool AllowAnonymous { get; set; }
    public int MaximumDelegationDepth { get; set; } = 8;
    public TimeSpan MaximumClockSkew { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan MaximumEvidenceLifetime { get; set; } =
        TimeSpan.FromHours(12);
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentIdentity(
            Action<AgentIdentityOptions>? configure = null) =>
            IdentityRegistration.AddDefault(services, configure);

        public IServiceCollection AddIdentityIssuer<TIssuer>(
            IdentityIssuerRegistration registration)
            where TIssuer : class, IIdentityIssuer =>
            IdentityRegistration.AddIssuer<TIssuer>(services, registration);

        public IServiceCollection AddIdentityNormalizationPolicy<TPolicy>(
            IdentityNormalizationPolicyRegistration registration)
            where TPolicy : class, IIdentityNormalizationPolicy =>
            IdentityRegistration.AddNormalizationPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceIdentityValidationPolicy<TPolicy>()
            where TPolicy : class, IIdentityValidationPolicy =>
            IdentityRegistration.ReplaceValidationPolicy<TPolicy>(services);

        public IServiceCollection ReplaceDelegatedIdentityDeriver<TDeriver>()
            where TDeriver : class, IDelegatedIdentityDeriver =>
            IdentityRegistration.ReplaceDelegatedIdentityDeriver<TDeriver>(
                services);

        public IServiceCollection ReplaceIdentityResolver<TResolver>()
            where TResolver : class, IExecutionIdentityResolver =>
            IdentityRegistration.ReplaceResolver<TResolver>(services);
    }
}
```

Issuer registrations are additive and keyed. The resolver is scoped to an
ingress operation; immutable issuer catalogs and policies may be singleton. A
host that constructs `ExecutionIdentity` directly is itself the trusted ingress
and remains responsible for its provenance. No anonymous or process-user
identity is fabricated as a default.

`AddAgentIdentity` is idempotent and `TryAdd`s the singular resolver, delegated
identity deriver, issuer catalog, and validation policy. Normalization policies
are additive, ordered by ascending integer order and then by unique stable name
using ordinal comparison. Registration order never breaks a tie; relative
before/after constraints are not part of this contract. The mutable binding
options are validated and copied into one immutable
`AgentIdentityOptionsSnapshot`; the scoped resolver never reads an options
monitor mid-resolution. Anonymous identity is disabled by default, no issuer or
credential is invented, and a missing issuer returns a typed rejection.
Non-positive depth or lifetime, negative clock skew, duplicate issuer keys or
policy names, and invalid singleton-to-scoped captures fail composition.

## Dependency direction and cycle prevention

AgentKit.Identity depends on neutral contracts and shared diagnostic
infrastructure. It does not depend on AgentKit.IO, AgentKit.Session,
AgentKit.Permissions, or AgentKit.Goals. Ingress and hosting leaves may depend
on Identity and then call the public AgentKit facade. Downstream components
consume `ExecutionIdentity` as request data; they do not call back into the
resolver.

This produces one-way flow: host authentication → identity normalization →
admission/run request → security evaluation and protected components. Security
never authenticates, and identity never authorizes.

## Related architecture

- [Composition and configuration](composition-and-configuration.md)
- [Input and output](input-and-output.md)
- [Security and human control](permissions-and-human-control.md)
- [Sessions](sessions.md)
- [Goals and delegation](goals-and-delegation.md)
