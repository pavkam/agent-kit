// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Deadlines that apply to one protected DNS resolution operation.</summary>
public sealed record NetworkResolutionBounds
{
    /// <summary>Initializes resolution bounds.</summary>
    /// <param name="resolutionTimeout">The maximum time allowed to complete resolution.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="resolutionTimeout"/> is not positive.
    /// </exception>
    public NetworkResolutionBounds(TimeSpan resolutionTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(resolutionTimeout, TimeSpan.Zero);
        ResolutionTimeout = resolutionTimeout;
    }

    /// <summary>Gets the maximum time allowed to complete resolution.</summary>
    public TimeSpan ResolutionTimeout { get; init; }
}
