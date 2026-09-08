// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one stable session-entry protocol kind on durable storage.
/// </summary>
/// <remarks>
/// <para>
/// This value is a wire identity selected by a session-entry codec; it is not
/// a CLR type name and must remain stable when an implementation is renamed
/// or moved between assemblies.
/// </para>
/// <para>
/// Equality is ordinal textual equality over <see cref="Value"/>. The
/// default value is intentionally invalid where a durable codec descriptor is
/// constructed, so a missing type identity cannot be serialized as an
/// anonymous entry.
/// </para>
/// </remarks>
public readonly record struct SessionEntryTypeId
{
    /// <summary>
    /// Initializes a stable session-entry protocol kind.
    /// </summary>
    /// <param name="value">
    /// The nonblank, component-defined wire identity used to select a reader.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty or whitespace.
    /// </exception>
    public SessionEntryTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Gets the stable wire identity for this entry family.
    /// </summary>
    /// <value>
    /// A nonblank component-defined identifier after successful construction,
    /// or <see langword="null"/> on the default uninitialized struct. It has
    /// no relationship to a CLR assembly-qualified type name.
    /// </value>
    public string Value { get; }

    /// <summary>
    /// Returns the stable wire identity for diagnostics and deterministic
    /// protocol composition.
    /// </summary>
    /// <returns>
    /// The same nonblank text held by <see cref="Value"/> after successful
    /// construction, or <see cref="string.Empty"/> for the default struct.
    /// </returns>
    public override string ToString() => Value ?? string.Empty;
}
