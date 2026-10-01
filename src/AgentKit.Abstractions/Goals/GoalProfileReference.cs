// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the exact captured goal profile a goal operation runs under.</summary>
/// <remarks>Resume, delayed joins, and worker replay use the profile key and version persisted on the goal rather than the agent's latest definition. This immutable value has structural equality.</remarks>
public sealed record GoalProfileReference
{
    /// <summary>Initializes a validated profile reference.</summary>
    /// <param name="key">The non-blank profile key.</param>
    /// <param name="version">The positive profile revision.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public GoalProfileReference(GoalProfileKey key, GoalProfileVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        Key = key;
        Version = version;
    }

    /// <summary>Gets the profile key.</summary>
    public GoalProfileKey Key { get; }

    /// <summary>Gets the profile revision.</summary>
    public GoalProfileVersion Version { get; }
}
