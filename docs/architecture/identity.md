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
are additive and deterministically ordered. The mutable binding options are
validated and copied into one immutable `AgentIdentityOptionsSnapshot`; the
scoped resolver never reads an options monitor mid-resolution. Anonymous
identity is disabled by default, no issuer or credential is invented, and a
missing issuer returns a typed rejection. Non-positive depth or lifetime,
negative clock skew, duplicate issuer keys, policy-order cycles, and invalid
singleton-to-scoped captures fail composition.

## Dependency direction and cycle prevention

AgentKit.Identity depends only on AgentKit.Abstractions. It does not depend on
AgentKit.IO, AgentKit.Session, AgentKit.Permissions, or AgentKit.Goals. Ingress
and hosting leaves may depend on Identity and then call the public AgentKit
facade. Downstream components consume `ExecutionIdentity` as request data; they
do not call back into the resolver.

This produces one-way flow: host authentication → identity normalization →
admission/run request → security evaluation and protected components. Security
never authenticates, and identity never authorizes.

## Related architecture

- [Composition and configuration](composition-and-configuration.md)
- [Input and output](input-and-output.md)
- [Security and human control](permissions-and-human-control.md)
- [Sessions](sessions.md)
- [Goals and delegation](goals-and-delegation.md)
