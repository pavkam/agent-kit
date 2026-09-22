// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the optional behaviors a keyed file-system profile actually supports.</summary>
/// <remarks>
/// Selection compares a requested <see cref="FileSystemCapability"/> against
/// <see cref="Supported"/> and rejects incompatible operations before any host effect.
/// </remarks>
public sealed record FileSystemCapabilities
{
    /// <summary>Initializes immutable capability evidence for one profile registration.</summary>
    /// <param name="supported">The non-empty set of capabilities this profile exposes.</param>
    /// <param name="supportsSymbolicLinks">Whether link traversal may be authorized and enforced.</param>
    /// <param name="supportsAtomicReplace">Whether replace dispositions can commit atomically.</param>
    /// <param name="supportsAtomicAppend">Whether append can commit without interleaving.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="supported"/> is undefined.</exception>
    public FileSystemCapabilities(
        FileSystemCapability supported,
        bool supportsSymbolicLinks = false,
        bool supportsAtomicReplace = true,
        bool supportsAtomicAppend = false)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(supported);
        Supported = supported;
        SupportsSymbolicLinks = supportsSymbolicLinks;
        SupportsAtomicReplace = supportsAtomicReplace;
        SupportsAtomicAppend = supportsAtomicAppend;
    }

    /// <summary>Gets the supported capability set.</summary>
    /// <value>A defined flags combination; may be <see cref="FileSystemCapability.None"/> only for explicit empty profiles.</value>
    public FileSystemCapability Supported { get; init; }

    /// <summary>Gets whether symbolic links may be traversed under explicit authorization.</summary>
    public bool SupportsSymbolicLinks { get; init; }

    /// <summary>Gets whether replace dispositions can use atomic publication semantics.</summary>
    public bool SupportsAtomicReplace { get; init; }

    /// <summary>Gets whether append dispositions can commit without byte interleaving.</summary>
    public bool SupportsAtomicAppend { get; init; }
}
