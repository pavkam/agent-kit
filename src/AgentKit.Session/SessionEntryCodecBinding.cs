// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Retains one non-null codec with its exactly-once captured immutable descriptor.</summary>
internal sealed record SessionEntryCodecBinding
{
    /// <summary>Creates one validated binding without reading the codec descriptor.</summary>
    /// <param name="codec">The non-null singleton codec instance.</param>
    /// <param name="descriptor">The non-null descriptor previously captured from <paramref name="codec"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="codec"/> or <paramref name="descriptor"/> is null.</exception>
    internal SessionEntryCodecBinding(ISessionEntryCodec codec, SessionEntryCodecDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(descriptor);
        Codec = codec;
        Descriptor = descriptor;
    }

    /// <summary>Gets the singleton codec used for concurrent encode and decode dispatch.</summary>
    internal ISessionEntryCodec Codec { get; }

    /// <summary>Gets the immutable descriptor captured once during catalog construction.</summary>
    internal SessionEntryCodecDescriptor Descriptor { get; }
}
