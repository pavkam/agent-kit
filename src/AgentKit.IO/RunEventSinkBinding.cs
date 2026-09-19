// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Wraps one registered <see cref="IRunEventSink"/> with the <see cref="RunEventSinkRegistration"/> it was declared under.</summary>
/// <remarks>
/// <see cref="DefaultOutputPublisher"/> receives every registered sink through the flat
/// <see cref="IEnumerable{T}"/> of <see cref="IRunEventSink"/> its constructor declares, matching the documented
/// dependency shape. This binding lets the publisher still recover each sink's stable name, delivery
/// requirement, and deterministic fan-out order without widening that constructor's public contract: every
/// sink <see cref="AgentIORegistration.AddRunEventSink{TSink}"/> registers is one of these, so the publisher
/// downcasts rather than requiring a second, parallel collection.
/// </remarks>
internal sealed class RunEventSinkBinding: IRunEventSink
{
    private readonly IRunEventSink _sink;

    /// <summary>Pairs one registration with the sink instance it declares.</summary>
    /// <param name="registration">The nonnull declared identity, delivery requirement, and fan-out order.</param>
    /// <param name="sink">The nonnull resolved sink instance.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public RunEventSinkBinding(RunEventSinkRegistration registration, IRunEventSink sink)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(sink);
        Registration = registration;
        _sink = sink;
    }

    /// <summary>Gets the declared identity, delivery requirement, and fan-out order for the wrapped sink.</summary>
    public RunEventSinkRegistration Registration { get; }

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        _sink.PublishAsync(runEvent, cancellationToken);
}
