// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Resolves the registered <see cref="BudgetDimensionDescriptor"/> for a
/// <see cref="BudgetDimension"/>.
/// </summary>
/// <remarks>
/// An implementation is an immutable, thread-safe process singleton
/// reflecting the dimensions registered at composition time. It never
/// fabricates a descriptor for an unregistered dimension.
/// </remarks>
public interface IBudgetDimensionCatalog
{
    /// <summary>Attempts to resolve the descriptor registered for <paramref name="dimension"/>.</summary>
    /// <param name="dimension">The dimension to resolve.</param>
    /// <param name="descriptor">The resolved descriptor, when one is registered.</param>
    /// <returns><see langword="true"/> if a descriptor is registered for <paramref name="dimension"/>.</returns>
    public bool TryGet(BudgetDimension dimension, [NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor);
}
