// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Routes session-entry encoding and decoding through an immutable codec composition.</summary>
public interface ISessionEntryCodecCatalog
{
    /// <summary>Encodes an entry through the codec registered for its exact local runtime type.</summary>
    /// <param name="entry">The non-null entry to encode.</param>
    /// <returns>A typed encoded or rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public SessionEntryEncodeResult Encode(SessionEntry entry);

    /// <summary>Decodes an envelope through its exact stable wire identity and schema.</summary>
    /// <param name="wire">The non-null envelope to decode.</param>
    /// <returns>A decoded, opaque, or rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire);
}
