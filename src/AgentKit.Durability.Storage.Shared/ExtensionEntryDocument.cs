// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Portable persisted mirror of one <see cref="ExtensionData"/> entry retained on a recoverable declaration.</summary>
/// <remarks>
/// Extension data is persisted as an ordinal-ordered array of name and value pairs rather than a JSON object, for two
/// reasons. A store's dictionary-key naming policy would otherwise rewrite a component-defined extension name, and an
/// <see cref="ImmutableDictionary{TKey, TValue}"/> guarantees no stable enumeration order, so an object form would make
/// two byte-identical declarations encode differently. The canonical JSON bytes are persisted as base64 so a value the
/// reader does not recognize survives the round trip unchanged instead of being reinterpreted.
/// </remarks>
/// <param name="Name">The non-null stable component-defined extension name, preserved with its original spelling.</param>
/// <param name="Value">The base64 encoding of the value's UTF-8 canonical JSON bytes; empty for a value carrying no bytes.</param>
internal sealed record ExtensionEntryDocument(string Name, string Value)
{
    /// <summary>Projects one domain extension bag into an ordinal-ordered array of persisted entries.</summary>
    /// <param name="value">The non-null extension bag to project.</param>
    /// <returns>Every entry ordered by ordinal name comparison, so the encoding is canonical.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static ImmutableArray<ExtensionEntryDocument> FromDomain(ExtensionData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return
        [
            .. value.Values
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => new ExtensionEntryDocument(
                    entry.Key, Convert.ToBase64String(entry.Value.CanonicalJson.AsSpan()))),
        ];
    }

    /// <summary>Reconstructs the exact domain extension bag a persisted entry array was projected from.</summary>
    /// <param name="entries">The persisted entries, which may be a default array when the declaration carried none.</param>
    /// <returns>An extension bag equal to the projected original, or <see cref="ExtensionData.Empty"/> when no entry was persisted.</returns>
    /// <exception cref="ArgumentException"><paramref name="entries"/> contains a null element, a blank name, or a duplicate name.</exception>
    /// <exception cref="ArgumentNullException">A persisted entry omits its value, which a well-formed document never does.</exception>
    /// <exception cref="FormatException">A persisted value is not valid base64.</exception>
    internal static ExtensionData ToDomain(ImmutableArray<ExtensionEntryDocument> entries)
    {
        if (entries.IsDefaultOrEmpty)
        {
            return ExtensionData.Empty;
        }

        ArgumentException.ThrowIfContainsNull(entries, nameof(entries));
        var builder = ImmutableDictionary.CreateBuilder<string, ExtensionValue>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Name, nameof(entries));
            ArgumentNullException.ThrowIfNull(entry.Value, nameof(entries));
            if (builder.ContainsKey(entry.Name))
            {
                throw new ArgumentException(
                    "A persisted durable declaration repeats an extension name.", nameof(entries));
            }

            builder.Add(entry.Name, new ExtensionValue([.. Convert.FromBase64String(entry.Value)]));
        }

        return new ExtensionData(builder.ToImmutable());
    }
}
