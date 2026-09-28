// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the effective redaction and size policy for one observation export attempt.</summary>
public sealed record ObservationPolicy
{
    /// <summary>Initializes a validated policy snapshot.</summary>
    /// <param name="bounds">The non-null size limits.</param>
    /// <param name="allowedClassifications">The classifications permitted to survive redaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bounds"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="allowedClassifications"/> is default.</exception>
    public ObservationPolicy(ObservationBounds bounds, ImmutableHashSet<DataClassification> allowedClassifications)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (allowedClassifications is null)
        {
            throw new ArgumentException("The allowed classification set must be provided.", nameof(allowedClassifications));
        }
        Bounds = bounds;
        AllowedClassifications = allowedClassifications;
    }

    /// <summary>Gets the size limits.</summary>
    public ObservationBounds Bounds { get; }

    /// <summary>Gets the classifications permitted to survive redaction.</summary>
    public ImmutableHashSet<DataClassification> AllowedClassifications { get; }
}
