// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of one extension-data entry.</summary>
/// <param name="Name">The non-blank extension name.</param>
/// <param name="Value">The Base64 canonical JSON of the extension value.</param>
internal sealed record StoreExtensionDocument(string Name, string Value)
{
    /// <summary>Converts extension data to its ordered persisted form.</summary>
    /// <param name="value">The non-null extension data.</param>
    /// <returns>The entries sorted by name so the persisted bytes are deterministic.</returns>
    internal static ImmutableArray<StoreExtensionDocument> FromDomain(ExtensionData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return
        [
            .. value.Values
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(static entry => new StoreExtensionDocument(entry.Key, Convert.ToBase64String(entry.Value.CanonicalJson.AsSpan()))),
        ];
    }

    /// <summary>Converts persisted entries back to extension data.</summary>
    /// <param name="entries">The persisted entries; default or empty yields empty data.</param>
    /// <returns>The restored extension data.</returns>
    /// <exception cref="ArgumentException">An entry is null, unnamed, or repeats a name.</exception>
    internal static ExtensionData ToDomain(ImmutableArray<StoreExtensionDocument> entries)
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
            if (!builder.TryAdd(entry.Name, new ExtensionValue([.. Convert.FromBase64String(entry.Value)])))
            {
                throw new ArgumentException("A persisted document repeats an extension name.", nameof(entries));
            }
        }

        return new ExtensionData(builder.ToImmutable());
    }
}
