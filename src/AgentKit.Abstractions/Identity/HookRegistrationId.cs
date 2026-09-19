// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one configured hook registration within a profile, stable
/// across equivalent catalog captures.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. Unlike <see cref="HookInvocationId"/>, which is minted
/// fresh for every individual execution, a <see cref="HookRegistrationId"/>
/// is chosen once by whoever registers a hook and stays stable across
/// equivalent catalog captures for the same profile. Ordering constraints
/// (before, after, and depends-on edges) and duplicate-registration
/// detection reference this identity.
/// </remarks>
public readonly record struct HookRegistrationId
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public HookRegistrationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
