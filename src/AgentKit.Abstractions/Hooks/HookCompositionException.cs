// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Thrown when the set of hooks registered for one hook point cannot be
/// resolved into a single deterministic order: a duplicate
/// <see cref="HookRegistrationId"/>, an ordering cycle, a missing
/// <see cref="HookRegistrationDescriptor.DependsOn"/> target, or more than one registration declaring
/// <see cref="HookOrder.First"/> or <see cref="HookOrder.Last"/>.
/// </summary>
/// <remarks>
/// This is a composition-time failure, not a per-dispatch failure: it
/// indicates registered hooks are mutually incompatible regardless of which
/// event triggers dispatch, and is expected to surface during engine
/// composition validation rather than only on first use in production.
/// </remarks>
public sealed class HookCompositionException: Exception
{
    /// <summary>Initializes a new instance of the <see cref="HookCompositionException"/> class.</summary>
    /// <param name="message">A message describing which composition rule was violated.</param>
    public HookCompositionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookCompositionException"/> class.</summary>
    /// <param name="message">A message describing which composition rule was violated.</param>
    /// <param name="innerException">The exception that caused this composition failure, if any.</param>
    public HookCompositionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookCompositionException"/> class.</summary>
    public HookCompositionException()
    {
    }
}
