// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Host-verified executable identity captured before process creation.</summary>
public sealed record ResolvedExecutable
{
    /// <summary>Initializes resolved executable facts.</summary>
    /// <param name="absolutePath">The absolute host path.</param>
    /// <param name="fingerprint">The executable fingerprint.</param>
    /// <exception cref="ArgumentException">The path is blank or not rooted.</exception>
    public ResolvedExecutable(string absolutePath, ContentHash fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        if (!Path.IsPathRooted(absolutePath))
        {
            throw new ArgumentException("The resolved executable path must be absolute.", nameof(absolutePath));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(fingerprint, default);
        AbsolutePath = absolutePath;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the absolute host path.</summary>
    public string AbsolutePath { get; init; }

    /// <summary>Gets the executable fingerprint.</summary>
    public ContentHash Fingerprint { get; init; }
}
