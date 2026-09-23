// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Immutable keyed process profile captured at registration time.</summary>
internal sealed record AgentProcessOptionsSnapshot
{
    /// <summary>Initializes one captured profile snapshot.</summary>
    /// <param name="executorKey">The executor profile key.</param>
    /// <param name="executorVersion">The profile version.</param>
    /// <param name="operatingSystem">The validated operating-system options.</param>
    /// <param name="defaultWorkspaceAccess">The default workspace access.</param>
    /// <param name="defaultChildPolicy">The default child-process policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="operatingSystem"/> is null.</exception>
    /// <exception cref="ArgumentException">The workspace root is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or enum is invalid.</exception>
    internal AgentProcessOptionsSnapshot(
        ProcessExecutorKey executorKey,
        ProcessExecutorVersion executorVersion,
        OperatingSystemProcessOptions operatingSystem,
        ProcessWorkspaceAccess defaultWorkspaceAccess,
        ProcessChildPolicy defaultChildPolicy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executorKey, default);
        ArgumentOutOfRangeException.ThrowIfNegative(executorVersion.Value);
        ArgumentNullException.ThrowIfNull(operatingSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatingSystem.RootDirectory);
        ArgumentOutOfRangeException.ThrowIfUndefined(defaultWorkspaceAccess);
        ArgumentOutOfRangeException.ThrowIfUndefined(defaultChildPolicy);
        ExecutorKey = executorKey;
        ExecutorVersion = executorVersion;
        OperatingSystem = operatingSystem;
        DefaultWorkspaceAccess = defaultWorkspaceAccess;
        DefaultChildPolicy = defaultChildPolicy;
    }

    /// <summary>Gets the executor profile key.</summary>
    internal ProcessExecutorKey ExecutorKey { get; init; }

    /// <summary>Gets the profile version.</summary>
    internal ProcessExecutorVersion ExecutorVersion { get; init; }

    /// <summary>Gets the validated operating-system options.</summary>
    internal OperatingSystemProcessOptions OperatingSystem { get; init; }

    /// <summary>Gets the default workspace access.</summary>
    internal ProcessWorkspaceAccess DefaultWorkspaceAccess { get; init; }

    /// <summary>Gets the default child-process policy.</summary>
    internal ProcessChildPolicy DefaultChildPolicy { get; init; }
}
