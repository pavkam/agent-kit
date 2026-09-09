// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Declares the process-wide payload ceiling applied before codec dispatch.</summary>
public sealed record SessionEntryCodecCatalogOptions
{
    /// <summary>Initializes immutable catalog limits.</summary>
    /// <param name="maximumPayloadBytes">The positive maximum payload size, including opaque unknown entries.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumPayloadBytes"/> is not positive.</exception>
    public SessionEntryCodecCatalogOptions(int maximumPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumPayloadBytes, 0);
        MaximumPayloadBytes = maximumPayloadBytes;
    }

    /// <summary>Gets the positive process-wide maximum payload size.</summary>
    public int MaximumPayloadBytes { get; }
}
