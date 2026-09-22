// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed resolved and unresolved outcomes of one <see cref="IToolResolver"/> attempt.</summary>
/// <remarks>Neither outcome authorizes invocation, validates arguments, or invokes the tool.</remarks>
public abstract record ToolResolutionResult
{
    /// <summary>Initializes one of the two supported resolution outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed resolution family.</exception>
    private protected ToolResolutionResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolCallResolved or ToolCallUnresolved, true, "result");

    /// <summary>Copies the base state of a supported immutable resolution outcome.</summary>
    /// <param name="original">The nonnull original result; copying never acquires another invoker lease.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed resolution family.</exception>
    protected ToolResolutionResult(ToolResolutionResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolCallResolved or ToolCallUnresolved, true, "result");
    }
}
