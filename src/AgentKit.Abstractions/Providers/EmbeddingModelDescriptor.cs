// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Describes one configured, selectable embedding model: its identity,
/// capabilities, limits, and optional pricing, independent of the
/// application-facing <see cref="EmbeddingModelAlias"/> used to select it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This mirrors <see cref="ModelDescriptor"/>'s shape exactly but is a
/// distinct type, not a reuse of it, because embedding generation is a
/// separate provider contract from conversational generation: the same
/// alias text may independently identify a chat model and an embedding
/// model without merging their contracts, capabilities, or selection
/// paths.
/// </para>
/// </remarks>
public sealed record EmbeddingModelDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingModelDescriptor"/> record.</summary>
    /// <param name="alias">The application-facing selection key for this descriptor.</param>
    /// <param name="providerId">The provider that serves this model.</param>
    /// <param name="apiFamily">The wire/API family used to reach this model.</param>
    /// <param name="modelId">The provider's own identity for this model.</param>
    /// <param name="deploymentId">
    /// The concrete deployment or endpoint used, when the provider
    /// distinguishes deployments from the underlying model identity.
    /// </param>
    /// <param name="capabilities">The portable behaviors this model supports.</param>
    /// <param name="limits">The input and output limits of this model.</param>
    /// <param name="pricing">The published per-token pricing of this model, when known.</param>
    /// <param name="extensions">Provider-specific descriptor data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="capabilities"/>, <paramref name="limits"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    public EmbeddingModelDescriptor(
        EmbeddingModelAlias alias,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId modelId,
        DeploymentId? deploymentId,
        EmbeddingCapabilities capabilities,
        EmbeddingLimits limits,
        ModelPricing? pricing,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(extensions);

        Alias = alias;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        ModelId = modelId;
        DeploymentId = deploymentId;
        Capabilities = capabilities;
        Limits = limits;
        Pricing = pricing;
        Extensions = extensions;
    }

    /// <summary>Gets the application-facing selection key for this descriptor.</summary>
    public EmbeddingModelAlias Alias { get; init; }

    /// <summary>Gets the provider that serves this model.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the wire/API family used to reach this model.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the provider's own identity for this model.</summary>
    public ModelId ModelId { get; init; }

    /// <summary>
    /// Gets the concrete deployment or endpoint used, when the provider
    /// distinguishes deployments from the underlying model identity.
    /// </summary>
    public DeploymentId? DeploymentId { get; init; }

    /// <summary>Gets the portable behaviors this model supports.</summary>
    public EmbeddingCapabilities Capabilities { get; init; }

    /// <summary>Gets the input and output limits of this model.</summary>
    public EmbeddingLimits Limits { get; init; }

    /// <summary>Gets the published per-token pricing of this model, when known.</summary>
    public ModelPricing? Pricing { get; init; }

    /// <summary>Gets provider-specific descriptor data.</summary>
    public ExtensionData Extensions { get; init; }
}
