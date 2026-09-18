// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Performs a real encode and decode of one persisted mirror document under the adapter's canonical contract.</summary>
/// <remarks>
/// A mirror that only satisfies <c>FromDomain</c> and <c>ToDomain</c> in memory can still be unpersistable, because the
/// canonical contract rejects unmapped members, refuses integer enumeration values, and omits null members entirely. Driving
/// every mirror case through a genuine <see cref="JsonSerializer"/> cycle proves the document survives the bytes it will
/// actually be stored as.
/// </remarks>
internal static class TestCanonicalJson
{
    /// <summary>Encodes a mirror document and decodes it again under freshly created canonical options.</summary>
    /// <typeparam name="TValue">The persisted mirror shape being cycled.</typeparam>
    /// <param name="value">The non-null document to encode.</param>
    /// <returns>The decoded document, which structural equality must find equal to <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The canonical contract decoded its own output to null.</exception>
    /// <exception cref="JsonException">The canonical contract cannot encode or decode the document.</exception>
    internal static TValue Cycle<TValue>(TValue value)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return JsonSerializer.Deserialize<TValue>(payload, options)
            ?? throw new InvalidOperationException("The canonical contract decoded a mirror document to null.");
    }

    /// <summary>Encodes a mirror document to its canonical UTF-8 text so a case can assert the persisted shape.</summary>
    /// <typeparam name="TValue">The persisted mirror shape being encoded.</typeparam>
    /// <param name="value">The non-null document to encode.</param>
    /// <returns>The compact canonical JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="JsonException">The canonical contract cannot encode the document.</exception>
    internal static string Encode<TValue>(TValue value)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, JsonStoreSerialization.CreateCanonicalOptions());
    }
}
