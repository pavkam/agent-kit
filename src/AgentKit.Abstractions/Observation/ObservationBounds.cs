// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares size limits applied during observation capture and redaction.</summary>
public sealed record ObservationBounds
{
    /// <summary>Initializes bounds with validated limits.</summary>
    /// <param name="maximumBytesPerField">The positive maximum bytes allowed per captured field after classification.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytesPerField"/> is not positive.</exception>
    public ObservationBounds(int maximumBytesPerField)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytesPerField, nameof(maximumBytesPerField));
        MaximumBytesPerField = maximumBytesPerField;
    }

    /// <summary>Gets the positive maximum bytes allowed per captured field.</summary>
    public int MaximumBytesPerField { get; }
}
