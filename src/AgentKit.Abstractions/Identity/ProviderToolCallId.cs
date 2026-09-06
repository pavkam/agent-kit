// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A provider-supplied tool-call identifier preserved as external
/// correlation. It never replaces the authoritative <see cref="ToolCallId"/>
/// that AgentKit uses internally to correlate a call with its terminal
/// result.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Providers that support native tool calling usually mint their own call
/// identifier when the model requests a call. AgentKit records that value
/// on the corresponding <see cref="ToolCallPart"/> so it can echo the exact
/// identifier back to the provider on continuation, while every internal
/// decision — authorization, scheduling, result correlation — uses the
/// stable <see cref="ToolCallId"/> instead. If a provider supplies no
/// usable call identifier, the adapter may synthesize one and record that
/// provenance rather than leaving this value unset without explanation.
/// </para>
/// </remarks>
public readonly record struct ProviderToolCallId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderToolCallId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty provider-supplied tool-call identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ProviderToolCallId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the provider-supplied tool-call identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the provider-supplied identifier text.</summary>
    public override string ToString() => Value;
}
