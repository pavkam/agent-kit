// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Binds one explicitly composed store to the descriptor captured from it during composition.</summary>
/// <remarks>The binding prevents a later descriptor property read from changing the key or capabilities used by catalog reporting and selection.</remarks>
internal sealed record SessionStoreBinding
{
    /// <summary>Initializes one immutable captured store binding.</summary>
    /// <param name="store">The non-null explicitly composed session store.</param>
    /// <param name="descriptor">The non-null descriptor read once from <paramref name="store"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="descriptor"/> is null.</exception>
    internal SessionStoreBinding(ISessionStore store, SessionStoreDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(descriptor);
        Store = store;
        Descriptor = descriptor;
    }

    /// <summary>Gets the explicitly composed store implementation.</summary>
    /// <value>The non-null instance retained for selection.</value>
    internal ISessionStore Store { get; }

    /// <summary>Gets the descriptor captured exactly once at composition time.</summary>
    /// <value>The non-null immutable declaration used for every later route decision.</value>
    internal SessionStoreDescriptor Descriptor { get; }
}
