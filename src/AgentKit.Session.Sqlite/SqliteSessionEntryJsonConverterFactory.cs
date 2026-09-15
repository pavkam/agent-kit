// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Creates the portable wire-envelope converter for the session-entry abstraction.</summary>
internal sealed class SqliteSessionEntryJsonConverterFactory: JsonConverterFactory
{
    private readonly ISessionEntryCodecCatalog _codecs;

    /// <summary>Captures the immutable codec catalog used for every persisted entry.</summary>
    /// <param name="codecs">The non-null codec catalog.</param>
    internal SqliteSessionEntryJsonConverterFactory(ISessionEntryCodecCatalog codecs)
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
        return new SqliteSessionEntryJsonConverter(_codecs);
    }
}
