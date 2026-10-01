// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Maps spec process start shapes onto the resolved intents used by enforcement and sandboxes.</summary>
internal static class ProcessIntentMapping
{
    /// <summary>Builds a resolve request from one start request and profile defaults.</summary>
    /// <param name="start">The structured start request.</param>
    /// <param name="snapshot">The captured profile defaults.</param>
    /// <returns>The resolve request.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static ProcessResolveRequest ToResolveRequest(ProcessStartRequest start, AgentProcessOptionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(snapshot);
        var workingDirectory = ToWorkingDirectory(start.WorkingDirectory);
        var environment = start.Environment.Variables;
        var standardInput = ToStandardInput(start.StandardInput);
        var readOnlyRoots = snapshot.OperatingSystem.ReadOnlyToolchainRoots
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new ProcessReadOnlyRoot(item.Key, item.Value))
            .ToImmutableArray();
        return new ProcessResolveRequest(
            start.Id,
            start.Executable.Value,
            [.. start.Arguments.Select(static argument => argument.Value)],
            workingDirectory,
            environment,
            standardInput,
            start.SandboxProfileId,
            snapshot.DefaultWorkspaceAccess,
            ToSideEffectClass(start.Effect),
            snapshot.DefaultChildPolicy,
            start.Limits)
        {
            ReadOnlyRoots = readOnlyRoots,
        };
    }

    /// <summary>Builds a resolved intent from resolved start facts and profile defaults.</summary>
    /// <param name="resolved">The resolved start facts.</param>
    /// <param name="snapshot">The captured profile defaults.</param>
    /// <returns>The resolved intent.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static ResolvedProcessIntent ToResolvedProcessIntent(
        ResolvedProcessStart resolved,
        AgentProcessOptionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = snapshot;
        return ProcessStartBinding.ToResolvedProcessIntent(resolved);
    }

    private static FileSystemPath? ToWorkingDirectory(FileTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var value = target.Path.Value;
        return value is "." or ""
            ? null
            : new FileSystemPath(value);
    }

    private static ImmutableArray<byte> ToStandardInput(ProcessInput? standardInput) =>
        standardInput switch
        {
            null => [],
            _ => [.. standardInput.Payload.Span],
        };

    private static ProcessSideEffectClass ToSideEffectClass(ProcessEffectClass effect) => effect switch
    {
        ProcessEffectClass.ReadOnlyObservation => ProcessSideEffectClass.ReadOnly,
        ProcessEffectClass.WorkspaceMutation => ProcessSideEffectClass.WorkspaceMutation,
        ProcessEffectClass.ExternalEffect => ProcessSideEffectClass.ExternalOrUnknown,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "The effect class is undefined."),
    };
}
