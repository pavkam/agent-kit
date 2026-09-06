// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one provider wire/API family, such as a specific Chat
/// Completions or native protocol profile, independent of the provider
/// brand that implements it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Two different <see cref="ProviderId"/> values can share the same
/// <see cref="ApiFamilyId"/> when they implement the same reusable wire
/// protocol (for example, several OpenAI-compatible endpoints), while a
/// single provider can expose more than one API family (for example, both
/// a Responses-style and a Chat-Completions-style endpoint). This identity
/// exists so capability negotiation and shared conformance suites can key
/// off "which wire behavior applies here" independently of brand identity.
/// </para>
/// </remarks>
public readonly record struct ApiFamilyId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiFamilyId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical API family identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ApiFamilyId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical API family identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
