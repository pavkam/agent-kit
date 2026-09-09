// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Encodes and decodes exactly one declared session-entry family without
/// reflection-based durable type activation.
/// </summary>
/// <remarks>
/// Implementations validate limits before parsing payload content. A decoder
/// returns <see cref="SessionEntryOpaque"/> for a wire identity or schema it
/// does not own and <see cref="SessionEntryDecodeRejected"/> for malformed
/// data; neither outcome authorizes, executes, or fabricates an entry.
/// Implementations are registered as singletons and must support concurrent
/// calls without mutable dispatch drift. Decoded callers retain
/// <see cref="DecodedSessionEntry.Wire"/> unchanged for loss-aware replay;
/// <see cref="Encode"/> writes a new value and does not merge unknown fields.
/// </remarks>
public interface ISessionEntryCodec
{
    /// <summary>Gets the immutable dispatch and finite-limit declaration.</summary>
    public SessionEntryCodecDescriptor Descriptor { get; }

    /// <summary>Encodes a matching exact local entry type at the declared write schema.</summary>
    /// <param name="entry">The non-null exact entry type declared by <see cref="Descriptor"/>.</param>
    /// <returns>A bounded envelope or a content-free rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public SessionEntryEncodeResult Encode(SessionEntry entry);

    /// <summary>Decodes one envelope selected by exact wire identity and schema.</summary>
    /// <param name="wire">The non-null bounded envelope to validate and decode.</param>
    /// <returns>A decoded entry, opaque retained data, or a content-free rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire);
}
