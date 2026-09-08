// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one stable attempt to consume authority for an exact protected effect.</summary>
public readonly record struct SecurityEnforcementIntentId
{
    /// <summary>Initializes an enforcement-intent identity.</summary><param name="value">The non-empty stable attempt identifier.</param><exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SecurityEnforcementIntentId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the stable attempt identifier.</summary><value>A non-empty GUID reused only to reconcile the same exact grant consumption attempt.</value>
    public Guid Value { get; }
    /// <summary>Formats the identifier in canonical GUID form.</summary><returns>The lowercase hyphenated GUID text.</returns>
    public override string ToString() => Value.ToString("D");
}
