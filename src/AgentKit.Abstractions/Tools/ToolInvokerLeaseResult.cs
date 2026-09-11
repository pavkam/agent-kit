// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed acquired and unavailable outcomes of exact invoker acquisition.</summary>
/// <remarks>Neither outcome invokes a tool, normalizes a result, or grants authority.</remarks>
public abstract record ToolInvokerLeaseResult
{
    /// <summary>Initializes one supported acquisition outcome.</summary>
    /// <exception cref="ArgumentException">The runtime type is outside the closed outcome family.</exception>
    private protected ToolInvokerLeaseResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolInvokerAcquired or ToolInvokerUnavailable, true, "result");

    /// <summary>Copies the base state of a supported acquisition outcome.</summary>
    /// <param name="original">The nonnull original result; copying never acquires another lease.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The runtime type is outside the closed outcome family.</exception>
    protected ToolInvokerLeaseResult(ToolInvokerLeaseResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolInvokerAcquired or ToolInvokerUnavailable, true, "result");
    }
}
