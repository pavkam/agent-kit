// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures versioned preprocessing evidence used to compare idempotent admission replays.</summary>
/// <remarks>The manifest records canonical input fingerprints and configuration evidence; it neither authorizes admission nor reruns preprocessing.</remarks>
public sealed record InputPreprocessingManifest
{
    /// <summary>Initializes preprocessing evidence.</summary>
    /// <param name="configurationVersion">The positive preprocessing configuration revision.</param>
    /// <param name="originalFingerprint">The canonical original-payload fingerprint.</param>
    /// <param name="effectiveFingerprint">The canonical effective-payload fingerprint.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="configurationVersion"/> is default.</exception>
    public InputPreprocessingManifest(ConfigurationVersion configurationVersion, InputFingerprint originalFingerprint, InputFingerprint effectiveFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(configurationVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(originalFingerprint, default);
        ArgumentOutOfRangeException.ThrowIfEqual(effectiveFingerprint, default);
        ConfigurationVersion = configurationVersion;
        OriginalFingerprint = originalFingerprint;
        EffectiveFingerprint = effectiveFingerprint;
    }

    /// <summary>Gets preprocessing configuration revision.</summary><value>The captured positive revision.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets original-payload fingerprint.</summary><value>The canonical original digest.</value>
    public InputFingerprint OriginalFingerprint { get; }
    /// <summary>Gets effective-payload fingerprint.</summary><value>The canonical effective digest.</value>
    public InputFingerprint EffectiveFingerprint { get; }
}
