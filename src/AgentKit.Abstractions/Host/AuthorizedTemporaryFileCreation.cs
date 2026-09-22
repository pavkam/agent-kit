// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes creation of one bounded temporary file under a resolved parent directory.</summary>
public sealed record AuthorizedTemporaryFileCreation
{
    /// <summary>Initializes authorized temporary file creation evidence.</summary>
    /// <param name="resolvedParentDirectory">The resolved parent directory for the temporary file.</param>
    /// <param name="maxBytes">The maximum committed byte length for the temporary payload.</param>
    /// <param name="grant">The bounded grant for this creation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AuthorizedTemporaryFileCreation(
        ResolvedFileTarget resolvedParentDirectory,
        long maxBytes,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        ArgumentNullException.ThrowIfNull(grant);
        ResolvedParentDirectory = resolvedParentDirectory;
        MaxBytes = maxBytes;
        Grant = grant;
    }

    /// <summary>Gets the resolved parent directory for the temporary file.</summary>
    public ResolvedFileTarget ResolvedParentDirectory { get; init; }

    /// <summary>Gets the maximum committed byte length for the temporary payload.</summary>
    public long MaxBytes { get; init; }

    /// <summary>Gets the bounded creation grant.</summary>
    public SecurityGrant Grant { get; init; }
}
