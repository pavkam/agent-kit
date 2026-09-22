// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers one validated call to the caller.</summary>
public sealed record ToolCallValidated: ToolArgumentValidationResult
{
    /// <summary>Wraps a validated call.</summary>
    /// <param name="call">The nonnull call whose arguments satisfied the resolved descriptor's declared schema.</param>
    /// <exception cref="ArgumentNullException"><paramref name="call"/> is null.</exception>
    public ToolCallValidated(ValidatedToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        Call = call;
    }

    /// <summary>Gets the validated call.</summary>
    /// <value>The final canonical arguments and their fingerprint, bound to the resolved descriptor.</value>
    public ValidatedToolCall Call { get; }
}
