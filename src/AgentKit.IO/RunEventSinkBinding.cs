// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Wraps one registered <see cref="IRunEventSink"/> with the <see cref="RunEventSinkRegistration"/> it was declared under.</summary>
/// <remarks>
/// <see cref="DefaultOutputPublisher"/> receives every registered sink through the flat
/// <see cref="IEnumerable{T}"/> of <see cref="IRunEventSink"/> its constructor declares, matching the documented
/// dependency shape. This binding lets the publisher still recover each sink's stable name, delivery
/// requirement, and deterministic fan-out order without widening that constructor's public contract: every
/// sink <c>AddRunEventSink</c> registers is one of these, so the publisher
/// downcasts rather than requiring a second, parallel collection.
/// </remarks>
internal sealed class RunEventSinkBinding: IRunEventSink
{
    /// <summary>Pairs one registration with the sink instance it declares.</summary>
    /// <param name="registration">The nonnull declared identity, delivery requirement, and fan-out order.</param>
    /// <param name="sink">The nonnull resolved sink instance.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public RunEventSinkBinding(RunEventSinkRegistration registration, IRunEventSink sink)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(sink);
        Registration = registration;
        Inner = sink;
    }

    /// <summary>Gets the declared identity, delivery requirement, and fan-out order for the wrapped sink.</summary>
    public RunEventSinkRegistration Registration { get; }

    /// <summary>Gets the wrapped sink instance, so shutdown can reach an optional <see cref="IFlushableRunEventSink"/> capability.</summary>
    internal IRunEventSink Inner { get; }

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        Inner.PublishAsync(runEvent, cancellationToken);
}
