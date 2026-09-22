// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The write exceeded an authorized byte or final-size limit before commit.</summary>
public sealed record FileWriteLimitExceeded: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteLimitExceeded"/> record.</summary>
    /// <param name="authorizedLimitBytes">The authorized limit that was exceeded.</param>
    /// <param name="observedBytes">The observed byte count that exceeded the limit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either byte count is negative or <paramref name="observedBytes"/> is less than
    /// <paramref name="authorizedLimitBytes"/>.
    /// </exception>
    public FileWriteLimitExceeded(long authorizedLimitBytes, long observedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(authorizedLimitBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(observedBytes);
        if (observedBytes < authorizedLimitBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedBytes),
                observedBytes,
                "Observed bytes must meet or exceed the authorized limit for a limit-exceeded outcome.");
        }

        AuthorizedLimitBytes = authorizedLimitBytes;
        ObservedBytes = observedBytes;
    }

    /// <summary>Gets the authorized limit that was exceeded.</summary>
    public long AuthorizedLimitBytes { get; init; }

    /// <summary>Gets the observed byte count that exceeded the limit.</summary>
    public long ObservedBytes { get; init; }
}
