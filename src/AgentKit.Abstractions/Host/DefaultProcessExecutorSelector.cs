// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Frozen;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Selects keyed process executors from explicit DI registrations.</summary>
public sealed class DefaultProcessExecutorSelector: IProcessExecutorSelector
{
    private readonly IServiceProvider _provider;
    private readonly FrozenSet<ProcessExecutorKey> _keys;

    /// <summary>Initializes the selector from captured executor registrations.</summary>
    /// <param name="provider">The root service provider used to resolve keyed services.</param>
    /// <param name="registrations">The registered executor profile declarations.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DefaultProcessExecutorSelector(
        IServiceProvider provider,
        IEnumerable<ProcessExecutorRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registrations);
        _provider = provider;
        _keys = registrations.Select(static registration => registration.Key).ToFrozenSet();
    }

    /// <inheritdoc/>
    public ValueTask<ProcessExecutorSelectionResult> SelectAsync(
        ProcessExecutorKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_keys.Contains(key))
        {
            return ValueTask.FromResult<ProcessExecutorSelectionResult>(new ProcessExecutorMissing(key));
        }

        var serviceKey = key.Value;
        return ValueTask.FromResult<ProcessExecutorSelectionResult>(new ProcessExecutorSelected(
            key,
            _provider.GetRequiredKeyedService<IExecutableResolver>(serviceKey),
            _provider.GetRequiredKeyedService<IProcessExecutor>(serviceKey)));
    }
}
