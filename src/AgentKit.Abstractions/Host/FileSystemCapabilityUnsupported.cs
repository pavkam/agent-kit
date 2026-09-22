// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a profile exists but does not expose the requested capability.</summary>
public sealed record FileSystemCapabilityUnsupported: FileSystemSelectionResult
{
    /// <summary>Initializes an unsupported-capability outcome.</summary>
    /// <param name="key">The profile that was found.</param>
    /// <param name="requiredCapability">The capability the caller requested.</param>
    /// <param name="declaredCapabilities">The capabilities the profile actually advertises.</param>
    /// <exception cref="ArgumentOutOfRangeException">A key or capability value is default or undefined.</exception>
    public FileSystemCapabilityUnsupported(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        FileSystemCapabilities declaredCapabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(requiredCapability);
        ArgumentNullException.ThrowIfNull(declaredCapabilities);
        Key = key;
        RequiredCapability = requiredCapability;
        DeclaredCapabilities = declaredCapabilities;
    }

    /// <summary>Gets the profile key that was resolved.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the capability the caller requested.</summary>
    public FileSystemCapability RequiredCapability { get; init; }

    /// <summary>Gets the capabilities registered for the profile.</summary>
    public FileSystemCapabilities DeclaredCapabilities { get; init; }
}
