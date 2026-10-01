// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one explicitly classified observation payload owned by its containing record.</summary>
/// <remarks>The byte array is immutable evidence for redaction input only; sinks must not export it without a successful <see cref="RedactionResult"/>.</remarks>
public sealed record ObservationContent
{
    /// <summary>Initializes classified content with an owned byte payload.</summary>
    /// <param name="kind">The bounded content kind.</param>
    /// <param name="classification">The declared data classification.</param>
    /// <param name="value">The owned payload bytes.</param>
    /// <param name="fingerprint">The fingerprint matching the payload semantics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> or <paramref name="classification"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> or <paramref name="fingerprint"/> is default.</exception>
    public ObservationContent(
        ObservationContentKind kind,
        DataClassification classification,
        ImmutableArray<byte> value,
        ContentFingerprint fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind, nameof(kind));
        ArgumentOutOfRangeException.ThrowIfUndefined(classification, nameof(classification));
        if (value.IsDefault)
        {
            throw new ArgumentException("The payload must be provided; use an empty array for empty content.", nameof(value));
        }

        if (fingerprint.Value is null)
        {
            throw new ArgumentException("The fingerprint must be provided.", nameof(fingerprint));
        }

        Kind = kind;
        Classification = classification;
        Value = value;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the bounded content kind.</summary>
    public ObservationContentKind Kind { get; }

    /// <summary>Gets the declared classification.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the owned payload bytes.</summary>
    public ImmutableArray<byte> Value { get; }

    /// <summary>Gets the content fingerprint.</summary>
    public ContentFingerprint Fingerprint { get; }
}
