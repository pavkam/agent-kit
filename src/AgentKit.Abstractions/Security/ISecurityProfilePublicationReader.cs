// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reads immutable security-profile publications by their full composition coordinates.</summary>
/// <remarks>Readers return only exact published coordinates and never consult ambient current configuration.</remarks>
public interface ISecurityProfilePublicationReader
{
    /// <summary>Reads one exact immutable publication.</summary>
    /// <param name="agentId">The non-default agent that owns the requested publication.</param>
    /// <param name="agentDefinitionRevision">The nonnegative selected definition revision.</param>
    /// <param name="configurationVersion">The positive selected configuration revision.</param>
    /// <param name="profileKey">The explicit nonblank security profile key.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>The exact publication or a typed unavailable result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/>, <paramref name="agentDefinitionRevision"/>, or <paramref name="configurationVersion"/> is outside its documented range.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="profileKey"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the read completes.</exception>
    public ValueTask<SecurityProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        ConfigurationVersion configurationVersion,
        SecurityProfileKey profileKey,
        CancellationToken cancellationToken = default);
}
