// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Defines allocation-efficient content-free artifact coordination events.</summary>
internal static partial class ArtifactLog
{
    /// <summary>Records one terminal artifact operation outcome.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="operation">The bounded artifact operation.</param>
    /// <param name="tenantId">The tenant when known.</param>
    /// <param name="artifactId">The artifact identity when established.</param>
    /// <param name="preparationId">The preparation identity when established.</param>
    /// <param name="outcome">The bounded semantic outcome.</param>
    [LoggerMessage(25000, LogLevel.Debug, "Artifact operation {Operation} for tenant {TenantId}, artifact {ArtifactId}, and preparation {PreparationId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, string operation, string? tenantId, string? artifactId, string? preparationId, string outcome);

    /// <summary>Records one unexpected artifact exception without protected content.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="operation">The bounded artifact operation.</param>
    /// <param name="tenantId">The tenant when known.</param>
    /// <param name="artifactId">The artifact identity when established.</param>
    /// <param name="preparationId">The preparation identity when established.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(25001, LogLevel.Error, "Artifact operation {Operation} for tenant {TenantId}, artifact {ArtifactId}, and preparation {PreparationId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string operation, string? tenantId, string? artifactId, string? preparationId, string errorType);
}
