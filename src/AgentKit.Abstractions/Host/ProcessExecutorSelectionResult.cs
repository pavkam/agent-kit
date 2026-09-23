// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for process executor selection outcomes.</summary>
public abstract record ProcessExecutorSelectionResult
{
    /// <summary>Initializes the base selection outcome.</summary>
    private protected ProcessExecutorSelectionResult()
    {
    }
}

/// <summary>One registered executor profile was selected.</summary>
public sealed record ProcessExecutorSelected: ProcessExecutorSelectionResult
{
    /// <summary>Initializes a successful selection.</summary>
    /// <param name="key">The selected profile key.</param>
    /// <param name="resolver">The executable resolver registered under the key.</param>
    /// <param name="executor">The executor registered under the key.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ProcessExecutorSelected(
        ProcessExecutorKey key,
        IExecutableResolver resolver,
        IProcessExecutor executor)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(executor);
        Key = key;
        Resolver = resolver;
        Executor = executor;
    }

    /// <summary>Gets the selected profile key.</summary>
    public ProcessExecutorKey Key { get; init; }

    /// <summary>Gets the executable resolver registered under the key.</summary>
    public IExecutableResolver Resolver { get; init; }

    /// <summary>Gets the executor registered under the key.</summary>
    public IProcessExecutor Executor { get; init; }
}

/// <summary>No executor profile is registered for the requested key.</summary>
/// <param name="Key">The missing profile key.</param>
public sealed record ProcessExecutorMissing(ProcessExecutorKey Key): ProcessExecutorSelectionResult;
