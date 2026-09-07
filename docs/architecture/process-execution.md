# Process execution

**Role:** Make operating-system process creation replaceable, sandboxed,
security-aware, and deterministic in tests.

Framework components and tools do not call process APIs directly. They depend on
narrow process contracts in AgentKit.Abstractions. AgentKit.Processes supplies
the first-party operating-system implementation; AgentKit.Processes.Scripted
supplies declared output, exit, timing, cancellation, and failure behavior
without starting a real process.

## Contract boundaries

The abstraction owns executable resolution, arguments, standard streams, working
directory, environment projection, sandbox profile, resource bounds,
cancellation, termination, and exit reporting. Process requests use structured
arguments rather than a shell command string unless the selected capability is
explicitly a shell.

Canonical operation identity includes the resolved executable, executable
fingerprint where available, arguments, working directory, projected environment
names and safe fingerprints, standard-input fingerprint, sandbox profile,
resource limits, and intended side-effect class. Credentials and raw secret
values never enter the request, audit, or model-visible result.

## Security and sandboxing

The
[security request and grant model](../concepts/permissions-approvals-and-trust.md)
authorizes the resolved process operation; the sandbox only constrains the
consequences of that granted operation.

Every start passes through the shared security authority after canonical
resolution and before process creation. The implementation validates and
consumes a bounded grant immediately before starting. Changes to the executable,
arguments, input, working directory, environment, sandbox, principal, or limits
require a new security request.

A sandbox limits consequences but does not grant permission. The effective
sandbox profile is derived from or narrower than the grant. If a required
sandbox, resource limit, working-directory boundary, environment isolation, or
audit sink cannot be applied, execution fails closed.

Child-process creation is not implicitly authorized. It is denied by the sandbox
or represented as a separately constrained capability. Available
[network](network.md) and [file](file-system.md) effects are intersected with
their own grants and the sandbox profile; permission to start a process is not
blanket host access.

Executable resolution that inspects a directory, binary, script interpreter,
link, or file metadata is protected observation. A pure configured alias may be
normalized before I/O; lookup and hashing use their own bounded observation
grant before the executor requests the separate start grant. Rechecking a
fingerprint before start does not alone prevent an independent actor swapping
the executable between check and creation. The selected host profile must prove
executable identity through a handle-bound start or enforceable isolation, or
reject the required guarantee. Script/interpreter chains follow the same rule.

## Lifecycle and results

Process output uses the
[typed event and backpressure rules](../concepts/streaming-and-event-protocol.md),
but process completion remains an effect-aware result rather than an assistant
message.

Process output is streamed as bounded typed events with separate standard-output
and standard-error identity. The component defines encoding, binary output,
truncation, backpressure, cancellation, graceful termination, forced kill,
orphan prevention, and disposal. A timeout outcome distinguishes a process that
never started from one that may have produced effects.

The current `OperatingSystemProcessRunner` always retains independently bounded
stdout and stderr tails. When a tail truncates and an
`IProcessOutputArtifactSink` is registered, it also retains the complete stream
up to `MaximumArtifactOutputBytes` and asks the sink to publish those exact
bytes after process settlement. `AgentKit.Artifacts` supplies the adapter: it
uses a fresh artifact authorization and two-phase prepare/finalize flow, never
reuses the process grant as storage authority, and compensates failed
publication by aborting staging. Artifact failure never repeats or rewrites the
already-settled process effect; the result keeps the tail, byte counts,
truncation flags, and a safe preservation warning.

Under the
[resilience contract](../concepts/cancellation-timeouts-and-resilience.md),
retries require idempotency or proof that the previous process did not start.
Exit codes, signals, resource usage, and side-effect certainty remain typed
facts rather than being flattened into exception text.

AgentKit.Processes uses TimeProvider for framework-owned deadlines, grace
periods, and timestamps. It does not use wall-clock sleeps for coordination.

## Contract shape

Executable resolution, sandbox construction, and execution are separate
capabilities. Shell interpretation is a distinct adapter and never an implicit
mode of the ordinary process request.

```csharp
namespace AgentKit;

public readonly record struct ProcessOperationId(Guid Value);
public readonly record struct SandboxProfileId(string Value);
public readonly record struct ProcessExecutorKey(string Value);
public readonly record struct ProcessExecutorVersion(long Value);

public sealed record ProcessStartRequest(
    ProcessOperationId Id,
    OperationId CausalOperationId,
    AgentId AgentId,
    RunId? RunId,
    ProcessExecutableReference Executable,
    ImmutableArray<ProcessArgument> Arguments,
    FileTarget WorkingDirectory,
    EnvironmentProjection Environment,
    ProcessInput? StandardInput,
    SandboxProfileId SandboxProfileId,
    ProcessResourceLimits Limits,
    ProcessEffectClass Effect);

public sealed record ResolvedProcessStart(
    ProcessStartRequest Request,
    ResolvedExecutable Executable,
    ResolvedWorkingDirectory WorkingDirectory,
    EnvironmentFingerprint EnvironmentFingerprint,
    InputFingerprint? StandardInputFingerprint);

public interface IExecutableResolver
{
    ValueTask<ExecutableResolutionResult> ResolveAsync(
        ProcessStartRequest request,
        CancellationToken cancellationToken);
}

public interface IProcessSandboxProvider
{
    SandboxDescriptor Descriptor { get; }

    ValueTask<ProcessSandboxResult> CreateAsync(
        ProcessSandboxRequest request,
        CancellationToken cancellationToken);
}

public interface IProcessExecutor
{
    ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken);
}

public interface IProcessExecutorSelector
{
    ValueTask<ProcessExecutorSelection> SelectAsync(
        ProcessExecutorKey key,
        CancellationToken cancellationToken);
}

public interface IProcessSandboxSelector
{
    ValueTask<ProcessSandboxSelection> SelectAsync(
        SandboxProfileId key,
        CancellationToken cancellationToken);
}

public interface IProcessHandle : IAsyncDisposable
{
    ProcessOperationId Id { get; }

    IAsyncEnumerable<ProcessOutputEvent> ReadOutputAsync(
        CancellationToken cancellationToken);

    Task<ProcessExitResult> Completion { get; }

    ValueTask<ProcessTerminationResult> TerminateAsync(
        ProcessTerminationRequest request,
        CancellationToken cancellationToken);
}
```

`ProcessOperationId` and causal identities are validated readonly values;
callers create operations with `IIdentifierGenerator<ProcessOperationId>`.
`SandboxProfileId` and `ProcessExecutorKey` are validated non-empty semantic
keys selected through configuration; they are not generated operation IDs.
Arguments remain an immutable sequence; no join-and-reparse step is permitted.
Environment values and standard input remain private operation content, while
canonical safe fingerprints participate in authorization and audit.
`ResolvedExecutable` records the host path, identity/fingerprint, resolution
source, and verification time. The executor resolves or verifies those facts
again immediately before creation so a swapped binary cannot inherit an old
grant.

`ProcessOutputEvent` is a closed hierarchy for standard-output bytes,
standard-error bytes, truncation/loss markers, and stream completion with a
monotonic per-process sequence. `ProcessStartResult` distinguishes a returned
handle from resolution, sandbox, limit, denial, cancellation, and typed start
failure. `ProcessExitResult` distinguishes exited, signalled, cancelled, timed
out, killed, and unknown effect certainty. Binary output never depends on a
guessed text encoding.

## First-party classes and service dependencies

| Package class                                                                             | Role and injected dependencies                                                                                                                                   |
| ----------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OperatingSystemExecutableResolver` in AgentKit.Processes                                 | Configured executable roots/aliases, file metadata contracts, safe fingerprinting, and bounded resolution                                                        |
| `OperatingSystemProcessExecutor`                                                          | Resolver, keyed sandbox provider, `ISecurityGrantStore` validation/consumption, file/network scope projection, required audit, `TimeProvider`, and output bounds |
| Platform sandbox providers                                                                | Enforce one declared `SandboxDescriptor`; remain separate because operating-system capabilities differ                                                           |
| `ScriptedProcessExecutor` and `ScriptedExecutableResolver` in AgentKit.Processes.Scripted | Deterministic start/output/exit/cancellation scenarios without host creation; use injected time and scheduling gates                                             |

The operating-system executor owns the final authority check and process
lifecycle. Its representative dependency shape is:

```csharp
namespace AgentKit.Processes;

internal sealed record AgentProcessOptionsSnapshot(
    ProcessExecutorKey ExecutorKey,
    ProcessExecutorVersion ExecutorVersion,
    ProcessExecutablePolicy ExecutablePolicy,
    ProcessEnvironmentPolicy EnvironmentPolicy,
    ProcessOutputBounds OutputBounds,
    ProcessTerminationPolicy TerminationPolicy,
    ProcessConcurrencyPolicy ConcurrencyPolicy);

internal sealed record AgentProcessExecutorBinding(
    IExecutableResolver ExecutableResolver,
    AgentProcessOptionsSnapshot Options);

internal sealed class OperatingSystemProcessExecutor(
    AgentProcessExecutorBinding profile,
    IProcessSandboxSelector sandboxes,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    ISecurityAuditDispatcher audit,
    TimeProvider timeProvider) : IProcessExecutor
{
    public ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken) =>
        ProcessStartExecution.StartAsync(
            request,
            grant,
            profile.ExecutableResolver,
            sandboxes,
            securityAuthorities,
            grants,
            audit,
            timeProvider,
            profile.Options,
            cancellationToken);
}
```

The sandbox provider produces a constrained, executor-owned sandbox handle; it
does not consume the process grant independently or start the process. Direct
executor and sandbox implementations remain supported without a required base
class.

Process-backed tools and MCP stdio transports depend only on these contracts.
They do not receive raw process APIs. The operating-system executor matches the
grant audience, executable fingerprint, arguments, working directory,
environment/input fingerprints, sandbox, principal, and resource limits, then
atomically consumes the allowed use with required audit immediately before
creation. The sandbox is created from the same resolved request and may only
tighten it. If file or network access is exposed to the child, the effective
sandbox intersects explicitly authorized file/network scopes; no missing scope
means ambient access.

If final executable or sandbox facts differ, `ISecurityAuthoritySelector`
selects only the authority and security-profile version carried by
`SecurityGrant.Authorization` for reevaluation; it never consults mutable
current-agent configuration.

## Lifetime, concurrency, and ownership

Resolvers, executors, and sandbox providers are normally thread-safe singletons.
Each successful start result contains an operation-owned handle. The caller must
asynchronously dispose it; disposal terminates and reaps a still-running owned
child using the configured graceful-then-forced policy. The container owns
singleton implementation resources, never individual child handles after they
have been returned.

`ReadOutputAsync` has one declared consumer unless a separate fan-out adapter is
used; completion may be awaited by several consumers and always returns the same
terminal result. Output uses bounded buffering and backpressure/truncation
rules. Cancelling the start token prevents further start work where possible.
Once created, cancellation triggers the explicit termination policy and reports
whether effects may have occurred; it does not merely abandon the child.

One engine may start bounded processes for concurrent agents and runs. Global
and keyed concurrency limits reserve capacity before creation. Process handles,
environment content, and mutable sandbox state never live in singleton agent
definitions or leak between run scopes. Retries belong to the calling tool/MCP
pipeline and require proof that the prior process did not start or a declared
idempotency mechanism.

## Dependency-injection registration

```csharp
namespace AgentKit.Processes;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentProcesses(
            ProcessExecutorKey key,
            Action<AgentProcessOptions> configure) =>
            ProcessRegistration.AddDefault(services, key, configure);
    }
}
```

```csharp
namespace AgentKit.Processes.Scripted;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddScriptedProcesses(
            ProcessExecutorKey key,
            Action<ScriptedProcessOptions>? configure = null) =>
            ScriptedProcessRegistration.Add(services, key, configure);
    }
}
```

```csharp
namespace AgentKit;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddProcessSandbox<TSandbox>(
            SandboxProfileId key)
            where TSandbox : class, IProcessSandboxProvider =>
            ProcessServiceRegistration.AddSandbox<TSandbox>(services, key);

        public IServiceCollection ReplaceProcessSandbox<TSandbox>(
            SandboxProfileId key)
            where TSandbox : class, IProcessSandboxProvider =>
            ProcessServiceRegistration.ReplaceSandbox<TSandbox>(services, key);

        public IServiceCollection AddExecutableResolver<TResolver>(
            ProcessExecutorKey key)
            where TResolver : class, IExecutableResolver =>
            ProcessServiceRegistration.AddExecutableResolver<TResolver>(
                services,
                key);

        public IServiceCollection ReplaceExecutableResolver<TResolver>(
            ProcessExecutorKey key)
            where TResolver : class, IExecutableResolver =>
            ProcessServiceRegistration.ReplaceExecutableResolver<TResolver>(
                services,
                key);

        public IServiceCollection AddProcessExecutor<TExecutor>(
            ProcessExecutorKey key)
            where TExecutor : class, IProcessExecutor =>
            ProcessServiceRegistration.AddExecutor<TExecutor>(services, key);

        public IServiceCollection ReplaceProcessExecutor<TExecutor>(
            ProcessExecutorKey key)
            where TExecutor : class, IProcessExecutor =>
            ProcessServiceRegistration.ReplaceExecutor<TExecutor>(services, key);

        public IServiceCollection ReplaceProcessExecutorSelector<TSelector>()
            where TSelector : class, IProcessExecutorSelector =>
            ProcessServiceRegistration.ReplaceExecutorSelector<TSelector>(
                services);

        public IServiceCollection ReplaceProcessSandboxSelector<TSelector>()
            where TSelector : class, IProcessSandboxSelector =>
            ProcessServiceRegistration.ReplaceSandboxSelector<TSelector>(
                services);
    }
}
```

The real and scripted package methods `TryAddKeyed` one resolver and executor
per `ProcessExecutorKey`. Those contracts are singular and explicitly
replaceable for that key; installing both packages does not silently replace an
existing registration. Sandbox providers are additive and keyed by stable
profile identity. Optional process backends are also keyed and selected through
a singular `IProcessExecutorSelector`; duplicate keys are invalid. Repeating
identical registration is idempotent.

Each executor key owns named `AgentProcessOptions`. Registration validates and
copies them into a package-owned immutable `AgentProcessOptionsSnapshot`
containing the key and `ProcessExecutorVersion`, then supplies that snapshot to
the keyed resolver/executor pair. Effecting services never inject unkeyed
`IOptions<T>`. The default captures options for the provider lifetime rather
than monitoring mutable values; a validated new provider or versioned profile
publication applies changes, while running process handles keep their original
snapshot. The keyed executor factory closes over that snapshot and only the
executable resolver registered under the same `ProcessExecutorKey`, constructing
`AgentProcessExecutorBinding`; the executor cannot be activated with an unkeyed
or mismatched resolver.

There is no additive chain allowed to race several executors against one
operation. Security policy, audit, hooks, and observation remain additive in
their owning packages. A shell feature registers a distinct keyed capability and
descriptor; it never changes how the ordinary executor interprets arguments.

## Build validation and unsupported behavior

Process execution is optional until a process tool, MCP stdio endpoint, dynamic
code feature, or other component declares it. Composition then validates the
selected resolver/executor, sandbox key and capabilities, executable and working
directory roots, environment allowlist, input/output/resource limits,
termination policy, security authority and grant store, required audit, scopes,
keyed options/executor-version agreement, and concurrency budget. A
configuration that requires child isolation, network denial, file projection, or
resource control fails startup when the platform sandbox cannot prove it can
enforce that requirement.

Unsupported signals, sandbox controls, binary streaming, resource counters, or
shell modes are advertised by descriptors and rejected before creation or
returned as typed unsupported results. The implementation never weakens a
sandbox, forwards the ambient environment, interprets a command string, or falls
back to an unsandboxed process. Missing, expired, consumed, mismatched, or
unauditable grants fail closed before the operating-system create call.

## Testing

The [shared conformance strategy](../concepts/testing-and-evaluation.md) applies
the same lifecycle and safety assertions to scripted and operating-system
implementations.

Shared conformance suites cover executable resolution, argument preservation,
environment filtering, working-directory escape, sandbox failure, standard
streams, output bounds, cancellation at every phase, termination escalation,
grant expiry and single-use consumption, concurrent starts, and denial before
process creation.

Unit tests use AgentKit.Processes.Scripted. Real-process coverage is explicit
integration testing against harmless fixtures in isolated temporary roots.

## Related concept specifications

- [Process execution and sandboxing](../concepts/process-execution-and-sandboxing.md)
- [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)
- [File-system access and bounds](../concepts/file-system-access-and-bounds.md)
- [Network access and egress](../concepts/network-access-and-egress.md)

## Related architecture

- [Project structure](project-structure.md)
- [Tools](tools.md)
- [Security and human control](permissions-and-human-control.md)
- [File system](file-system.md)
- [Network access](network.md)
