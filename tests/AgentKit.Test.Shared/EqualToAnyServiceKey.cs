// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Models a foreign DI key whose equality must not influence exact typed-key registration changes.</summary>
public sealed class EqualToAnyServiceKey
{
    private int _comparisons;
    /// <summary>Gets the number of custom equality invocations observed by the fake.</summary>
    public int Comparisons => Volatile.Read(ref _comparisons);
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        _ = Interlocked.Increment(ref _comparisons);
        return true;
    }
    /// <inheritdoc/>
    public override int GetHashCode() => 0;
}
