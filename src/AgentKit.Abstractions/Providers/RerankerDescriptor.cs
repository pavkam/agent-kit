// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one configured, selectable reranker.</summary>
public sealed record RerankerDescriptor
{
    /// <summary>Initializes a reranker descriptor.</summary>
    /// <param name="alias">The application-facing selection key.</param>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="apiFamily">The wire family.</param>
    /// <param name="modelId">The provider model identity.</param>
    /// <param name="deploymentId">The deployment identity, when applicable.</param>
    /// <param name="capabilities">Supported portable behaviors.</param>
    /// <param name="limits">Input limits.</param>
    /// <param name="extensions">Provider-specific descriptor data.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public RerankerDescriptor(
        RerankerAlias alias,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId modelId,
        DeploymentId? deploymentId,
        RerankerCapabilities capabilities,
        RerankerLimits limits,
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
        Extensions = extensions;
    }

    /// <summary>Gets the application-facing selection key.</summary>
    public RerankerAlias Alias { get; init; }

    /// <summary>Gets the provider identity.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the wire family.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the provider model identity.</summary>
    public ModelId ModelId { get; init; }

    /// <summary>Gets the deployment identity, when applicable.</summary>
    public DeploymentId? DeploymentId { get; init; }

    /// <summary>Gets supported portable behaviors.</summary>
    public RerankerCapabilities Capabilities { get; init; }

    /// <summary>Gets input limits.</summary>
    public RerankerLimits Limits { get; init; }

    /// <summary>Gets provider-specific descriptor data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <summary>Gets the service surface, when published.</summary>
    public ProviderServiceSurfaceId ServiceSurface { get; init; }

    /// <summary>Gets the endpoint identity, when published.</summary>
    public ProviderEndpointId EndpointId { get; init; }

    /// <summary>Gets profile binding references, when configured.</summary>
    public ProviderOperationBinding? Binding { get; init; }

    /// <summary>Gets the provider model revision, when tracked.</summary>
    public ProviderModelRevision? ModelRevision { get; init; }
}
