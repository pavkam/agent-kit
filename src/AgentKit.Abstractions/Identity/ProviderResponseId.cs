// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A provider-supplied response identifier preserved as external
/// correlation for support and debugging. It never replaces the
/// authoritative <see cref="ModelRequestId"/> used internally to correlate a
/// request's own lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// AgentKit treats every provider-supplied identifier — this one,
/// <see cref="ProviderRequestId"/>, and <see cref="ProviderToolCallId"/> —
/// as untrusted external correlation data rather than internal identity.
/// It is carried through <see cref="ProviderResponseIdentity"/> so
/// diagnostics and support workflows can match a durable
/// <see cref="AssistantMessage"/> back to the exact vendor response that
/// produced it.
/// </para>
/// </remarks>
public readonly record struct ProviderResponseId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderResponseId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty provider-supplied response identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ProviderResponseId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the provider-supplied response identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the provider-supplied identifier text.</summary>
    public override string ToString() => Value;
}
