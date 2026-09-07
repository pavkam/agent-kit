// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one specific dispatch of one hook against one
/// <see cref="AgentHookEventArgs"/> instance, distinct from the stable
/// <see cref="HookId"/> of the hook implementation being invoked.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. Diagnostics correlate a specific invocation's duration,
/// outcome, and changed-field report using this identity, so repeated
/// invocations of the same <see cref="HookId"/> across different dispatches
/// remain individually traceable.
/// </remarks>
public readonly record struct HookInvocationId
{
    /// <summary>Initializes a new instance of the <see cref="HookInvocationId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public HookInvocationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
