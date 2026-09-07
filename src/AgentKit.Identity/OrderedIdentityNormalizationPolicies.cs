// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Captures ordered singleton policy registrations and resolves their implementation once.</summary>
internal sealed class OrderedIdentityNormalizationPolicies
{
    /// <summary>Initializes the deterministic policy sequence.</summary>
    /// <param name="bindings">The fully materialized singleton policy bindings.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is null or contains a null binding.</exception>
    /// <exception cref="InvalidOperationException">A policy name is registered more than once.</exception>
    public OrderedIdentityNormalizationPolicies(IEnumerable<IdentityNormalizationPolicyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Policies = CreatePolicies(bindings);
    }

    /// <summary>Gets policies in their deterministic transform order.</summary>
    public ImmutableArray<IIdentityNormalizationPolicy> Policies { get; }

    /// <summary>Initializes the deterministic policy sequence and rejects duplicate stable names.</summary>
    /// <param name="bindings">The fully materialized singleton policy bindings.</param>
    /// <exception cref="InvalidOperationException">A policy name is registered more than once.</exception>
    private static ImmutableArray<IIdentityNormalizationPolicy> CreatePolicies(IEnumerable<IdentityNormalizationPolicyBinding> bindings)
    {
        var materialized = bindings.ToArray();
        foreach (var binding in materialized)
        {
            ArgumentNullException.ThrowIfNull(binding);
        }

        var ordered = materialized.OrderBy(static binding => binding.Registration.Order).ThenBy(static binding => binding.Registration.Name, StringComparer.Ordinal).ToArray();
        return ordered.Select(static registration => registration.Registration.Name).Distinct(StringComparer.Ordinal).Count() == ordered.Length
            ? [.. ordered.Select(static binding => binding.Policy)]
            : throw new InvalidOperationException("Identity normalization policy names must be unique.");
    }
}
