// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One versioned, serialized payload carrying a recoverable operation's
/// input, intermediate state, or terminal result across process loss.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. Equality is structural over the
/// schema version and the payload bytes, which are never mutated after
/// construction.
/// </para>
/// <para>
/// Payloads carry data, never behavior. Runtime objects, service scopes,
/// tasks, cancellation sources, delegates, credentials, and provider clients
/// are never serialized into one; a recovering worker rebuilds those from its
/// own validated composition. Encoding and decoding go through a registered
/// <see cref="IDurableOperationCodec{TState}"/> so that unknown but
/// compatible fields survive a round trip instead of being silently dropped
/// by a newer reader.
/// </para>
/// </remarks>
public sealed record OperationPayload
{
    private readonly ImmutableArray<byte> _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationPayload"/>
    /// record.
    /// </summary>
    /// <param name="schemaVersion">
    /// The schema version the payload bytes were written under, used to
    /// decide whether the current codec can interpret them.
    /// </param>
    /// <param name="data">
    /// The serialized payload bytes. An empty payload is valid and
    /// represents an operation whose state carries no data.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> is uninitialized. A default
    /// <see cref="ImmutableArray{T}"/> is not the same as an empty one and
    /// would throw on first access.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="schemaVersion"/> is default and cannot identify a
    /// recoverable serialization format.
    /// </exception>
    public OperationPayload(SchemaVersion schemaVersion, ImmutableArray<byte> data)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(schemaVersion, default);
        ArgumentException.ThrowIfDefault(data);
        SchemaVersion = schemaVersion;
        _data = data;
    }

    /// <summary>Gets the non-default schema version the payload was written under.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set a default schema version.
    /// </exception>
    public SchemaVersion SchemaVersion
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(SchemaVersion));
            field = value;
        }
    }

    /// <summary>Gets the serialized payload bytes.</summary>
    /// <value>
    /// An initialized, possibly empty array. The value is immutable, so
    /// exposing it directly cannot let a caller mutate recorded state.
    /// </value>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array.
    /// </exception>
    public ImmutableArray<byte> Data
    {
        get => _data;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Data));
            _data = value;
        }
    }

    /// <summary>
    /// Determines whether another payload retains the same schema version and
    /// byte sequence.
    /// </summary>
    /// <param name="other">The payload to compare, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="other"/> is non-null
    /// and its schema version and every serialized byte are equal.
    /// </returns>
    public bool Equals(OperationPayload? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && Data.AsSpan().SequenceEqual(other.Data.AsSpan());

    /// <summary>
    /// Returns a hash derived from the schema version and every serialized
    /// byte, consistent with structural payload equality.
    /// </summary>
    /// <returns>The structural hash code for this payload.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        foreach (var value in Data)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
