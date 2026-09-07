// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// An immutable <see cref="IBudgetDimensionCatalog"/> built once from every
/// additively registered <see cref="BudgetDimensionDescriptor"/>.
/// </summary>
/// <remarks>
/// This type is a thread-safe process singleton reflecting the descriptors
/// registered at composition time. Duplicate registrations for the same
/// <see cref="BudgetDimension"/> fail construction; use
/// <c>ReplaceBudgetDimension</c> to override a specific key intentionally.
/// </remarks>
internal sealed class InMemoryBudgetDimensionCatalog: IBudgetDimensionCatalog
{
    private readonly ImmutableDictionary<BudgetDimension, BudgetDimensionDescriptor> _descriptors;

    /// <summary>Initializes a new instance of the <see cref="InMemoryBudgetDimensionCatalog"/> class.</summary>
    /// <param name="descriptors">Every additively registered dimension descriptor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="descriptors"/> contains more than one descriptor for the same dimension.
    /// </exception>
    public InMemoryBudgetDimensionCatalog(IEnumerable<BudgetDimensionDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var builder = ImmutableDictionary.CreateBuilder<BudgetDimension, BudgetDimensionDescriptor>();
        foreach (var descriptor in descriptors)
        {
            if (!builder.TryAdd(descriptor.Dimension, descriptor))
            {
                throw new ArgumentException(
                    $"More than one descriptor is registered for dimension '{descriptor.Dimension}'.",
                    nameof(descriptors));
            }
        }

        _descriptors = builder.ToImmutable();
    }

    /// <inheritdoc/>
    public bool TryGet(BudgetDimension dimension, [NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor) =>
        _descriptors.TryGetValue(dimension, out descriptor);
}
