// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

using AgentKit.Session;

/// <summary>Derives the effective record-encoding contract this leaf needs from a host-configured base contract.</summary>
/// <remarks>
/// <para>
/// A host may replace the whole serializer contract through <see cref="JsonEncodingOptions.SerializerOptions"/>, but session
/// evidence carries three shapes reflection alone cannot persist correctly: the closed polymorphic message, content-part,
/// and correlation hierarchies; AgentKit's validating single-value identity structs; and <see cref="SessionEntry"/>, whose
/// only durable form is the shared portable wire envelope. This type layers exactly those three concerns onto the caller's
/// contract and leaves every other setting — naming policy, ignore condition, number handling, reader tolerance — under the
/// caller's control.
/// </para>
/// <para>
/// The result is fingerprinted as a whole, so the manifest binds a root to the caller's settings <em>and</em> to these
/// derived converters. A composition that later changes either fails closed instead of reinterpreting existing records.
/// </para>
/// </remarks>
internal static class JsonSessionSerialization
{
    /// <summary>Layers the session-store resolver, identity converters, and entry-envelope converter onto one host contract.</summary>
    /// <param name="baseEncoding">The frozen host-configured encoding whose record contract is used as the base.</param>
    /// <param name="codecs">The captured entry-codec catalog used for every persisted <see cref="SessionEntry"/>.</param>
    /// <returns>Immutable settings whose record contract can persist every shape the session store writes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseEncoding"/> or <paramref name="codecs"/> is null.</exception>
    /// <remarks>
    /// The returned contract always writes compact output because a record must occupy exactly one line. Converters are
    /// inserted ahead of the caller's own so an explicitly configured converter for an unrelated type still applies while
    /// the three session-critical shapes cannot be silently reinterpreted.
    /// </remarks>
    internal static JsonEncodingSettings CreateStoreRecordEncoding(
        JsonEncodingSettings baseEncoding, ISessionEntryCodecCatalog codecs)
    {
        ArgumentNullException.ThrowIfNull(baseEncoding);
        ArgumentNullException.ThrowIfNull(codecs);
        var derived = Derive(baseEncoding);
        derived.Converters.Insert(0, new JsonSessionEntryConverterFactory(codecs));
        derived.MakeReadOnly();
        return new JsonEncodingSettings(derived);
    }

    /// <summary>Layers the session resolver and identity converters onto one host contract for routing records.</summary>
    /// <param name="baseEncoding">The frozen host-configured encoding whose record contract is used as the base.</param>
    /// <returns>Immutable settings whose record contract can persist every shape the session directory writes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseEncoding"/> is null.</exception>
    /// <remarks>
    /// Routing records never carry a <see cref="SessionEntry"/>, so the directory deliberately takes no dependency on an
    /// entry-codec catalog. Its fingerprint therefore differs from the store's, which is correct: the two roots hold
    /// different evidence under different contracts.
    /// </remarks>
    internal static JsonEncodingSettings CreateDirectoryRecordEncoding(JsonEncodingSettings baseEncoding)
    {
        ArgumentNullException.ThrowIfNull(baseEncoding);
        var derived = Derive(baseEncoding);
        derived.MakeReadOnly();
        return new JsonEncodingSettings(derived);
    }

    private static JsonSerializerOptions Derive(JsonEncodingSettings baseEncoding)
    {
        Debug.Assert(baseEncoding is not null, "A frozen host-configured encoding is required.");
        var derived = new JsonSerializerOptions(baseEncoding.RecordOptions)
        {
            TypeInfoResolver = PortableSessionJsonPolymorphism.CreateResolver(),
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            WriteIndented = false,
        };
        derived.Converters.Insert(0, new JsonSessionValueObjectConverterFactory());
        return derived;
    }
}
