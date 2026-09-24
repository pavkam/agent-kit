// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

internal sealed class RecordingExecutableResolver: IExecutableResolver
{
    internal List<ProcessStartRequest> Requests { get; } = [];
    internal ExecutableResolutionResult? Result { get; set; }

    public ComponentId SecurityAudience { get; } = new("test.executable-resolver");

    public ValueTask<ExecutableResolutionResult> ResolveAsync(
        ProcessStartRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Result is not null)
        {
            return ValueTask.FromResult(Result);
        }

        var resolveRequest = ProcessStartBinding.ToResolveRequest(request);
        var intent = new ResolvedProcessIntent(
            resolveRequest,
            "/canonical/sh",
            new ContentHash("sha256:executable"),
            "/workspace",
            request.WorkingDirectory.Path.Value is "." or ""
                ? "/workspace"
                : $"/workspace/{request.WorkingDirectory.Path.Value}",
            new ContentHash("sha256:environment"),
            new ContentHash(ProcessSecurityBinding.FingerprintBytes([]).Value));
        var resolved = new ResolvedProcessStart(
            request,
            new ResolvedExecutable(intent.AbsoluteExecutablePath, intent.ExecutableFingerprint),
            intent.AbsoluteWorkspaceRoot,
            intent.AbsoluteWorkingDirectory,
            intent.EnvironmentFingerprint,
            intent.StandardInputFingerprint);
        return ValueTask.FromResult<ExecutableResolutionResult>(new ExecutableResolved(resolved));
    }
}

internal sealed class RecordingProcessExecutor: IProcessExecutor
{
    internal List<(ResolvedProcessStart Request, SecurityGrant Grant)> Starts { get; } = [];
    internal ProcessStartResult Result { get; set; } = new ProcessHandleStarted(new TestProcessHandle());

    public ComponentId SecurityAudience { get; } = new("test.process-executor");

    public ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken = default)
    {
        Starts.Add((request, grant));
        return ValueTask.FromResult(Result);
    }
}

internal sealed class TestProcessHandle: IProcessHandle
{
    internal ProcessExitResult Exit { get; set; } = new ProcessExited(0, SideEffectCertainty.DefinitelyPerformed);
    internal ImmutableArray<byte> StandardOutput { get; set; } = [];
    internal ImmutableArray<byte> StandardError { get; set; } = [];

    public ProcessOperationId Id { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    public Task<ProcessExitResult> Completion => Task.FromResult(Exit);

    public async IAsyncEnumerable<ProcessOutputEvent> ReadOutputAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (StandardOutput.Length > 0)
        {
            yield return new ProcessStandardOutputBytes(0, StandardOutput.AsMemory());
        }

        if (StandardError.Length > 0)
        {
            yield return new ProcessStandardErrorBytes(1, StandardError.AsMemory());
        }

        yield return new ProcessOutputStreamsCompleted(2);
        await Task.CompletedTask;
    }

    public ValueTask<ProcessTerminationResult> TerminateAsync(
        ProcessTerminationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new ProcessTerminationResult(true, SideEffectCertainty.DefinitelyPerformed));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FixedProcessExecutorSelector(
    IExecutableResolver resolver,
    IProcessExecutor executor): IProcessExecutorSelector
{
    public ValueTask<ProcessExecutorSelectionResult> SelectAsync(
        ProcessExecutorKey key,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ProcessExecutorSelectionResult>(
            new ProcessExecutorSelected(new ProcessExecutorKey("test"), resolver, executor));
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return !allow
            ? ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")))
            : ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityGrant(
                    new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
                    request.Id,
                    request.Scope,
                    request.Identity,
                    request.Audience,
                    request.Kind,
                    request.Effect,
                    request.Resources,
                    request.InputFingerprint,
                    new SecurityPolicyVersion(1),
                    new SecurityRevocationVersion(1),
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddMinutes(5),
                    1)));
    }
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedProcessOperationIdGenerator: IIdentifierGenerator<ProcessOperationId>
{
    public ProcessOperationId Create() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
