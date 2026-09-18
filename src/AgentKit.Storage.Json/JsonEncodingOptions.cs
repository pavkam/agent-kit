// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Mutable composition-time control over the exact JSON encoding contract used by one store leaf.</summary>
/// <remarks>
/// <para>
/// The instance exists only while a configure delegate passed to a registration overload runs. Registration materializes it
/// into an immutable <see cref="JsonEncodingSettings"/>, so the encoding contract cannot drift after composition.
/// </para>
/// <para>
/// <see cref="SerializerOptions"/> is a full passthrough: any setting <see cref="JsonSerializerOptions"/> supports may be
/// changed, including naming policy, converters, number handling, and ignore conditions. That flexibility makes the on-disk
/// format caller-defined, so the adapter protects previously written evidence in two ways. Registration records a
/// fingerprint of the semantic option set in the store manifest and refuses to open a root written under a different
/// contract, and initialization performs a round-trip self-check that rejects options which cannot faithfully reproduce
/// persisted security evidence. Presentation-only settings such as indentation are excluded from the fingerprint.
/// </para>
/// <para>
/// Indentation is honored when rewriting whole documents. It is always suppressed for newline-delimited record logs, because
/// a record must occupy exactly one line for the log to remain framable and crash-recoverable.
/// </para>
/// </remarks>
public sealed class JsonEncodingOptions
{
    /// <summary>Gets or sets the serializer options applied to every encode and decode performed by the store.</summary>
    /// <value>
    /// A mutable non-null options instance, pre-seeded by <see cref="JsonStoreSerialization.CreateCanonicalOptions"/> with
    /// the converters the adapter's persisted shapes require. Replacing the instance outright is permitted; registration
    /// validates that the result can still round-trip the store's evidence. Defaults to the canonical options.
    /// </value>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    public JsonSerializerOptions SerializerOptions
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(SerializerOptions));
            field = value;
        }
    } = JsonStoreSerialization.CreateCanonicalOptions();

    /// <summary>Applies a mutation to the current <see cref="SerializerOptions"/> instance.</summary>
    /// <param name="configure">The non-null delegate invoked once with the current options instance.</param>
    /// <returns>The same <see cref="JsonEncodingOptions"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The current options instance has already been made read-only.</exception>
    /// <remarks>This is a convenience over assigning <see cref="SerializerOptions"/>; it mutates in place rather than replacing.</remarks>
    public JsonEncodingOptions Configure(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(SerializerOptions);
        return this;
    }
}
