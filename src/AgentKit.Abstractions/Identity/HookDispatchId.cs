// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one emission of one hook point and spans every registration
/// invoked for that emission, distinct from the per-registration
/// <see cref="HookInvocationId"/> minted for each individual execution
/// within it.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. It comes from an injected
/// <see cref="IIdentifierGenerator{TIdentifier}"/> rather than being
/// author-supplied, and correlates every hook invocation, diagnostic, and
/// activation lease belonging to one dispatch.
/// </remarks>
public readonly record struct HookDispatchId
{
    /// <summary>Initializes a new instance of the <see cref="HookDispatchId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public HookDispatchId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
