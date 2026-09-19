// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The activation lifetime a hook registration declares for its own implementation instance.</summary>
/// <remarks>
/// This is the registration's own declared lifetime, independent of the surrounding activation-lease scope: an
/// immutable, thread-safe hook implementation may declare <see cref="Singleton"/> even though every lease still
/// resolves it through the hook instance factory, while a mutable hook declares <see cref="Scoped"/> or
/// <see cref="Transient"/> so each lease, or each resolution within a lease, gets its own instance.
/// </remarks>
public enum HookLifetime
{
    /// <summary>One shared instance is reused across every activation lease for the registration's lifetime.</summary>
    Singleton,

    /// <summary>One instance is created per activation lease and reused for every invocation within it.</summary>
    Scoped,

    /// <summary>A new instance is created for every invocation, even within the same activation lease.</summary>
    Transient
}
