// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Points at one piece of verifiable evidence that supports a goal result.</summary>
/// <remarks>The reference is untrusted agent-produced data until a verifier resolves it and checks the optional fingerprint; carrying it proves nothing by itself.</remarks>
public sealed record EvidenceReference
{
    /// <summary>Initializes a validated evidence reference.</summary>
    /// <param name="kind">The non-blank evidence category, such as an artifact or message.</param>
    /// <param name="reference">The non-blank opaque locator.</param>
    /// <param name="fingerprint">The content fingerprint to verify, or <see langword="null"/> when none was supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/>, <paramref name="reference"/>, or a present <paramref name="fingerprint"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="kind"/> or <paramref name="reference"/> is null.</exception>
    public EvidenceReference(string kind, string reference, ContentHash? fingerprint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (fingerprint is { } hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash.Value, nameof(fingerprint));
        }

        Kind = kind;
        Reference = reference;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the evidence category.</summary>
    public string Kind { get; }

    /// <summary>Gets the opaque locator.</summary>
    public string Reference { get; }

    /// <summary>Gets the content fingerprint to verify, or <see langword="null"/>.</summary>
    public ContentHash? Fingerprint { get; }
}
