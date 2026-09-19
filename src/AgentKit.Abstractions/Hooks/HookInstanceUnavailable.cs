// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a hook instance could not be activated for this lease.</summary>
/// <typeparam name="THook">The closed hook interface requested.</typeparam>
public sealed record HookInstanceUnavailable<THook>: HookInstanceResolution<THook>
    where THook : class
{
    /// <summary>Initializes an unavailable resolution.</summary>
    /// <param name="reason">A safe, non-blank explanation of why activation failed.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is blank.</exception>
    public HookInstanceUnavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>Gets the safe explanation of why activation failed.</summary>
    public string Reason { get; }
}
