// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A provider-supplied request identifier, such as an <c>x-request-id</c>
/// header value returned by a vendor API, preserved as external correlation
/// for support and debugging. It never replaces the authoritative
/// <see cref="ModelRequestId"/> used internally to correlate a request's own
/// lifecycle.
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
/// <see cref="ProviderResponseId"/>, and <see cref="ProviderToolCallId"/> —
/// as untrusted external correlation data: useful for matching a support
/// ticket to a specific vendor request, but never substituted for AgentKit's
/// own identities in internal logic, and never assumed to be present,
/// globally unique, or stable across retries unless a specific provider's
/// documented behavior says otherwise.
/// </para>
/// </remarks>
public readonly record struct ProviderRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderRequestId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty provider-supplied request identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ProviderRequestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the provider-supplied request identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the provider-supplied identifier text.</summary>
    public override string ToString() => Value;
}
