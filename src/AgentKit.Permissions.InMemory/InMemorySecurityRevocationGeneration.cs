// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Retains a process-local revocation epoch for tests and standalone hosts.</summary>
/// <remarks>Tests may advance the epoch through <see cref="SetCurrent"/> without restarting the host.</remarks>
public sealed class InMemorySecurityRevocationGeneration: ISecurityRevocationGeneration
{
    private long _current;

    /// <summary>Initializes the epoch to one.</summary>
    public InMemorySecurityRevocationGeneration()
        : this(1)
    {
    }

    /// <summary>Initializes the epoch to a positive value.</summary>
    /// <param name="initialVersion">The starting revocation epoch.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialVersion"/> is not positive.</exception>
    public InMemorySecurityRevocationGeneration(long initialVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialVersion);
        _current = initialVersion;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityRevocationVersion> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SecurityRevocationVersion(_current));
    }

    /// <summary>Replaces the live epoch for deterministic test control.</summary>
    /// <param name="version">The positive epoch that subsequent reads return.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive.</exception>
    public void SetCurrent(long version)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        _current = version;
    }
}
