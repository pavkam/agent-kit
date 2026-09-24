// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Capability-profile resolution failed for a typed catalog reason.</summary>
public sealed record McpCapabilityProfileResolutionFailed: McpCapabilityProfileResolution
{
    /// <summary>Initializes a failed profile resolution.</summary>
    /// <param name="profileId">The requested profile id.</param>
    /// <param name="safeMessage">A non-sensitive failure summary.</param>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> or <paramref name="safeMessage"/> is uninitialized.</exception>
    public McpCapabilityProfileResolutionFailed(CapabilityProfileId profileId, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        ProfileId = profileId;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the requested profile id.</summary>
    public CapabilityProfileId ProfileId { get; }

    /// <summary>Gets the non-sensitive failure summary.</summary>
    public string SafeMessage { get; }
}
