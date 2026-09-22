// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one registered file-system capability for a profile key.</summary>
/// <remarks>
/// Selection is explicit: missing profiles and unsupported capabilities return typed outcomes rather than falling back to another profile.
/// </remarks>
public interface IFileSystemSelector
{
    /// <summary>Resolves one capability registered under <paramref name="key"/>.</summary>
    /// <param name="key">The profile key authored by the application.</param>
    /// <param name="requiredCapability">The capability the caller intends to use.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A closed selection outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default or <paramref name="requiredCapability"/> is undefined.</exception>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default);
}
