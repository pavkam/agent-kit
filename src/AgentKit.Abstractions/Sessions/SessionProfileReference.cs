// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one exact immutable session-profile revision.</summary>
/// <remarks>This value preserves the profile selected while an operation was compiled. It does not resolve registrations or permit a later configuration reload to alter an in-flight operation.</remarks>
public sealed record SessionProfileReference
{
    /// <summary>Initializes a reference to one validated profile revision.</summary>
    /// <param name="key">The nonblank profile key.</param>
    /// <param name="version">The positive immutable profile revision.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is default.</exception>
    public SessionProfileReference(SessionProfileKey key, SessionProfileVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfEqual(version, default, nameof(version));
        Key = key;
        Version = version;
    }

    /// <summary>Gets the selected profile key.</summary>
    /// <value>The nonblank ordinal profile selection.</value>
    public SessionProfileKey Key { get; }

    /// <summary>Gets the selected immutable profile revision.</summary>
    /// <value>The positive revision captured with the selection.</value>
    public SessionProfileVersion Version { get; }
}
