# Foundation contracts

**Role:** Define the provider-neutral values and narrow interfaces shared by
every AgentKit component without depending on any implementation package.

AgentKit.Abstractions is the bottom of the package graph. It owns contracts,
immutable values, typed identities, discriminated results, lifecycle events,
capability descriptors, stable errors, and deterministic primitive interfaces.
It contains no agent loop, facade, storage, transport, policy engine, service
locator, or hidden default implementation.

## Dependency rule

The package references only the BCL and stable Microsoft abstractions required
by its public contracts. It never references AgentKit, a first-party runtime
package, a provider SDK, a persistence client, or a hosting integration.

Runtime packages may all reference AgentKit.Abstractions without referencing one
another. This removes compile-time cycles, but runtime services must still obey
the constructor/factory DAG in [project structure](project-structure.md).

## Contract families

AgentKit.Abstractions owns:

- engine, agent, session, run, turn, request, message, operation, and component
  identities;
- immutable messages, content parts, artifact references, configuration
  snapshots, and capability descriptors;
- loop, context, output, provider, tool, session, security, memory, durability,
  budget, identity, hook, host-access, and observation interfaces;
- typed success, failure, denial, deferral, cancellation, limit, unsupported,
  and conflict outcomes; and
- `TimeProvider`-adjacent identifier, randomness, and content-hashing
  abstractions needed for deterministic tests and replay.

One semantic value has one owner. Feature packages must reuse the canonical
contract rather than declare a structurally similar local identity or result.

## Stable error contract

The behavioral taxonomy comes from
[error taxonomy](../concepts/error-taxonomy.md). Error values are portable;
mapping behavior remains at the boundary that understands the external failure.

```csharp
namespace AgentKit;

public readonly record struct AgentErrorCode(string Value);

public sealed record AgentError(
    AgentErrorCode Code,
    string SafeMessage,
    bool IsRetryable,
    SideEffectCertainty SideEffectCertainty,
    ErrorOrigin Origin,
    string? ExternalCode,
    OperationId? OperationId,
    ExternalRequestId? ExternalRequestId,
    TimeSpan? RetryAfter,
    ExtensionData Diagnostics);
```

Provider adapters map provider errors; tool invokers map tool errors; stores map
storage errors; file, network, process, MCP, identity, artifact, and durability
boundaries map their own failures. They preserve safe external code, request
identity, retry hints, origin, and side-effect certainty. Original exceptions
may remain non-serialized diagnostic context but never enter messages, durable
records, or unrestricted telemetry.

`RetryAfter` is a normalized non-negative delay observed using the injected
`TimeProvider`, not an ambient future wall-clock promise. When safe and useful,
`Diagnostics` retains whether the external hint was a delay, absolute date, or
provider-specific reset plus its observation time; retry policy still owns all
deadline, budget, idempotency, and side-effect checks.

There is deliberately no central `IErrorManager` that depends on every
subsystem. Each local mapper produces the common value before returning to its
caller. Retry ownership remains with the operation owner, not the error record.

## Deterministic primitives

`IIdentifierGenerator<TIdentifier>`, `IRandomizerFactory`, `IRandomizer`, and
`IContentHasher` are neutral contracts. The factory boundary prevents mutable
random state from becoming an engine-wide singleton. Runtime code never calls
ambient GUID, random, hash, clock, or delay APIs when the result affects
observable behavior.

```csharp
namespace AgentKit;

public readonly record struct ContentHashAlgorithmId(string Value);

public readonly record struct ContentHashAlgorithmVersion(string Value);

public readonly record struct CanonicalizationProfileId(string Value);

public readonly record struct CanonicalizationProfileVersion(long Value);

public readonly record struct ContentDigest(string Value);

public sealed record ContentHashProfile(
    ContentHashAlgorithmId Algorithm,
    ContentHashAlgorithmVersion AlgorithmVersion,
    CanonicalizationProfileId Canonicalization,
    CanonicalizationProfileVersion CanonicalizationVersion);

public sealed record ContentHash(
    ContentHashProfile Profile,
    ContentDigest Digest);

public readonly record struct RandomizerAlgorithmId(string Value);

public readonly record struct RandomizerAlgorithmVersion(string Value);

public readonly record struct RandomizerPurpose(string Value);

public sealed record RandomizerCreationRequest(
    OperationId OperationId,
    RandomizerPurpose Purpose);

public sealed record RandomizerDescriptor(
    RandomizerAlgorithmId Algorithm,
    RandomizerAlgorithmVersion AlgorithmVersion,
    bool IsDeterministic,
    ContentHash? SeedFingerprint);

public interface IIdentifierGenerator<TIdentifier>
    where TIdentifier : struct
{
    TIdentifier Create();
}

public interface IRandomizerFactory
{
    IRandomizer Create(RandomizerCreationRequest request);
}

public interface IRandomizer
{
    RandomizerDescriptor Descriptor { get; }

    int NextInt32(int exclusiveMaximum);

    long NextInt64(long exclusiveMaximum);

    double NextUnitDouble();

    void Fill(Span<byte> destination);
}

public interface IContentHasher
{
    ContentHashAlgorithmId Algorithm { get; }

    ContentHashAlgorithmVersion AlgorithmVersion { get; }

    ContentHash Compute(
        ReadOnlySpan<byte> canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion);

    ValueTask<ContentHash> ComputeAsync(
        Stream canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion,
        CancellationToken cancellationToken = default);
}
```

Every value above rejects an empty/default representation at a public boundary.
`ContentDigest` is the algorithm's canonical lowercase or base64url encoding,
not an arbitrary display string. A semantic owner canonicalizes its value first
and supplies the typed canonicalization profile; the hasher never guesses a
JSON, message, configuration, or store serialization. The resulting
`ContentHash` is self-describing, so changing either algorithm or
canonicalization is an explicit compatibility migration rather than a silent
equality change.

`IRandomizerFactory` is thread-safe. It creates one operation-owned randomizer
for the supplied operation and purpose. An `IRandomizer` is not shared across
parallel branches, registered directly in DI, or retained beyond that operation.
The default factory is cryptographically strong and reports
`IsDeterministic == false`; a deterministic test/replay replacement derives an
isolated stream from its configured seed plus the operation and purpose and
reports only a seed fingerprint. Raw seed material never enters a descriptor,
durable record, log, or model-visible value. `NextUnitDouble` returns a value in
`[0, 1)`, integer maxima are strictly positive, and zero-length fills are valid.

Closed identifier generators are thread-safe and create only AgentKit-owned
identities. External provider identities are preserved and never fabricated by
local generators. Exact replay records the randomizer descriptor and the policy
that consumed it; injecting a deterministic factory does not make an otherwise
nondeterministic external operation reproducible.

## Time, chronology, and retained versions

UTC timestamps record observations and external deadlines. They are not an
ordering authority: clock adjustment may put a later observation before an
earlier one. Session/event sequences and operation transitions establish order.
Within one process, elapsed durations and timeout scheduling use the injected
`TimeProvider` monotonic timestamp surface; persisted deadlines carry UTC and a
captured timeout policy. A restarted owner applies the remaining-deadline and
clock-skew policy rather than persisting a process-local timestamp counter.
Lease and grant services decide their own expiry under the consistency domain
that owns those records; a caller's clock never grants an extension.

Versioned records retain the canonical data needed to interpret or replay their
references for the advertised recovery/retention window. This may be embedded
snapshot data or a retained catalog entry; it does not require keeping every old
service instance alive. Removing a referenced version before pending work can
settle is invalid. After a supported retention window ends, unavailable history
or code yields typed incompatibility/unavailability, not substitution of the
latest behavior or fabricated replay success.

## Registration helpers

The package's ServiceExtensions file may contain typed helpers for registering
user implementations and their component dependency descriptors. It does not
offer `AddAgentAbstractions` or no-op registrations merely to satisfy a file
convention. Concrete defaults remain with the owning runtime or facade package.

The dependency-light `AgentKit` facade installs the mechanical defaults as part
of `AddAgentKit`/standalone builder creation and exposes explicit replacement:

```csharp
namespace AgentKit;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIdentifierGenerator<
            TIdentifier,
            TGenerator>()
            where TIdentifier : struct
            where TGenerator : class, IIdentifierGenerator<TIdentifier> =>
            FoundationServiceRegistration.AddIdentifierGenerator<
                TIdentifier,
                TGenerator>(services);

        public IServiceCollection ReplaceIdentifierGenerator<
            TIdentifier,
            TGenerator>()
            where TIdentifier : struct
            where TGenerator : class, IIdentifierGenerator<TIdentifier> =>
            FoundationServiceRegistration.ReplaceIdentifierGenerator<
                TIdentifier,
                TGenerator>(services);

        public IServiceCollection ReplaceRandomizerFactory<TFactory>()
            where TFactory : class, IRandomizerFactory =>
            FoundationServiceRegistration.ReplaceRandomizerFactory<TFactory>(
                services);

        public IServiceCollection ReplaceContentHasher<THasher>()
            where THasher : class, IContentHasher =>
            FoundationServiceRegistration.ReplaceContentHasher<THasher>(
                services);

        public IServiceCollection ReplaceTimeProvider(
            TimeProvider timeProvider) =>
            FoundationServiceRegistration.ReplaceTimeProvider(
                services,
                timeProvider);
    }
}
```

Facade defaults use `TryAddSingleton`: `TimeProvider.System`, one
cryptographically strong `IRandomizerFactory`, one SHA-256 `IContentHasher`, and
explicit closed `IIdentifierGenerator<TIdentifier>` registrations for every
framework-created identifier. There is no unconstrained open-generic identifier
generator and no default generator for provider/external identities.

Factories, hashers, and closed identifier generators are immutable thread-safe
singletons. Created randomizers are operation-owned values, not container-owned
services. Equivalent repeated registration is idempotent; conflicting
implementation types fail composition validation unless the exact `Replace*`
method is used. Replacement remains singleton and must pass thread-safety,
scope-validation, deterministic-vector, cancellation, and disposal-once tests.
Neither registration nor replacement builds or resolves a service provider.

Selectable implementation registration must publish enough dependency, lifetime,
key, and factory-boundary metadata for composition validation. An opaque factory
without that descriptor cannot participate in a validated agent definition.

## Evolution and testing

Public contract changes require compatibility review. Additive capability
interfaces and discriminated results are preferred over optional members that
return null or throw. AgentKit.Conformance tests observable behavior; public API
snapshots prevent accidental signature drift.

## Related architecture

- [Composition and configuration](composition-and-configuration.md)
- [Project structure](project-structure.md)
- [Testing and evaluation](testing-and-evaluation.md)
