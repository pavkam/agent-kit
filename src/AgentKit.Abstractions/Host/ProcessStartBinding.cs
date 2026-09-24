// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Maps structured process start facts onto legacy authorization intents.</summary>
public static class ProcessStartBinding
{
    /// <summary>Builds the canonical resolved intent used for process security evidence.</summary>
    /// <param name="resolved">The resolved start facts.</param>
    /// <returns>The legacy intent shape bound to the same executable and workspace evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolved"/> is null.</exception>
    public static ResolvedProcessIntent ToResolvedProcessIntent(ResolvedProcessStart resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        var request = ToResolveRequest(resolved.Request);
        return new ResolvedProcessIntent(
            request,
            resolved.Executable.AbsolutePath,
            resolved.Executable.Fingerprint,
            resolved.AbsoluteWorkspaceConfinementRoot,
            resolved.WorkingDirectory,
            resolved.EnvironmentFingerprint,
            resolved.StandardInputFingerprint ?? ProcessSecurityBinding.FingerprintBytes([]));
    }

    /// <summary>Builds a legacy resolve request from one structured start request.</summary>
    /// <param name="start">The structured start request.</param>
    /// <returns>The legacy resolve request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
    public static ProcessResolveRequest ToResolveRequest(ProcessStartRequest start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new ProcessResolveRequest(
            start.Id,
            start.Executable.Value,
            [.. start.Arguments.Select(static argument => argument.Value)],
            ToWorkingDirectory(start.WorkingDirectory),
            start.Environment.Variables,
            ToStandardInput(start.StandardInput),
            start.SandboxProfileId,
            ToWorkspaceAccess(start.Effect),
            ToSideEffectClass(start.Effect),
            ProcessChildPolicy.AllowSandboxed,
            start.Limits);
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

    private static ProcessWorkspaceAccess ToWorkspaceAccess(ProcessEffectClass effect) => effect switch
    {
        ProcessEffectClass.ReadOnlyObservation => ProcessWorkspaceAccess.ReadOnly,
        ProcessEffectClass.WorkspaceMutation => ProcessWorkspaceAccess.ReadWrite,
        ProcessEffectClass.ExternalEffect => ProcessWorkspaceAccess.ReadWrite,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "The effect class is undefined."),
    };

    private static ProcessSideEffectClass ToSideEffectClass(ProcessEffectClass effect) => effect switch
    {
        ProcessEffectClass.ReadOnlyObservation => ProcessSideEffectClass.ReadOnly,
        ProcessEffectClass.WorkspaceMutation => ProcessSideEffectClass.WorkspaceMutation,
        ProcessEffectClass.ExternalEffect => ProcessSideEffectClass.ExternalOrUnknown,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "The effect class is undefined."),
    };
}
