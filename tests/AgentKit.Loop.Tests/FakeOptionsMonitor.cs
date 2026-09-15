// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>A minimal <see cref="IOptionsMonitor{TOptions}"/> that always returns the same value, regardless of name.</summary>
/// <typeparam name="TOptions">The options type.</typeparam>
internal sealed class FakeOptionsMonitor<TOptions>(TOptions value): IOptionsMonitor<TOptions>
    where TOptions : class
{
    /// <inheritdoc/>
    public TOptions CurrentValue => value;

    /// <inheritdoc/>
    public TOptions Get(string? name) => value;

    /// <inheritdoc/>
    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
