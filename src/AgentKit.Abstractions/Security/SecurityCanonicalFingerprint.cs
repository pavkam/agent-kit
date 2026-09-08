// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Hashes an immutable security payload with one unambiguous structural encoding.</summary>
/// <remarks>The encoder includes runtime type and property names, uses ordinal property and extension-key ordering, preserves sequence order, and length-prefixes every exact scalar or content byte sequence. It returns only a digest and never exports protected payload bytes.</remarks>
internal static class SecurityCanonicalFingerprint
{
    /// <summary>Computes a canonical SHA-256 fingerprint for a complete immutable payload graph.</summary>
    /// <typeparam name="TPayload">The payload's statically declared root type.</typeparam>
    /// <param name="payload">The non-null immutable payload.</param>
    /// <returns>An algorithm-qualified digest over the payload's exact runtime types, properties, sequence order, and content bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    /// <exception cref="ArgumentException">The graph is cyclic or contains a value without canonical public state.</exception>
    internal static InputFingerprint Create<TPayload>(TPayload payload)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        WriteValue(hash, payload, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return new InputFingerprint($"sha256:{Convert.ToHexStringLower(hash.GetHashAndReset())}");
    }

    private static void WriteValue(IncrementalHash hash, object? value, HashSet<object> active)
    {
        Debug.Assert(hash is not null, "A hash accumulator is required.");
        Debug.Assert(active is not null, "A cycle-detection set is required.");
        if (value is null)
        {
            Append(hash, 0, []);
            return;
        }

        var type = value.GetType();
        Append(hash, 1, Encoding.UTF8.GetBytes(type.FullName ?? type.Name));
        switch (value)
        {
            case string text:
                Append(hash, 2, EncodeUtf16CodeUnits(text));
                return;
            case bool boolean:
                Append(hash, 3, [boolean ? (byte) 1 : (byte) 0]);
                return;
            case byte octet:
                Append(hash, 4, [octet]);
                return;
            case sbyte or short or ushort or int or uint or long or ulong or decimal or float or double:
                Append(hash, 5, Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture)!));
                return;
            case Guid guid:
                Span<byte> guidBytes = stackalloc byte[16];
                _ = guid.TryWriteBytes(guidBytes, bigEndian: true, out _);
                Append(hash, 6, guidBytes);
                return;
            case DateTimeOffset timestamp:
                Append(hash, 7, Encoding.UTF8.GetBytes(
                    $"{timestamp.Ticks.ToString(CultureInfo.InvariantCulture)}:{timestamp.Offset.Ticks.ToString(CultureInfo.InvariantCulture)}"));
                return;
            case DateTime dateTime:
                Append(hash, 8, Encoding.UTF8.GetBytes(
                    $"{dateTime.Ticks.ToString(CultureInfo.InvariantCulture)}:{((int) dateTime.Kind).ToString(CultureInfo.InvariantCulture)}"));
                return;
            case TimeSpan duration:
                Append(hash, 9, Encoding.UTF8.GetBytes(duration.Ticks.ToString(CultureInfo.InvariantCulture)));
                return;
            case Enum enumeration:
                Append(hash, 10, Encoding.UTF8.GetBytes(Enum.Format(type, enumeration, "D")));
                return;
            case byte[] bytes:
                Append(hash, 11, bytes);
                return;
            case ImmutableArray<byte> immutableBytes:
                Append(hash, 12, immutableBytes.AsSpan());
                return;
            case ExtensionData extensions:
                Append(hash, 13, Encoding.UTF8.GetBytes(extensions.Values.Count.ToString(CultureInfo.InvariantCulture)));
                foreach (var pair in extensions.Values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    WriteValue(hash, pair.Key, active);
                    WriteValue(hash, pair.Value.CanonicalJson, active);
                }
                return;
            case JsonElement json:
                Append(hash, 17, Encoding.UTF8.GetBytes(json.ValueKind == JsonValueKind.Undefined
                    ? string.Empty
                    : json.GetRawText()));
                return;
            case Uri uri:
                Append(hash, 18, [uri.IsAbsoluteUri ? (byte) 1 : (byte) 0]);
                Append(hash, 19, EncodeUtf16CodeUnits(uri.OriginalString));
                return;
            default:
                break;
        }

        var track = !type.IsValueType;
        if (track && !active.Add(value))
        {
            throw new ArgumentException("The security payload graph must not contain cycles.", nameof(value));
        }

        try
        {
            var dictionaryInterface = type.GetInterfaces().FirstOrDefault(static candidate =>
                candidate.IsGenericType
                && (candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                    || candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)));
            if (dictionaryInterface is not null)
            {
                if (dictionaryInterface.GetGenericArguments()[0] != typeof(string))
                {
                    throw new ArgumentException(
                        "Security payload dictionaries must use string keys.", nameof(value));
                }

                var pairs = ((IEnumerable) value).Cast<object>()
                    .Select(static pair => (
                        Key: (string) pair.GetType().GetProperty("Key")!.GetValue(pair)!,
                        Value: pair.GetType().GetProperty("Value")!.GetValue(pair)))
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .ToArray();
                Append(hash, 20, Encoding.UTF8.GetBytes(pairs.Length.ToString(CultureInfo.InvariantCulture)));
                foreach (var (key, item) in pairs)
                {
                    WriteValue(hash, key, active);
                    WriteValue(hash, item, active);
                }
                return;
            }

            if (value is IEnumerable sequence)
            {
                var items = sequence.Cast<object?>().ToArray();
                Append(hash, 14, Encoding.UTF8.GetBytes(items.Length.ToString(CultureInfo.InvariantCulture)));
                foreach (var item in items)
                {
                    WriteValue(hash, item, active);
                }
                return;
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .ToArray();
            if (properties.Length == 0)
            {
                throw new ArgumentException($"Type {type.FullName} has no canonical public state.", nameof(value));
            }

            ThrowIfHiddenInstanceState(type, properties);

            Append(hash, 15, Encoding.UTF8.GetBytes(properties.Length.ToString(CultureInfo.InvariantCulture)));
            foreach (var property in properties)
            {
                Append(hash, 16, Encoding.UTF8.GetBytes(property.Name));
                WriteValue(hash, property.GetValue(value), active);
            }
        }
        finally
        {
            if (track)
            {
                _ = active.Remove(value);
            }
        }
    }

    private static void ThrowIfHiddenInstanceState(Type type, PropertyInfo[] publicProperties)
    {
        Debug.Assert(type is not null, "A runtime type is required.");
        Debug.Assert(publicProperties is not null, "The public property set is required.");
        var propertyNames = publicProperties.Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var name = field.Name;
                var isPublicPropertyBackingField = name.Length > 3
                    && name[0] == '<'
                    && name.EndsWith(">k__BackingField", StringComparison.Ordinal)
                    && propertyNames.Contains(name[1..name.IndexOf('>')]);
                if (!isPublicPropertyBackingField)
                {
                    throw new ArgumentException(
                        $"Type {type.FullName} contains state without a canonical public security representation.",
                        nameof(type));
                }
            }
        }
    }

    private static void Append(IncrementalHash hash, byte marker, ReadOnlySpan<byte> bytes)
    {
        Debug.Assert(hash is not null, "A hash accumulator is required.");
        Span<byte> header = stackalloc byte[5];
        header[0] = marker;
        BinaryPrimitives.WriteInt32BigEndian(header[1..], bytes.Length);
        hash.AppendData(header);
        hash.AppendData(bytes);
    }

    private static byte[] EncodeUtf16CodeUnits(string value)
    {
        Debug.Assert(value is not null, "A caller-validated string is required.");
        var bytes = GC.AllocateUninitializedArray<byte>(checked(value.Length * sizeof(char)));
        for (var index = 0; index < value.Length; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(
                bytes.AsSpan(index * sizeof(char), sizeof(char)), value[index]);
        }

        return bytes;
    }
}
