// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Resolves structured start requests using the legacy operating-system intent resolver.</summary>
internal sealed class OperatingSystemExecutableResolver: IExecutableResolver
{
    private readonly AgentProcessOptionsSnapshot _snapshot;
    private readonly OperatingSystemProcessIntentResolver _intentResolver;

    /// <summary>Initializes a resolver bound to one captured profile snapshot.</summary>
    /// <param name="snapshot">The immutable profile snapshot.</param>
    /// <param name="intentResolver">The legacy intent resolver for the same profile.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal OperatingSystemExecutableResolver(
        AgentProcessOptionsSnapshot snapshot,
        OperatingSystemProcessIntentResolver intentResolver)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intentResolver);
        _snapshot = snapshot;
        _intentResolver = intentResolver;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.processes.executable-resolver");

    /// <inheritdoc/>
    public async ValueTask<ExecutableResolutionResult> ResolveAsync(
        ProcessStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolveRequest = ProcessLegacyIntentMapping.ToResolveRequest(request, _snapshot);
        var resolution = await _intentResolver.ResolveAsync(resolveRequest, cancellationToken).ConfigureAwait(false);
        if (resolution.Status != ProcessResolutionStatus.Resolved || resolution.Intent is null)
        {
            return new ExecutableResolutionFailed(resolution.SafeMessage ?? "The executable could not be resolved.");
        }

        var intent = resolution.Intent;
        return new ExecutableResolved(new ResolvedProcessStart(
            request,
            new ResolvedExecutable(intent.AbsoluteExecutablePath, intent.ExecutableFingerprint),
            intent.AbsoluteWorkingDirectory,
            intent.EnvironmentFingerprint,
            intent.StandardInputFingerprint));
    }
}
