// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Derives a stable fingerprint from every effective JSON option that can change a persisted record's meaning.</summary>
/// <remarks>
/// Callers may supply arbitrary serializer options, so the on-disk contract is caller-influenced. The fingerprint captures
/// exactly the settings that alter emitted property names, value shapes, numeric text, converter selection, and reader
/// tolerance. Purely presentational settings such as indentation are deliberately excluded because record logs always write
/// one compact line per record and document rewrites remain parseable regardless of whitespace.
/// </remarks>
public static class JsonFormatFingerprint
{
    /// <summary>Computes the canonical fingerprint of one effective encoding contract.</summary>
    /// <param name="options">The effective non-null options used for every encode and decode.</param>
    /// <returns>A lowercase hexadecimal SHA-256 fingerprint of the ordered semantic option set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <remarks>
    /// The value is deterministic for one option set within a process and across processes running the same adapter and
    /// converter types. It intentionally includes ordered converter type names, because converter order selects which
    /// converter handles a type and therefore changes the emitted representation.
    /// </remarks>
    public static string Compute(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = new StringBuilder(512);
        Append(builder, "namingPolicy", TypeName(options.PropertyNamingPolicy));
        Append(builder, "dictionaryKeyPolicy", TypeName(options.DictionaryKeyPolicy));
        Append(builder, "caseInsensitive", options.PropertyNameCaseInsensitive);
        Append(builder, "numberHandling", options.NumberHandling);
        Append(builder, "defaultIgnoreCondition", options.DefaultIgnoreCondition);
        Append(builder, "ignoreReadOnlyProperties", options.IgnoreReadOnlyProperties);
        Append(builder, "ignoreReadOnlyFields", options.IgnoreReadOnlyFields);
        Append(builder, "includeFields", options.IncludeFields);
        Append(builder, "referenceHandler", TypeName(options.ReferenceHandler));
        Append(builder, "encoder", TypeName(options.Encoder));
        Append(builder, "maxDepth", options.MaxDepth);
        Append(builder, "allowTrailingCommas", options.AllowTrailingCommas);
        Append(builder, "readCommentHandling", options.ReadCommentHandling);
        Append(builder, "unmappedMemberHandling", options.UnmappedMemberHandling);
        Append(builder, "objectCreationHandling", options.PreferredObjectCreationHandling);
        Append(builder, "outOfOrderMetadata", options.AllowOutOfOrderMetadataProperties);
        Append(builder, "respectNullableAnnotations", options.RespectNullableAnnotations);
        Append(builder, "respectRequiredConstructorParameters", options.RespectRequiredConstructorParameters);
        Append(builder, "typeInfoResolver", TypeName(options.TypeInfoResolver));
        for (var index = 0; index < options.Converters.Count; index++)
        {
            Append(builder, $"converter[{index.ToString(CultureInfo.InvariantCulture)}]", TypeName(options.Converters[index]));
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string name, object? value)
    {
        Debug.Assert(builder is not null, "An active fingerprint builder is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "A stable option name is required.");
        _ = builder.Append(name).Append('=')
            .Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null")
            .Append('\n');
    }

    private static string TypeName(object? instance) => instance?.GetType().FullName ?? "null";
}
