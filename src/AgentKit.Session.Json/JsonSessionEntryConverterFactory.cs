// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Creates the shared wire-envelope converter for the abstract <see cref="SessionEntry"/> hierarchy.</summary>
/// <remarks>
/// The factory matches only the abstract base type. Concrete entry records are never serialized through reflection,
/// because doing so would create a second, unversioned entry format beside the portable codecs.
/// </remarks>
internal sealed class JsonSessionEntryConverterFactory: JsonConverterFactory
{
    private readonly ISessionEntryCodecCatalog _codecs;

    /// <summary>Captures the immutable codec catalog handed to every created converter.</summary>
    /// <param name="codecs">The non-null catalog resolved once at composition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="codecs"/> is null.</exception>
    internal JsonSessionEntryConverterFactory(ISessionEntryCodecCatalog codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        _codecs = codecs;
    }

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(SessionEntry);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        return new JsonSessionEntryConverter(_codecs);
    }
}
