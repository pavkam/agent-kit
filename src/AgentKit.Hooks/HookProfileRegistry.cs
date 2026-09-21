// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using System.Collections.Concurrent;

/// <summary>Stores every hook profile registered through <c>AddHookProfile</c> or <c>ReplaceHookProfile</c>.</summary>
internal sealed class HookProfileRegistry
{
    private readonly ConcurrentDictionary<string, HookProfileOptions> _profiles = new(StringComparer.Ordinal);

    /// <summary>Initializes the registry with the built-in default profile.</summary>
    public HookProfileRegistry() =>
        _profiles[HookProfileOptions.DefaultProfileKey.Value] = new HookProfileOptions();

    /// <summary>Gets whether <paramref name="key"/> has been registered.</summary>
    /// <param name="key">The profile key to test.</param>
    /// <returns><see langword="true"/> when the key is registered.</returns>
    internal bool Contains(HookProfileKey key) =>
        key.Value is { Length: > 0 } value && _profiles.ContainsKey(value);

    /// <summary>Gets the options for a registered profile.</summary>
    /// <param name="key">The profile key.</param>
    /// <returns>The configured options.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> is not registered.</exception>
    internal HookProfileOptions GetRequired(HookProfileKey key)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        return _profiles[key.Value];
    }

    /// <summary>Registers or updates one named profile.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="configure">The configuration callback.</param>
    /// <param name="replace">
    /// When <see langword="true"/>, replaces any existing configuration for <paramref name="key"/>; otherwise merges
    /// into an existing entry.
    /// </param>
    internal void Configure(HookProfileKey key, Action<HookProfileOptions> configure, bool replace)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(configure);

        if (replace)
        {
            var options = new HookProfileOptions();
            configure(options);
            Validate(options);
            _profiles[key.Value] = options;
            return;
        }

        if (_profiles.TryGetValue(key.Value, out var existing))
        {
            configure(existing);
            Validate(existing);
            return;
        }

        var created = new HookProfileOptions();
        configure(created);
        Validate(created);
        _profiles[key.Value] = created;
    }

    private static void Validate(HookProfileOptions options)
    {
        if (options.DefaultRequestedFailureMode is { } mode)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(options.ReloadBoundary);
    }
}

/// <summary>Contributes one profile configuration during service-provider construction.</summary>
internal interface IHookProfileContributor
{
    /// <summary>Applies this contributor's profile configuration.</summary>
    /// <param name="registry">The registry being initialized.</param>
    public void Contribute(HookProfileRegistry registry);
}

/// <summary>One profile configuration registered during composition.</summary>
internal sealed class HookProfileContributor: IHookProfileContributor
{
    private readonly HookProfileKey _key;
    private readonly Action<HookProfileOptions> _configure;
    private readonly bool _replace;

    /// <summary>Initializes a new instance of the <see cref="HookProfileContributor"/> class.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="configure">The configuration callback.</param>
    /// <param name="replace">Whether this contributor replaces an existing profile.</param>
    public HookProfileContributor(HookProfileKey key, Action<HookProfileOptions> configure, bool replace)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(configure);
        _key = key;
        _configure = configure;
        _replace = replace;
    }

    /// <inheritdoc/>
    public void Contribute(HookProfileRegistry registry) => registry.Configure(_key, _configure, _replace);
}

/// <summary>Initializes <see cref="HookProfileRegistry"/> from every registered contributor.</summary>
internal sealed class HookProfileRegistryInitializer
{
    /// <summary>Initializes a new instance of the <see cref="HookProfileRegistryInitializer"/> class.</summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="contributors">Every profile contributor registered in the composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="contributors"/> is null.</exception>
    public HookProfileRegistryInitializer(HookProfileRegistry registry, IEnumerable<IHookProfileContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contributors);
        foreach (var contributor in contributors)
        {
            contributor.Contribute(registry);
        }
    }
}
