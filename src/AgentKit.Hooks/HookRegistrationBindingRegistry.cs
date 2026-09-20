// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Collects every <see cref="HookRegistrationBinding"/> registered during composition.</summary>
internal sealed class HookRegistrationBindingRegistry
{
    private readonly List<HookRegistrationBinding> _bindings = [];

    /// <summary>Gets the captured bindings.</summary>
    internal IReadOnlyList<HookRegistrationBinding> Bindings => _bindings;

    /// <summary>Adds one binding to the registry.</summary>
    /// <param name="binding">The binding to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    internal void Add(HookRegistrationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings.Add(binding);
    }
}

/// <summary>Contributes one binding to <see cref="HookRegistrationBindingRegistry"/> during service-provider construction.</summary>
internal interface IHookRegistrationBindingContributor
{
    /// <summary>Adds this contributor's binding to the registry.</summary>
    /// <param name="registry">The registry being initialized.</param>
    public void Contribute(HookRegistrationBindingRegistry registry);
}

/// <summary>One typed registration contributor so dependency injection can distinguish multiple bindings.</summary>
/// <typeparam name="TImplementation">The hook implementation type this contributor registers.</typeparam>
internal sealed class HookRegistrationBindingContributor<TImplementation>: IHookRegistrationBindingContributor
    where TImplementation : class
{
    private readonly HookRegistrationBinding _binding;

    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBindingContributor{TImplementation}"/> class.</summary>
    /// <param name="binding">The binding to contribute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    public HookRegistrationBindingContributor(HookRegistrationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _binding = binding;
    }

    /// <inheritdoc/>
    public void Contribute(HookRegistrationBindingRegistry registry) => registry.Add(_binding);
}

/// <summary>Initializes <see cref="HookRegistrationBindingRegistry"/> from every registered contributor.</summary>
internal sealed class HookRegistrationBindingRegistryInitializer
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBindingRegistryInitializer"/> class.</summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="contributors">Every binding contributor registered in the composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="contributors"/> is null.</exception>
    public HookRegistrationBindingRegistryInitializer(
        HookRegistrationBindingRegistry registry,
        IEnumerable<IHookRegistrationBindingContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contributors);
        foreach (var contributor in contributors)
        {
            contributor.Contribute(registry);
        }
    }
}
