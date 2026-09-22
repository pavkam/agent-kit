// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Standard-input payload content for one process start.</summary>
public sealed record ProcessInput
{
    /// <summary>Initializes standard-input content.</summary>
    /// <param name="payload">The exact input bytes.</param>
    /// <param name="fingerprint">The authorized payload fingerprint.</param>
    /// <exception cref="ArgumentException"><paramref name="payload"/> is default or <paramref name="fingerprint"/> is default.</exception>
    public ProcessInput(ReadOnlyMemory<byte> payload, ContentHash fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(fingerprint, default);
        Payload = payload;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the exact input bytes.</summary>
    public ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>Gets the authorized payload fingerprint.</summary>
    public ContentHash Fingerprint { get; init; }
}
