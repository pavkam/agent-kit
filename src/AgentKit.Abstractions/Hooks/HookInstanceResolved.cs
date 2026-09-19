// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a hook instance was successfully activated.</summary>
/// <typeparam name="THook">The closed hook interface requested.</typeparam>
public sealed record HookInstanceResolved<THook>: HookInstanceResolution<THook>
    where THook : class
{
    /// <summary>Initializes a successful resolution.</summary>
    /// <param name="hook">The activated hook instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public HookInstanceResolved(THook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Hook = hook;
    }

    /// <summary>Gets the activated hook instance.</summary>
    public THook Hook { get; }
}
