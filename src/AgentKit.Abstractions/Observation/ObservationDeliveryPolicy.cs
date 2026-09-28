// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares delivery requirements and shutdown bounds for one observation sink registration.</summary>
public sealed record ObservationDeliveryPolicy
{
    /// <summary>Initializes delivery policy with validated shutdown bounds.</summary>
    /// <param name="required">When <see langword="true"/>, delivery failure prevents clean settlement.</param>
    /// <param name="flushDeadline">The positive bounded wait applied while draining required sinks during shutdown.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flushDeadline"/> is not positive when supplied.</exception>
    public ObservationDeliveryPolicy(bool required = false, TimeSpan flushDeadline = default)
    {
        if (flushDeadline != default && flushDeadline <= TimeSpan.Zero)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(flushDeadline, TimeSpan.Zero, nameof(flushDeadline));
        }

        Required = required;
        FlushDeadline = flushDeadline == default ? TimeSpan.FromSeconds(30) : flushDeadline;
    }

    /// <summary>Gets whether delivery failure prevents clean settlement.</summary>
    public bool Required { get; }

    /// <summary>Gets the bounded wait applied while draining required sinks during shutdown.</summary>
    public TimeSpan FlushDeadline { get; }
}
