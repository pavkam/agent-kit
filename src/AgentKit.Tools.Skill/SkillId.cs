// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Identifies one skill within a captured catalog without exposing its backing path.</summary>
public readonly record struct SkillId
{
    /// <summary>Initializes a non-blank skill identity.</summary>
    /// <param name="value">The stable host-defined value.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public SkillId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable value.</summary>
    public string Value { get; }

    /// <summary>Returns the stable value.</summary>
    public override string ToString() => Value;
}
