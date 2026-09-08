// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one closed service contract and its optional dependency-injection selection key.</summary>
/// <remarks>The reference is immutable declared metadata for a registration address. A <see langword="null"/> key denotes the unkeyed contract; a present key uses exact ordinal string semantics. The value carries no service instance and does not prove that any registration or factory obeys the declaration.</remarks>
public sealed record ComponentContractReference
{
    /// <summary>Initializes declared metadata for one closed service registration address.</summary>
    /// <param name="contractType">The non-null closed reference-type service contract supported by dependency injection.</param>
    /// <param name="key">The optional non-whitespace keyed selector using exact ordinal text, or <see langword="null"/> for the unkeyed contract.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contractType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="contractType"/> is not a closed reference type supported by DI, or <paramref name="key"/> is blank.</exception>
    public ComponentContractReference(Type contractType, string? key = null)
    {
        ArgumentException.ThrowIfNotComponentContractType(contractType);
        if (key is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        ContractType = contractType;
        Key = key;
    }

    /// <summary>Gets the closed contract type used to address registrations.</summary>
    /// <value>A non-null closed reference type describing the declared service contract, not a resolved instance.</value>
    public Type ContractType { get; }

    /// <summary>Gets the optional keyed-registration selector.</summary>
    /// <value>A non-whitespace exact ordinal key, or <see langword="null"/> for the unkeyed contract. Empty and whitespace-only keys are invalid.</value>
    public string? Key { get; }

    /// <summary>Creates declared keyed-registration metadata from a typed component key.</summary>
    /// <typeparam name="TContract">The reference-type service contract selected by <paramref name="key"/>.</typeparam>
    /// <param name="key">The initialized typed key whose exact ordinal text selects the declared registration.</param>
    /// <returns>An immutable reference to the keyed <typeparamref name="TContract"/> registration address.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or has blank text.</exception>
    public static ComponentContractReference From<TContract>(ComponentKey<TContract> key)
        where TContract : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return new ComponentContractReference(typeof(TContract), key.Value);
    }

    /// <summary>Creates declared metadata for the unkeyed registration of a contract.</summary>
    /// <typeparam name="TContract">The reference-type service contract.</typeparam>
    /// <returns>An immutable reference whose <see cref="Key"/> is <see langword="null"/>.</returns>
    public static ComponentContractReference Unkeyed<TContract>()
        where TContract : class => new(typeof(TContract));
}
