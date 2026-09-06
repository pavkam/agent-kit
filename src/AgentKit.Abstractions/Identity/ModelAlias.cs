// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An application-facing selection key for one configured
/// <see cref="ModelDescriptor"/>, such as "fast-chat-model", independent of
/// the actual <see cref="ProviderId"/>, <see cref="ApiFamilyId"/>, and
/// <see cref="ModelId"/> it currently resolves to.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Configuration and application code select a model by alias so the actual
/// provider, model, or deployment behind it can change without touching
/// every call site. Durable records, telemetry, and vector metadata always
/// preserve the real resolved identity instead of the alias, so historical
/// data remains accurate even after the alias is repointed.
/// </para>
/// </remarks>
public readonly record struct ModelAlias
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelAlias"/> struct,
    /// validating that it carries usable selection text.
    /// </summary>
    /// <param name="value">The non-empty canonical alias text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ModelAlias(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical alias text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical alias text.</summary>
    public override string ToString() => Value;
}
