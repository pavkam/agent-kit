// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Supplies one codec and deterministic representative entry for reusable portable-codec checks.</summary>
public interface ISessionEntryCodecConformanceFixture
{
    /// <summary>Creates one thread-safe codec instance.</summary><returns>The codec under test.</returns>
    public ISessionEntryCodec CreateCodec();
    /// <summary>Creates one valid deterministic entry owned by the codec.</summary><returns>A fresh semantically equivalent entry.</returns>
    public SessionEntry CreateEntry();
    /// <summary>Compares the complete portable semantics of two entries.</summary><param name="expected">The original entry.</param><param name="actual">The decoded entry.</param><returns><see langword="true"/> when every durable semantic field agrees.</returns>
    public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual);
}
