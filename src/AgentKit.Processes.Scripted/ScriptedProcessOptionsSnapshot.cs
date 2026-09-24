// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Immutable keyed scripted process profile captured at registration time.</summary>
internal sealed record ScriptedProcessOptionsSnapshot
{
    /// <summary>Initializes one captured scripted profile snapshot.</summary>
    /// <param name="executorKey">The executor profile key.</param>
    /// <param name="executorVersion">The profile version.</param>
    /// <param name="options">The validated scripted options.</param>
    /// <param name="scenarios">The operation scenarios indexed by operation identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The workspace or scenarios are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or version is invalid.</exception>
    internal ScriptedProcessOptionsSnapshot(
        ProcessExecutorKey executorKey,
        ProcessExecutorVersion executorVersion,
        ScriptedProcessOptions options,
        ImmutableDictionary<ProcessOperationId, ScriptedProcessScenario> scenarios)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executorKey, default);
        ArgumentOutOfRangeException.ThrowIfNegative(executorVersion.Value);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorkspaceRoot);
        if (!Path.IsPathRooted(options.WorkspaceRoot))
        {
            throw new ArgumentException("The scripted workspace root must be absolute.", nameof(options));
        }

        ExecutorKey = executorKey;
        ExecutorVersion = executorVersion;
        WorkspaceRoot = Path.GetFullPath(options.WorkspaceRoot);
        Executables = options.Executables.ToImmutableDictionary(static item => item.Reference, StringComparer.Ordinal);
        AllowedEnvironmentVariableNames = options.AllowedEnvironmentVariableNames.ToImmutableHashSet(StringComparer.Ordinal);
        Scenarios = scenarios;
    }

    /// <summary>Gets the executor profile key.</summary>
    internal ProcessExecutorKey ExecutorKey { get; init; }

    /// <summary>Gets the profile version.</summary>
    internal ProcessExecutorVersion ExecutorVersion { get; init; }

    /// <summary>Gets the absolute synthetic workspace root.</summary>
    internal string WorkspaceRoot { get; init; }

    /// <summary>Gets the configured executable mappings.</summary>
    internal ImmutableDictionary<string, ScriptedExecutable> Executables { get; init; }

    /// <summary>Gets the allowed environment variable names.</summary>
    internal ImmutableHashSet<string> AllowedEnvironmentVariableNames { get; init; }

    /// <summary>Gets the configured operation scenarios.</summary>
    internal ImmutableDictionary<ProcessOperationId, ScriptedProcessScenario> Scenarios { get; init; }
}
