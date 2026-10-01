// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>The result of one memory hook dispatch.</summary>
/// <typeparam name="TEventArgs">The event arguments of the dispatched point.</typeparam>
internal readonly struct MemoryHookOutcome<TEventArgs>
    where TEventArgs : AgentHookEventArgs
{
    /// <summary>Initializes an outcome.</summary>
    /// <param name="args">The completed arguments, or null when the dispatch failed.</param>
    /// <param name="failed">Whether a hook failed the operation.</param>
    internal MemoryHookOutcome(TEventArgs? args, bool failed)
    {
        Args = args;
        Failed = failed;
    }

    /// <summary>Gets the completed arguments, or null when <see cref="Failed"/>.</summary>
    internal TEventArgs? Args { get; }

    /// <summary>Gets whether a hook failed the operation.</summary>
    internal bool Failed { get; }
}
