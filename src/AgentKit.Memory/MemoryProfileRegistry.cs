// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Collections.Concurrent;

/// <summary>Accumulates every memory profile registered through <c>AddMemoryProfile</c> or <c>ReplaceMemoryProfile</c>.</summary>
/// <remarks>The registry is a composition-time accumulator, not a persistence adapter. It is populated once from ordered contributors while the provider is built and is then read by <see cref="MemoryProfileCatalog"/>. Mutation after the provider is built is not part of its contract.</remarks>
internal sealed class MemoryProfileRegistry
{
    private readonly ConcurrentDictionary<string, MemoryProfileOptions> _profiles = new(StringComparer.Ordinal);

    /// <summary>Gets the registered profile options in registration-independent key order.</summary>
    /// <returns>Each key with its accumulated options.</returns>
    internal IReadOnlyList<KeyValuePair<MemoryProfileKey, MemoryProfileOptions>> Profiles =>
        [.. _profiles.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => KeyValuePair.Create(new MemoryProfileKey(pair.Key), pair.Value))];

    /// <summary>Registers or updates one named profile.</summary>
    /// <param name="key">The non-blank profile key.</param>
    /// <param name="configure">The configuration callback applied to the profile options.</param>
    /// <param name="replace">When <see langword="true"/>, discards earlier configuration for the key and applies the callback to fresh options.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    internal void Configure(MemoryProfileKey key, Action<MemoryProfileOptions> configure, bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        if (!replace && _profiles.TryGetValue(key.Value, out var existing))
        {
            configure(existing);
            return;
        }

        var options = new MemoryProfileOptions();
        configure(options);
        _profiles[key.Value] = options;
    }
}
