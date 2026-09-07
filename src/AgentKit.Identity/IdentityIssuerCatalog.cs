// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Provides immutable keyed lookup of configured trusted issuers.</summary>
internal sealed class IdentityIssuerCatalog: IIdentityIssuerCatalog
{
    private readonly FrozenDictionary<IdentityIssuerId, IIdentityIssuer> _issuers;

    /// <summary>Creates an immutable issuer lookup and verifies every keyed descriptor.</summary>
    /// <param name="bindings">The fully materialized issuer registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is null or contains a null binding.</exception>
    /// <exception cref="ArgumentException">More than one binding uses the same issuer identifier.</exception>
    /// <exception cref="InvalidOperationException">A descriptor differs from its registration key.</exception>
    public IdentityIssuerCatalog(IEnumerable<IdentityIssuerBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var materialized = bindings.ToArray();
        foreach (var binding in materialized)
        {
            ArgumentNullException.ThrowIfNull(binding);
        }

        _issuers = materialized.ToFrozenDictionary(
            static binding => binding.Registration.IssuerId,
            static binding => ValidateBinding(binding));
    }

    /// <inheritdoc/>
    public IIdentityIssuer? Find(IdentityIssuerId issuerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId.Value, nameof(issuerId));
        return _issuers.GetValueOrDefault(issuerId);
    }

    private static IIdentityIssuer ValidateBinding(IdentityIssuerBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding.Issuer.Descriptor.Id == binding.Registration.IssuerId
            ? binding.Issuer
            : throw new InvalidOperationException("The configured identity issuer descriptor does not match its registration key.");
    }
}
