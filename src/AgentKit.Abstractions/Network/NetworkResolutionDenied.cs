// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Resolution succeeded, but every resulting address is rejected by the
/// configured <see cref="NetworkDestinationPolicy"/> before any connection
/// is attempted.
/// </summary>
public sealed record NetworkResolutionDenied: NetworkResolutionResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResolutionDenied"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public NetworkResolutionDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
