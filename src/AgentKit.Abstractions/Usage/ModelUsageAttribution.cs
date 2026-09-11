// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the provider/model route and logical request of one charged model attempt.</summary>
/// <remarks>The enclosing usage-entry identity distinguishes retries sharing this request. Credential references are classified execution evidence and are deliberately absent from ordinary result attribution.</remarks>
public sealed record ModelUsageAttribution
{
    /// <summary>Captures validated immutable request and route identities.</summary>
    /// <param name="requestId">The nondefault logical request identity, shared by same-request retries.</param>
    /// <param name="providerId">The nondefault serving provider.</param>
    /// <param name="apiFamily">The nondefault serving API family.</param>
    /// <param name="modelId">The nondefault model identity.</param>
    /// <param name="deploymentId">The optional nondefault deployment.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required or present optional identity is default.</exception>
    public ModelUsageAttribution(ModelRequestId requestId, ProviderId providerId, ApiFamilyId apiFamily, ModelId modelId, DeploymentId? deploymentId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(providerId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(apiFamily, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelId, default);
        if (deploymentId is { } deployment) { ArgumentOutOfRangeException.ThrowIfEqual(deployment, default, nameof(deploymentId)); }
        RequestId = requestId; ProviderId = providerId; ApiFamily = apiFamily; ModelId = modelId; DeploymentId = deploymentId;
    }

    /// <summary>Gets the logical request that owns this attempt's usage.</summary>
    /// <value>A nondefault request; retries remain separate usage entries.</value>
    public ModelRequestId RequestId { get; }
    /// <summary>Gets the captured serving provider.</summary>
    /// <value>A nondefault provider identity.</value>
    public ProviderId ProviderId { get; }
    /// <summary>Gets the captured API family.</summary>
    /// <value>A nondefault wire-family identity.</value>
    public ApiFamilyId ApiFamily { get; }
    /// <summary>Gets the captured serving model.</summary>
    /// <value>A nondefault provider model identity.</value>
    public ModelId ModelId { get; }
    /// <summary>Gets the captured deployment when the route distinguishes one.</summary>
    /// <value>A nondefault identity or null; it is never inferred from registration order.</value>
    public DeploymentId? DeploymentId { get; }
}
