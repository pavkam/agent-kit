// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Wraps one registered <see cref="IToolEventSink"/> with the <see cref="ToolEventSinkRegistration"/> it was declared under.</summary>
/// <remarks>Every sink <c>AddToolEventSink</c> registers is one of these, so the dispatcher orders and identifies sinks without inspecting their types. The binding is immutable.</remarks>
public sealed class ToolEventSinkBinding
{
    /// <summary>Initializes a binding.</summary>
    /// <param name="registration">The nonnull declared registration.</param>
    /// <param name="sink">The nonnull singleton sink instance, owned by the container.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ToolEventSinkBinding(ToolEventSinkRegistration registration, IToolEventSink sink)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(sink);
        Registration = registration;
        Sink = sink;
    }

    /// <summary>Gets the declared registration.</summary>
    public ToolEventSinkRegistration Registration { get; }

    /// <summary>Gets the sink instance.</summary>
    /// <value>A borrowed singleton; the dispatcher never disposes it.</value>
    public IToolEventSink Sink { get; }
}
