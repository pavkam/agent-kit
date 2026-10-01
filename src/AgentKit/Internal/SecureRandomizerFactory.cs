// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

/// <summary>The default <see cref="IRandomizerFactory"/>: cryptographically strong and nondeterministic.</summary>
/// <remarks>The factory is stateless and thread-safe; every created <see cref="SecureRandomizer"/> is operation-owned.</remarks>
internal sealed class SecureRandomizerFactory: IRandomizerFactory
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public IRandomizer Create(RandomizerCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SecureRandomizer();
    }
}
