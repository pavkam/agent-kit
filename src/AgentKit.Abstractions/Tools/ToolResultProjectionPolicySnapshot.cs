// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures immutable projection-policy content alongside its selected key and revision.</summary>
/// <remarks>The snapshot records allowed loss-aware transformations and compatible extensions. It neither registers policy content nor resolves a reference at runtime.</remarks>
public sealed record ToolResultProjectionPolicySnapshot
{
    /// <summary>Initializes one validated immutable projection-policy snapshot.</summary>
    /// <param name="reference">The non-null selected policy key and revision.</param>
    /// <param name="bounds">The non-null finite projected-result bounds.</param>
    /// <param name="allowedTransformations">The closed set of permitted transformation flags.</param>
    /// <param name="extensions">The non-null immutable compatible extension bag whose dictionary and every value buffer are initialized.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/>, <paramref name="bounds"/>, <paramref name="extensions"/>, or its copied <see cref="ExtensionData.Values"/> dictionary is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="allowedTransformations"/> contains an unsupported flag bit.</exception>
    /// <exception cref="ArgumentException">A copied extension value has a default, uninitialized canonical-byte buffer.</exception>
    public ToolResultProjectionPolicySnapshot(
        ToolResultProjectionPolicyReference reference,
        ToolResultProjectionBounds bounds,
        ToolResultProjectionTransformations allowedTransformations,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            allowedTransformations & ~(ToolResultProjectionTransformations.Redaction
                | ToolResultProjectionTransformations.Normalization
                | ToolResultProjectionTransformations.Summarization
                | ToolResultProjectionTransformations.Truncation
                | ToolResultProjectionTransformations.Externalization),
            ToolResultProjectionTransformations.None,
            nameof(allowedTransformations));
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(extensions.Values, nameof(extensions));
        foreach (var extension in extensions.Values)
        {
            ArgumentException.ThrowIfDefault(extension.Value.CanonicalJson, nameof(extensions));
        }

        Reference = reference;
        Bounds = bounds;
        AllowedTransformations = allowedTransformations;
        Extensions = extensions;
    }

    /// <summary>Gets the immutable reference that selected this policy content.</summary>
    /// <value>The non-null key and positive revision captured at construction.</value>
    public ToolResultProjectionPolicyReference Reference { get; }

    /// <summary>Gets the immutable finite projection bounds.</summary>
    /// <value>The non-null byte and part ceilings captured at construction.</value>
    public ToolResultProjectionBounds Bounds { get; }

    /// <summary>Gets the closed set of transformations this snapshot permits.</summary>
    /// <value>Any combination of the defined <see cref="ToolResultProjectionTransformations"/> values.</value>
    public ToolResultProjectionTransformations AllowedTransformations { get; }

    /// <summary>Gets compatible immutable extension content retained with the snapshot.</summary>
    /// <value>The non-null extension bag supplied at construction.</value>
    public ExtensionData Extensions { get; }
}
