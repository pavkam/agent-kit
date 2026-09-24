// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Resolves transport factories by declared transport profile kind.</summary>
public interface IMcpTransportFactoryCatalog
{
    /// <summary>Resolves one transport profile to its registered factory.</summary>
    /// <param name="transportProfile">The declared transport profile.</param>
    /// <param name="cancellationToken">A token that cancels resolution.</param>
    /// <returns>The terminal factory resolution outcome.</returns>
    public ValueTask<McpTransportFactoryResolution> ResolveAsync(
        McpTransportProfile transportProfile,
        CancellationToken cancellationToken);
}
