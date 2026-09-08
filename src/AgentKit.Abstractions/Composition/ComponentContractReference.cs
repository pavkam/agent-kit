// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one closed service contract and its optional dependency-injection selection key.</summary>
/// <remarks>The reference is a value description of a registration address. It never carries a service instance and is safe to retain or compare across threads.</remarks>
public sealed record ComponentContractReference
{
    /// <summary>Initializes a reference to a closed service contract and an optional keyed registration.</summary>
    /// <param name="contractType">The closed service contract type.</param>
    /// <param name="key">The optional non-blank DI service key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contractType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="contractType"/> is open, or <paramref name="key"/> is blank.</exception>
    public ComponentContractReference(Type contractType, string? key = null)
    {
        ArgumentException.ThrowIfNotClosedType(contractType);
        if (key is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        ContractType = contractType;
        Key = key;
    }

    /// <summary>Gets the closed contract type used to find registrations.</summary>
    public Type ContractType { get; }

    /// <summary>Gets the optional keyed-registration selector; <see langword="null"/> denotes the unkeyed contract.</summary>
    public string? Key { get; }

    /// <summary>Creates a keyed reference from a typed component key.</summary>
    /// <typeparam name="TContract">The reference-type contract selected by <paramref name="key"/>.</typeparam>
    /// <param name="key">The initialized typed selection key.</param>
    /// <returns>A reference to the keyed <typeparamref name="TContract"/> registration.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or has blank text.</exception>
    public static ComponentContractReference From<TContract>(ComponentKey<TContract> key)
        where TContract : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return new ComponentContractReference(typeof(TContract), key.Value);
    }

    /// <summary>Creates a reference to the unkeyed registration of a contract.</summary>
    /// <typeparam name="TContract">The reference-type service contract.</typeparam>
    /// <returns>A reference whose key is <see langword="null"/>.</returns>
    public static ComponentContractReference Unkeyed<TContract>()
        where TContract : class => new(typeof(TContract));
}
