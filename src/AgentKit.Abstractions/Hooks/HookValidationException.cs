// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Thrown when <see cref="AgentHookEventArgs.Validate"/> rejects the state a
/// hook left its writable properties in, or when a hook attempts to write a
/// value <see cref="AgentHookEventArgs.Validate"/> would never accept.
/// </summary>
/// <remarks>
/// The dispatcher fails the entire dispatch when this is thrown: a hook
/// point's owning operation never proceeds against event arguments a later
/// stage has rejected as invalid, and no subsequent hook is invoked.
/// </remarks>
public sealed class HookValidationException: Exception
{
    /// <summary>Initializes a new instance of the <see cref="HookValidationException"/> class.</summary>
    /// <param name="message">A message describing which invariant was violated.</param>
    public HookValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookValidationException"/> class.</summary>
    /// <param name="message">A message describing which invariant was violated.</param>
    /// <param name="innerException">The exception that caused this validation failure, if any.</param>
    public HookValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HookValidationException"/> class.</summary>
    public HookValidationException()
    {
    }
}
