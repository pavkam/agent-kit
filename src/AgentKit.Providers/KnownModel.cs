// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// Reference data about one model a provider serves: the facts a vendor feed publishes (identity,
/// availability, reasoning and vision support, token limits, list prices) independent of how any
/// AgentKit adapter talks to that provider.
/// </summary>
/// <remarks>
/// <para>
/// A known model is not a registration. It carries no endpoint, credential, API family, or alias, and
/// registering it grants nothing. Applications use it to build a <see cref="ModelDescriptor"/> with
/// <see cref="ToDescriptor"/> instead of hand-writing limits and pricing, then register that descriptor
/// together with the matching provider adapter.
/// </para>
/// <para>
/// Capabilities that depend on the adapter's protocol rather than on the model (streaming, system
/// instructions, parallel tool calls, structured output) are deliberately absent here. They come from
/// the provider package's baseline capabilities, which <see cref="ToDescriptor"/> overlays with the
/// model facts recorded in this value.
/// </para>
/// </remarks>
public sealed record KnownModel
{
    /// <summary>Initializes a new instance of the <see cref="KnownModel"/> record.</summary>
    /// <param name="providerId">The AgentKit provider that serves the model.</param>
    /// <param name="modelId">The provider's own model identifier.</param>
    /// <param name="displayName">A human-readable name for catalogs and pickers.</param>
    /// <param name="status">The availability the feed reported at import time.</param>
    /// <param name="supportsReasoning">Whether the model exposes reasoning or extended thinking.</param>
    /// <param name="supportsVisionInput">Whether the model accepts image input.</param>
    /// <param name="supportsToolCalls">Whether the model can request tool calls.</param>
    /// <param name="limits">The published context and output token limits; unknown limits stay <see langword="null"/>.</param>
    /// <param name="pricing">The published list prices, or <see langword="null"/> when the feed publishes none.</param>
    /// <param name="replacedBy">The successor model the vendor names for a deprecated model, when any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="providerId"/> or <paramref name="modelId"/> is default, or <paramref name="displayName"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a defined value.</exception>
    public KnownModel(
        ProviderId providerId,
        ModelId modelId,
        string displayName,
        KnownModelStatus status,
        bool supportsReasoning,
        bool supportsVisionInput,
        bool supportsToolCalls,
        ModelLimits limits,
        KnownModelPricing? pricing,
        ModelId? replacedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId.Value, nameof(providerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId.Value, nameof(modelId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentNullException.ThrowIfNull(limits);

        ProviderId = providerId;
        ModelId = modelId;
        DisplayName = displayName;
        Status = status;
        SupportsReasoning = supportsReasoning;
        SupportsVisionInput = supportsVisionInput;
        SupportsToolCalls = supportsToolCalls;
        Limits = limits;
        Pricing = pricing;
        ReplacedBy = replacedBy;
    }

    /// <summary>Gets the AgentKit provider that serves the model.</summary>
    public ProviderId ProviderId { get; }

    /// <summary>Gets the provider's own model identifier.</summary>
    public ModelId ModelId { get; }

    /// <summary>Gets a human-readable name for catalogs and pickers.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the availability the feed reported at import time.</summary>
    public KnownModelStatus Status { get; }

    /// <summary>Gets a value indicating whether the model exposes reasoning or extended thinking.</summary>
    public bool SupportsReasoning { get; }

    /// <summary>Gets a value indicating whether the model accepts image input.</summary>
    public bool SupportsVisionInput { get; }

    /// <summary>Gets a value indicating whether the model can request tool calls.</summary>
    public bool SupportsToolCalls { get; }

    /// <summary>Gets the published context and output token limits; unknown limits are <see langword="null"/> and never mean unlimited.</summary>
    public ModelLimits Limits { get; }

    /// <summary>Gets the published list prices, or <see langword="null"/> when the feed publishes none.</summary>
    public KnownModelPricing? Pricing { get; }

    /// <summary>Gets the successor the vendor names for a deprecated model, when any.</summary>
    public ModelId? ReplacedBy { get; }

    /// <summary>
    /// Builds the <see cref="ModelDescriptor"/> an application registers for this model under one alias,
    /// overlaying the model facts recorded here on a provider package's protocol baseline.
    /// </summary>
    /// <param name="alias">The application-facing alias the agent selects.</param>
    /// <param name="apiFamily">The API family of the adapter that will serve the alias, from the provider package's defaults.</param>
    /// <param name="baselineCapabilities">
    /// The provider package's default capabilities. Its streaming, system-instruction, parallel-tool-call,
    /// and structured-output flags are kept; reasoning, vision, and tool-call flags are replaced by this model's facts.
    /// </param>
    /// <returns>A descriptor with this model's identity, capabilities, limits, and pricing and no deployment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baselineCapabilities"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="alias"/> or <paramref name="apiFamily"/> is default.</exception>
    public ModelDescriptor ToDescriptor(ModelAlias alias, ApiFamilyId apiFamily, ModelCapabilities baselineCapabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
        ArgumentException.ThrowIfNullOrWhiteSpace(apiFamily.Value, nameof(apiFamily));
        ArgumentNullException.ThrowIfNull(baselineCapabilities);

        var capabilities = baselineCapabilities with
        {
            SupportsReasoning = SupportsReasoning,
            SupportsVisionInput = SupportsVisionInput,
            SupportsToolCalls = SupportsToolCalls,
            SupportsParallelToolCalls = SupportsToolCalls && baselineCapabilities.SupportsParallelToolCalls,
        };

        return new ModelDescriptor(
            alias,
            ProviderId,
            apiFamily,
            ModelId,
            deploymentId: null,
            capabilities,
            Limits,
            Pricing?.ToModelPricing(),
            ExtensionData.Empty);
    }
}
