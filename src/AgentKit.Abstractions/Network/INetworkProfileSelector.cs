// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one registered network resolver/transport pair for a profile key.</summary>
public interface INetworkProfileSelector
{
    /// <summary>Resolves the profile registered under <paramref name="key"/>.</summary>
    /// <param name="key">The profile key authored by the application.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A closed selection outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    public ValueTask<NetworkProfileSelectionResult> SelectAsync(
        NetworkProfileKey key,
        CancellationToken cancellationToken = default);
}
