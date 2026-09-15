// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>An <see cref="IIdentifierGenerator{TIdentifier}"/> that always returns one known identity.</summary>
/// <typeparam name="TIdentifier">The identity value type.</typeparam>
/// <param name="value">The identity every call returns.</param>
internal sealed class FixedIdentifierGenerator<TIdentifier>(TIdentifier value): IIdentifierGenerator<TIdentifier>
    where TIdentifier : struct
{
    /// <summary>Gets the number of identities handed out.</summary>
    public int CreateCount { get; private set; }

    /// <inheritdoc/>
    public TIdentifier Create()
    {
        CreateCount++;
        return value;
    }
}
